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
    user_id: Annotated[uuid.UUID, Header(alias="X-User-ID")],
) -> uuid.UUID:
    return user_id


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
