import logging
import uuid
from pathlib import PurePosixPath

from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.document import Document, DocumentStatus, DocumentVersion
from app.documents.errors import DocumentProcessingError, DocumentUploadValidationError
from app.documents.repository import DocumentRepository
from app.documents.validation import DocumentValidationError, validate_document_upload
from app.storage.protocol import FileStorage, FileStorageError
from app.workspaces.service import WorkspaceService

logger = logging.getLogger(__name__)


class DocumentService:
    def __init__(
        self,
        *,
        session: AsyncSession,
        file_storage: FileStorage,
        max_size_bytes: int,
    ) -> None:
        self._session = session
        self._file_storage = file_storage
        self._max_size_bytes = max_size_bytes
        self._repository = DocumentRepository(session)

    async def upload(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
        file_name: str,
        declared_media_type: str | None,
        content: bytes,
    ) -> Document:
        await WorkspaceService(self._session).get(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )
        try:
            validated = validate_document_upload(
                file_name=file_name,
                declared_media_type=declared_media_type,
                content=content,
                max_size_bytes=self._max_size_bytes,
            )
        except DocumentValidationError as exc:
            raise DocumentUploadValidationError(exc) from exc

        document_id = uuid.uuid4()
        version_id = uuid.uuid4()
        extension = PurePosixPath(validated.file_name).suffix.lower()
        object_key = (
            f"workspaces/{workspace_id}/documents/{document_id}/"
            f"versions/{version_id}/original{extension}"
        )
        try:
            await self._file_storage.put(
                object_key=object_key,
                content=validated.content,
                media_type=validated.media_type,
            )
        except FileStorageError as exc:
            raise DocumentProcessingError from exc

        document = Document(
            id=document_id,
            workspace_id=workspace_id,
            source_file_name=validated.file_name,
            media_type=validated.media_type,
            status=DocumentStatus.UPLOADED,
            versions=[
                DocumentVersion(
                    id=version_id,
                    version_number=1,
                    object_key=object_key,
                    content_hash=validated.content_hash,
                    size_bytes=validated.size_bytes,
                )
            ],
        )
        try:
            await self._repository.add(document)
            await self._session.commit()
        except SQLAlchemyError as exc:
            await self._session.rollback()
            try:
                await self._file_storage.delete(object_key=object_key)
            except OSError:
                logger.exception(
                    "Failed to compensate object upload",
                    extra={"object_key": object_key},
                )
            raise DocumentProcessingError from exc
        return document
