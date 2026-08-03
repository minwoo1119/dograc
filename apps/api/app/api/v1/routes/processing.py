import uuid

from fastapi import APIRouter, status

from app.api.dependencies import CurrentUserId, DatabaseSession, FileStorageDependency
from app.core.errors import ErrorResponse
from app.document_processing.service import DocumentProcessingService
from app.documents.schemas import DocumentResponse

router = APIRouter()


@router.post(
    "/{document_id}/process",
    response_model=DocumentResponse,
    responses={
        status.HTTP_404_NOT_FOUND: {"model": ErrorResponse},
        status.HTTP_422_UNPROCESSABLE_CONTENT: {"model": ErrorResponse},
        status.HTTP_500_INTERNAL_SERVER_ERROR: {"model": ErrorResponse},
    },
)
async def process_document(
    document_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
    file_storage: FileStorageDependency,
) -> DocumentResponse:
    document = await DocumentProcessingService(
        session=session,
        file_storage=file_storage,
    ).process(document_id=document_id, owner_id=user_id)
    return DocumentResponse.model_validate(document)
