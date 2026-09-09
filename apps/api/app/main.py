from collections.abc import AsyncIterator, Sequence
from contextlib import asynccontextmanager

from fastapi import FastAPI
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from app.api.v1.router import api_router
from app.core.config import Settings, get_settings
from app.core.errors import ApplicationError, application_error_handler
from app.db.health import DatabaseReadinessCheck
from app.db.session import create_engine, create_session_factory
from app.health.checks import ReadinessCheck
from app.models.embedding import EmbeddingModel, SentenceTransformerEmbeddingModel
from app.models.generation import GenerationModel, OpenAICompatibleGenerationModel
from app.storage.health import FileStorageReadinessCheck
from app.storage.protocol import FileStorage
from app.storage.s3 import S3FileStorage
from app.vector_store.health import VectorStoreReadinessCheck
from app.vector_store.protocol import VectorStore
from app.vector_store.qdrant import QdrantVectorStore


def create_app(
    *,
    settings: Settings | None = None,
    readiness_checks: Sequence[ReadinessCheck] | None = None,
    session_factory: async_sessionmaker[AsyncSession] | None = None,
    file_storage: FileStorage | None = None,
    embedding_model: EmbeddingModel | None = None,
    vector_store: VectorStore | None = None,
    generation_model: GenerationModel | None = None,
) -> FastAPI:
    app_settings = settings or get_settings()
    engine = None
    if session_factory is None:
        engine = create_engine(app_settings.database_url)
        session_factory = create_session_factory(engine)
    if file_storage is None:
        file_storage = S3FileStorage(
            endpoint_url=app_settings.object_storage_endpoint,
            access_key=app_settings.object_storage_access_key,
            secret_key=app_settings.object_storage_secret_key,
            bucket=app_settings.object_storage_bucket,
            region=app_settings.object_storage_region,
        )
    if embedding_model is None:
        embedding_model = SentenceTransformerEmbeddingModel(
            app_settings.embedding_model,
            dimensions=app_settings.embedding_dimensions,
        )
    if vector_store is None:
        vector_store = QdrantVectorStore(
            url=app_settings.qdrant_url,
            api_key=app_settings.qdrant_api_key,
            collection_name=app_settings.qdrant_collection,
        )
    if generation_model is None:
        generation_model = OpenAICompatibleGenerationModel(
            model_name=app_settings.generation_model,
            base_url=app_settings.generation_base_url,
            api_key=app_settings.generation_api_key,
        )
    app_readiness_checks = (
        tuple(readiness_checks)
        if readiness_checks is not None
        else (
            DatabaseReadinessCheck(session_factory),
            FileStorageReadinessCheck(file_storage),
            VectorStoreReadinessCheck(vector_store),
        )
    )

    @asynccontextmanager
    async def lifespan(application: FastAPI) -> AsyncIterator[None]:
        application.state.settings = app_settings
        application.state.readiness_checks = app_readiness_checks
        application.state.session_factory = session_factory
        application.state.file_storage = file_storage
        application.state.embedding_model = embedding_model
        application.state.vector_store = vector_store
        application.state.generation_model = generation_model
        try:
            yield
        finally:
            if engine is not None:
                await engine.dispose()

    application = FastAPI(
        title=app_settings.app_name,
        version="0.1.0",
        docs_url="/docs" if app_settings.docs_enabled else None,
        redoc_url=None,
        lifespan=lifespan,
    )
    application.add_exception_handler(ApplicationError, application_error_handler)
    application.include_router(api_router, prefix="/api/v1")
    return application


app = create_app()
