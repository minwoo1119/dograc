import uuid
from typing import Annotated

from fastapi import APIRouter, File, Request, UploadFile, status

from app.api.dependencies import CurrentUserId, DatabaseSession, FileStorageDependency
from app.core.errors import ErrorResponse
from app.documents.schemas import DocumentResponse
from app.documents.service import DocumentService

router = APIRouter()


@router.post(
    "",
    response_model=DocumentResponse,
    status_code=status.HTTP_201_CREATED,
    responses={
        status.HTTP_400_BAD_REQUEST: {"model": ErrorResponse},
        status.HTTP_404_NOT_FOUND: {"model": ErrorResponse},
        status.HTTP_413_CONTENT_TOO_LARGE: {"model": ErrorResponse},
        status.HTTP_415_UNSUPPORTED_MEDIA_TYPE: {"model": ErrorResponse},
        status.HTTP_500_INTERNAL_SERVER_ERROR: {"model": ErrorResponse},
    },
)
async def upload_document(
    workspace_id: uuid.UUID,
    request: Request,
    session: DatabaseSession,
    user_id: CurrentUserId,
    file_storage: FileStorageDependency,
    file: Annotated[UploadFile, File()],
) -> DocumentResponse:
    max_size_bytes = request.app.state.settings.max_document_size_bytes
    content = await file.read(max_size_bytes + 1)
    document = await DocumentService(
        session=session,
        file_storage=file_storage,
        max_size_bytes=max_size_bytes,
    ).upload(
        workspace_id=workspace_id,
        owner_id=user_id,
        file_name=file.filename or "",
        declared_media_type=file.content_type,
        content=content,
    )
    return DocumentResponse.model_validate(document)
