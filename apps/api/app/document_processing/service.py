import asyncio
import hashlib
import uuid

from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.document import Document, DocumentPage, DocumentStatus
from app.document_processing.parsers import ParserRegistry
from app.document_processing.protocol import DocumentParseError
from app.documents.errors import (
    DocumentNotFoundError,
    DocumentParseFailedError,
    DocumentProcessingError,
)
from app.documents.repository import DocumentRepository
from app.storage.protocol import FileStorage, FileStorageError


class DocumentProcessingService:
    def __init__(
        self,
        *,
        session: AsyncSession,
        file_storage: FileStorage,
        parser_registry: ParserRegistry | None = None,
    ) -> None:
        self._session = session
        self._file_storage = file_storage
        self._parser_registry = parser_registry or ParserRegistry()
        self._repository = DocumentRepository(session)

    async def process(self, *, document_id: uuid.UUID, owner_id: uuid.UUID) -> Document:
        document = await self._repository.get_for_owner(
            document_id=document_id,
            owner_id=owner_id,
        )
        if document is None:
            raise DocumentNotFoundError
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

        pages = [
            DocumentPage(
                document_version_id=version.id,
                page_number=page.page_number,
                text=page.text,
                parser_name=parsed.parser_name,
                content_hash=hashlib.sha256(page.text.encode()).hexdigest(),
            )
            for page in parsed.pages
        ]
        try:
            await self._repository.replace_pages(version=version, pages=pages)
            document.status = DocumentStatus.READY
            document.failure_code = None
            await self._session.commit()
            await self._session.refresh(document)
        except SQLAlchemyError as exc:
            await self._session.rollback()
            raise DocumentProcessingError from exc
        return document
