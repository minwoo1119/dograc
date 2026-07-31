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
from app.storage.health import FileStorageReadinessCheck
from app.storage.protocol import FileStorage
from app.storage.s3 import S3FileStorage


def create_app(
    *,
    settings: Settings | None = None,
    readiness_checks: Sequence[ReadinessCheck] | None = None,
    session_factory: async_sessionmaker[AsyncSession] | None = None,
    file_storage: FileStorage | None = None,
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
    app_readiness_checks = (
        tuple(readiness_checks)
        if readiness_checks is not None
        else (
            DatabaseReadinessCheck(session_factory),
            FileStorageReadinessCheck(file_storage),
        )
    )

    @asynccontextmanager
    async def lifespan(application: FastAPI) -> AsyncIterator[None]:
        application.state.settings = app_settings
        application.state.readiness_checks = app_readiness_checks
        application.state.session_factory = session_factory
        application.state.file_storage = file_storage
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
