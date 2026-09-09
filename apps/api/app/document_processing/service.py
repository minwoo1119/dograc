import asyncio
import hashlib
import logging
import uuid

from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.document import Document, DocumentChunk, DocumentPage, DocumentStatus
from app.document_processing.chunking import RecursiveCharacterChunker
from app.document_processing.parsers import ParserRegistry
from app.document_processing.protocol import DocumentParseError
from app.documents.errors import (
    DocumentNotFoundError,
    DocumentParseFailedError,
    DocumentProcessingError,
)
from app.documents.repository import DocumentRepository
from app.models.embedding import EmbeddingModel, EmbeddingModelError
from app.storage.protocol import FileStorage, FileStorageError
from app.vector_store.protocol import VectorRecord, VectorStore, VectorStoreError

logger = logging.getLogger(__name__)


class DocumentProcessingService:
    def __init__(
        self,
        *,
        session: AsyncSession,
        file_storage: FileStorage,
        embedding_model: EmbeddingModel,
        vector_store: VectorStore,
        parser_registry: ParserRegistry | None = None,
        chunker: RecursiveCharacterChunker | None = None,
    ) -> None:
        self._session = session
        self._file_storage = file_storage
        self._parser_registry = parser_registry or ParserRegistry()
        self._chunker = chunker or RecursiveCharacterChunker(
            chunk_size=1200,
            chunk_overlap=150,
        )
        self._embedding_model = embedding_model
        self._vector_store = vector_store
        self._repository = DocumentRepository(session)

    async def process(self, *, document_id: uuid.UUID, owner_id: uuid.UUID) -> Document:
        document = await self._repository.get_for_owner(
            document_id=document_id,
            owner_id=owner_id,
        )
        if document is None:
            raise DocumentNotFoundError
        workspace_id = document.workspace_id
        persisted_document_id = document.id
        version = max(document.versions, key=lambda item: item.version_number)
        document.status = DocumentStatus.PROCESSING
        document.failure_code = None
        await self._session.commit()

        try:
            content = await self._file_storage.get(object_key=version.object_key)
            parser = self._parser_registry.for_media_type(document.media_type)
            parsed = await asyncio.to_thread(parser.parse, content)
        except (FileStorageError, DocumentParseError) as exc:
            document.status = DocumentStatus.FAILED
            document.failure_code = "DOCUMENT_PARSE_FAILED"
            await self._session.commit()
            raise DocumentParseFailedError from exc

        pages: list[DocumentPage] = []
        chunk_index = 0
        for page in parsed.pages:
            page_entity = DocumentPage(
                document_version_id=version.id,
                page_number=page.page_number,
                text=page.text,
                parser_name=parsed.parser_name,
                content_hash=hashlib.sha256(page.text.encode()).hexdigest(),
            )
            page_entity.chunks = []
            for chunk in self._chunker.split(page.text):
                page_entity.chunks.append(
                    DocumentChunk(
                        workspace_id=document.workspace_id,
                        document_id=document.id,
                        document_version_id=version.id,
                        source_file_name=document.source_file_name,
                        page_number=page.page_number,
                        section_title=page_entity.section_title,
                        chunk_index=chunk_index,
                        parser_name=parsed.parser_name,
                        chunking_strategy=self._chunker.name,
                        text=chunk.text,
                        content_hash=hashlib.sha256(chunk.text.encode()).hexdigest(),
                    )
                )
                chunk_index += 1
            pages.append(page_entity)
        try:
            await self._repository.replace_pages(version=version, pages=pages)
            chunks = [chunk for page in pages for chunk in page.chunks]
            vectors = await self._embedding_model.embed_documents([chunk.text for chunk in chunks])
            if len(vectors) != len(chunks):
                raise EmbeddingModelError("embedding count does not match chunk count")
            await self._vector_store.ensure_collection(dimensions=self._embedding_model.dimensions)
            await self._vector_store.delete_document(
                workspace_id=workspace_id,
                document_id=persisted_document_id,
            )
            await self._vector_store.upsert(
                [
                    VectorRecord(
                        id=chunk.id,
                        vector=vector,
                        payload={
                            "workspace_id": str(chunk.workspace_id),
                            "document_id": str(chunk.document_id),
                            "document_version_id": str(chunk.document_version_id),
                            "chunk_id": str(chunk.id),
                            "source_file_name": chunk.source_file_name,
                            "page_number": chunk.page_number,
                            "chunk_index": chunk.chunk_index,
                            "parser_name": chunk.parser_name,
                            "chunking_strategy": chunk.chunking_strategy,
                            "content_hash": chunk.content_hash,
                        },
                    )
                    for chunk, vector in zip(chunks, vectors, strict=True)
                ]
            )
            document.status = DocumentStatus.READY
            document.failure_code = None
            await self._session.commit()
            await self._session.refresh(document)
        except (SQLAlchemyError, EmbeddingModelError, VectorStoreError) as exc:
            await self._session.rollback()
            try:
                await self._vector_store.delete_document(
                    workspace_id=workspace_id,
                    document_id=persisted_document_id,
                )
            except VectorStoreError:
                logger.exception(
                    "Failed to compensate vector indexing",
                    extra={"document_id": str(persisted_document_id)},
                )
            failed_document = await self._repository.get_for_owner(
                document_id=document_id,
                owner_id=owner_id,
            )
            if failed_document is not None:
                failed_document.status = DocumentStatus.FAILED
                failed_document.failure_code = "DOCUMENT_PROCESSING_FAILED"
                await self._session.commit()

            if isinstance(exc, EmbeddingModelError):
                detail_msg = "문서 임베딩 벡터 생성(Embedding) 중 오류가 발생했습니다."
            elif isinstance(exc, VectorStoreError):
                detail_msg = "벡터 데이터베이스(Qdrant) 색인 중 오류가 발생했습니다."
            elif isinstance(exc, SQLAlchemyError):
                detail_msg = "문서 페이지 및 청크 정보를 데이터베이스에 기록하지 못했습니다."
            else:
                detail_msg = "문서 색인 처리 중 오류가 발생했습니다."

            raise DocumentProcessingError(detail_msg) from exc
        return document
