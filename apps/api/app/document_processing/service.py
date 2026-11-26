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
    DocumentAlreadyProcessingError,
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
        if document.status == DocumentStatus.PROCESSING:
            raise DocumentAlreadyProcessingError
        workspace_id = document.workspace_id
        persisted_document_id = document.id
        version = max(document.versions, key=lambda item: item.version_number)
        document.status = DocumentStatus.PROCESSING
        document.failure_code = None
        await self._session.commit()

        # Stage 1: Storage read
        try:
            content = await self._file_storage.get(object_key=version.object_key)
        except FileStorageError as exc:
            document.status = DocumentStatus.FAILED
            document.failure_code = "STORAGE_READ_FAILED"
            await self._session.commit()
            raise DocumentParseFailedError(
                "스토리지에서 원본 문서를 읽지 못했습니다.",
                stage="storage_read",
                details={"stage": "storage_read", "stage_code": "STORAGE_READ_FAILED"},
            ) from exc

        # Stage 2: Text extraction
        try:
            parser = self._parser_registry.for_media_type(document.media_type)
            parsed = await asyncio.to_thread(parser.parse, content)
        except DocumentParseError as exc:
            raw_reason = str(exc)
            if "Encrypted" in raw_reason:
                stage_code = "DOCUMENT_ENCRYPTED_PDF"
                stored_code = "DOCUMENT_ENCRYPTED_PDF"
                msg = "암호로 보호된 PDF는 텍스트를 추출할 수 없습니다. 암호를 해제한 뒤 다시 업로드해 주세요."
            elif "UTF-8" in raw_reason:
                stage_code = "DOCUMENT_ENCODING_ERROR"
                stored_code = "DOCUMENT_ENCODING_ERROR"
                msg = "지원하지 않는 문자 인코딩입니다. UTF-8로 저장한 텍스트 파일을 업로드해 주세요."
            else:
                stage_code = "DOCUMENT_PARSE_FAILED"
                stored_code = "DOCUMENT_PARSE_FAILED"
                msg = (
                    "PDF 문서에서 텍스트를 추출하지 못했습니다. 손상된 PDF 파일인지 확인해 주세요."
                    if document.media_type == "application/pdf"
                    else "텍스트 문서에서 내용을 추출하지 못했습니다. 파일 형식을 확인해 주세요."
                )
            document.status = DocumentStatus.FAILED
            document.failure_code = stored_code
            await self._session.commit()
            raise DocumentParseFailedError(
                msg,
                stage="text_extraction",
                details={"stage": "text_extraction", "stage_code": stage_code, "reason": raw_reason},
            ) from exc

        # Stage 3: Chunking
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

        if chunk_index == 0:
            document.status = DocumentStatus.FAILED
            document.failure_code = "DOCUMENT_EMPTY_TEXT"
            await self._session.commit()
            empty_msg = (
                "텍스트 파일에 색인할 내용이 없습니다. 공백 외의 본문이 포함된 UTF-8 파일인지 확인해 주세요."
                if document.media_type == "text/plain"
                else "PDF에서 색인할 텍스트를 찾지 못했습니다. 빈 문서이거나 OCR이 필요한 스캔 이미지 PDF일 수 있습니다."
            )
            raise DocumentParseFailedError(
                empty_msg,
                stage="chunking",
                details={"stage": "chunking", "stage_code": "DOCUMENT_EMPTY_TEXT"},
            )

        # Stage 4-6: DB staging, Embedding, Vector Indexing, Commit
        current_stage = "db_commit"
        try:
            await self._repository.replace_pages(version=version, pages=pages)
            chunks = [chunk for page in pages for chunk in page.chunks]

            current_stage = "embedding"
            vectors = await self._embedding_model.embed_documents([chunk.text for chunk in chunks])
            if len(vectors) != len(chunks):
                raise EmbeddingModelError("embedding count does not match chunk count")

            current_stage = "vector_indexing"
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

            current_stage = "db_commit"
            document.status = DocumentStatus.READY
            document.failure_code = None
            await self._session.commit()
            reloaded = await self._repository.get_for_owner(
                document_id=persisted_document_id,
                owner_id=owner_id,
            )
            if reloaded is not None:
                document = reloaded
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
                stage = "embedding"
                stage_code = "DOCUMENT_EMBEDDING_FAILED"
                detail_msg = "문서 임베딩 벡터 생성(Embedding) 단계에서 오류가 발생했습니다."
            elif isinstance(exc, VectorStoreError):
                stage = "vector_indexing"
                stage_code = "DOCUMENT_VECTOR_INDEX_FAILED"
                detail_msg = "벡터 데이터베이스(Qdrant) 색인(Vector Indexing) 단계에서 오류가 발생했습니다."
            elif isinstance(exc, SQLAlchemyError):
                stage = "db_commit"
                stage_code = "DOCUMENT_DB_COMMIT_FAILED"
                detail_msg = "문서 페이지 및 청크 정보를 데이터베이스에 저장하는 단계에서 오류가 발생했습니다."
            else:
                stage = current_stage
                stage_code = "DOCUMENT_PROCESSING_FAILED"
                detail_msg = "문서 색인 처리 중 오류가 발생했습니다."

            raise DocumentProcessingError(
                detail_msg,
                stage=stage,
                details={"stage": stage, "stage_code": stage_code},
            ) from exc
        return document
