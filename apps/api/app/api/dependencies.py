import uuid
from collections.abc import AsyncIterator
from typing import Annotated

from fastapi import Depends, Header, Request
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.embedding import EmbeddingModel
from app.models.generation import GenerationModel
from app.storage.protocol import FileStorage
from app.vector_store.protocol import VectorStore


async def get_db_session(request: Request) -> AsyncIterator[AsyncSession]:
    async with request.app.state.session_factory() as session:
        yield session


def get_current_user_id(
    x_user_id: Annotated[str | None, Header(alias="X-User-ID")] = None,
    authorization: Annotated[str | None, Header(alias="Authorization")] = None,
) -> uuid.UUID:
    if authorization and authorization.startswith("Bearer "):
        token = authorization.removeprefix("Bearer ").strip()
        from app.auth.service import verify_access_token

        return verify_access_token(token)

    if x_user_id:
        try:
            return uuid.UUID(x_user_id)
        except ValueError as e:
            from fastapi import HTTPException

            raise HTTPException(
                status_code=422,
                detail="X-User-ID 헤더의 UUID 형식이 올바르지 않습니다.",
            ) from e

    from fastapi import HTTPException

    raise HTTPException(
        status_code=422,
        detail="사용자 인증 식별자가 필요합니다. (X-User-ID 또는 Authorization 헤더)",
    )


def get_file_storage(request: Request) -> FileStorage:
    return request.app.state.file_storage


def get_embedding_model(request: Request) -> EmbeddingModel:
    return request.app.state.embedding_model


def get_vector_store(request: Request) -> VectorStore:
    return request.app.state.vector_store


def get_generation_model(request: Request) -> GenerationModel:
    return request.app.state.generation_model


DatabaseSession = Annotated[AsyncSession, Depends(get_db_session)]
CurrentUserId = Annotated[uuid.UUID, Depends(get_current_user_id)]
FileStorageDependency = Annotated[FileStorage, Depends(get_file_storage)]
EmbeddingModelDependency = Annotated[EmbeddingModel, Depends(get_embedding_model)]
VectorStoreDependency = Annotated[VectorStore, Depends(get_vector_store)]
GenerationModelDependency = Annotated[GenerationModel, Depends(get_generation_model)]
