import uuid
from typing import Annotated

from fastapi import APIRouter, File, Request, Response, UploadFile, status

from app.api.dependencies import (
    CurrentUserId,
    DatabaseSession,
    FileStorageDependency,
    VectorStoreDependency,
)
from app.core.errors import ErrorResponse
from app.documents.schemas import DocumentResponse
from app.documents.service import DocumentService

router = APIRouter()
document_router = APIRouter()


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


@router.get(
    "",
    response_model=list[DocumentResponse],
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def list_documents(
    workspace_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
    file_storage: FileStorageDependency,
) -> list[DocumentResponse]:
    documents = await DocumentService(
        session=session,
        file_storage=file_storage,
    ).list_for_workspace(
        workspace_id=workspace_id,
        owner_id=user_id,
    )
    return [DocumentResponse.model_validate(doc) for doc in documents]


@document_router.get(
    "/{document_id}",
    response_model=DocumentResponse,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def get_document(
    document_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
    file_storage: FileStorageDependency,
) -> DocumentResponse:
    document = await DocumentService(
        session=session,
        file_storage=file_storage,
    ).get(
        document_id=document_id,
        owner_id=user_id,
    )
    return DocumentResponse.model_validate(document)


@document_router.delete(
    "/{document_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def delete_document(
    document_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
    file_storage: FileStorageDependency,
    vector_store: VectorStoreDependency,
) -> Response:
    await DocumentService(
        session=session,
        file_storage=file_storage,
        vector_store=vector_store,
    ).delete(
        document_id=document_id,
        owner_id=user_id,
    )
    return Response(status_code=status.HTTP_204_NO_CONTENT)
