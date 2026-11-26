import logging
import socket
import uuid
from collections.abc import AsyncIterator, Sequence
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from app.api.v1.router import api_router
from app.core.config import Settings, get_settings
from app.core.errors import (
    ApplicationError,
    application_error_handler,
    http_exception_handler,
    unhandled_exception_handler,
    validation_exception_handler,
)
from app.db.health import DatabaseReadinessCheck
from app.db.session import create_engine, create_session_factory
from app.health.checks import ReadinessCheck
from app.models.embedding import EmbeddingModel, SentenceTransformerEmbeddingModel
from app.models.generation import GenerationModel, OpenAICompatibleGenerationModel
from app.storage.health import FileStorageReadinessCheck
from app.storage.local import LocalFileStorage
from app.storage.protocol import FileStorage
from app.storage.s3 import S3FileStorage
from app.vector_store.health import VectorStoreReadinessCheck
from app.vector_store.protocol import VectorStore
from app.vector_store.qdrant import QdrantVectorStore

logger = logging.getLogger("dograc")


class _ManagedLocalQdrantStore:
    """
    Lazy disk-persistent local Qdrant wrapper.
    Acquires the Qdrant local storage lock ONLY during FastAPI lifespan (never at module import time)
    and releases it deterministically on shutdown so app restarts reload persisted vectors cleanly.
    """

    def __init__(self, *, qdrant_dir, collection_name: str) -> None:
        self._qdrant_dir = qdrant_dir
        self._collection_name = collection_name
        self._client = None
        self._store: QdrantVectorStore | None = None

    def _ensure_open(self) -> QdrantVectorStore:
        if self._store is None:
            self._qdrant_dir.mkdir(parents=True, exist_ok=True)
            from qdrant_client import AsyncQdrantClient

            self._client = AsyncQdrantClient(path=str(self._qdrant_dir))
            self._store = QdrantVectorStore(
                url="",
                collection_name=self._collection_name,
                client=self._client,
            )
        return self._store

    async def close(self) -> None:
        if self._client is not None:
            client = self._client
            self._client = None
            self._store = None
            await client.close()

    async def ensure_collection(self, *, dimensions: int) -> None:
        await self._ensure_open().ensure_collection(dimensions=dimensions)

    async def upsert(self, records) -> None:
        await self._ensure_open().upsert(records)

    async def delete_document(self, *, workspace_id: uuid.UUID, document_id: uuid.UUID) -> None:
        await self._ensure_open().delete_document(workspace_id=workspace_id, document_id=document_id)

    async def search(self, *, query_vector: list[float], workspace_id: uuid.UUID, document_ids: list[uuid.UUID] | None, limit: int):
        return await self._ensure_open().search(
            query_vector=query_vector,
            workspace_id=workspace_id,
            document_ids=document_ids,
            limit=limit,
        )

    async def check(self) -> None:
        await self._ensure_open().check()


def is_service_accessible(host: str, port: int, timeout: float = 0.6) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except Exception:
        return False


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
    is_sqlite = False
    local_qdrant_client = None
    data_dir = app_settings.resolved_data_dir()

    if session_factory is None:
        db_url = app_settings.database_url
        # If configured with postgresql but local postgres port (5432) is closed, fallback to persistent local SQLite
        if "postgresql" in db_url and not is_service_accessible("localhost", 5432):
            data_dir.mkdir(parents=True, exist_ok=True)
            sqlite_path = data_dir / "dograc_local.db"
            logger.info("PostgreSQL 서비스 미감지: 영속 로컬 SQLite 데이터베이스(%s)로 자동 폴백합니다.", sqlite_path)
            db_url = f"sqlite+aiosqlite:///{sqlite_path.as_posix()}"
            is_sqlite = True
        engine = create_engine(db_url)
        session_factory = create_session_factory(engine)

    if file_storage is None:
        # If MinIO port (9000) is closed, fallback to persistent LocalFileStorage inside DOGRAC_DATA_DIR
        if not is_service_accessible("localhost", 9000):
            storage_dir = data_dir / "storage"
            logger.info("MinIO 스토리지 미감지: 영속 로컬 파일시스템 스토리지(%s)로 자동 폴백합니다.", storage_dir)
            file_storage = LocalFileStorage(base_dir=storage_dir)
        else:
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
        # If Qdrant port (6333) is closed, fallback to lazy DISK-PERSISTENT local Qdrant (survives app restarts!)
        if not is_service_accessible("localhost", 6333):
            qdrant_dir = data_dir / "qdrant"
            logger.info("Qdrant 서비스 미감지: 영속 로컬 디스크 Qdrant(%s)로 자동 폴백합니다.", qdrant_dir)
            local_qdrant_client = _ManagedLocalQdrantStore(
                qdrant_dir=qdrant_dir,
                collection_name=app_settings.qdrant_collection,
            )
            vector_store = local_qdrant_client
        else:
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
        # Initialize SQLite tables automatically if in standalone local mode
        if is_sqlite and engine is not None:
            try:
                from app.db.base import Base
                import app.db.models.workspace
                import app.db.models.document
                import app.db.models.conversation
                import app.db.models.user

                async with engine.begin() as conn:
                    await conn.run_sync(Base.metadata.create_all)
                logger.info("로컬 SQLite 테이블 스키마 초기화 완료.")
            except Exception as e:
                logger.warning(f"로컬 SQLite 초기화 중 경고: {e}")

        # Recover documents left stuck in PROCESSING after an unexpected crash/kill
        try:
            from sqlalchemy import update
            from app.db.models.document import Document, DocumentStatus

            async with session_factory() as recovery_session:
                result = await recovery_session.execute(
                    update(Document)
                    .where(Document.status == DocumentStatus.PROCESSING)
                    .values(
                        status=DocumentStatus.FAILED,
                        failure_code="DOCUMENT_PROCESS_INTERRUPTED",
                    )
                )
                if result.rowcount and result.rowcount > 0:
                    await recovery_session.commit()
                    logger.warning(
                        "이전 비정상 종료로 PROCESSING 상태에 고착된 문서 %d건을 FAILED(DOCUMENT_PROCESS_INTERRUPTED)로 복구했습니다.",
                        result.rowcount,
                    )
        except Exception as e:
            logger.warning("비정상 종료 문서 상태 복구 점검 중 경고: %s", e)

        # Ensure vector store collection
        if vector_store is not None:
            try:
                await vector_store.ensure_collection(dimensions=app_settings.embedding_dimensions)
            except Exception as e:
                logger.warning(f"벡터 컬렉션 초기화 중 경고: {e}")

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
            if local_qdrant_client is not None:
                try:
                    await local_qdrant_client.close()
                except Exception as e:
                    logger.warning("로컬 Qdrant 클라이언트 종료 중 경고: %s", e)
            if engine is not None:
                await engine.dispose()

    application = FastAPI(
        title=app_settings.app_name,
        version="0.1.0",
        docs_url="/docs" if app_settings.docs_enabled else None,
        redoc_url=None,
        lifespan=lifespan,
    )
    application.state.settings = app_settings
    application.state.readiness_checks = app_readiness_checks
    application.state.session_factory = session_factory
    application.state.file_storage = file_storage
    application.state.embedding_model = embedding_model
    application.state.vector_store = vector_store
    application.state.generation_model = generation_model

    application.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    @application.middleware("http")
    async def attach_correlation_id(request: Request, call_next):
        correlation_id = request.headers.get("X-Correlation-ID", "").strip()[:64] or uuid.uuid4().hex[:12]
        request.state.correlation_id = correlation_id
        response = await call_next(request)
        response.headers.setdefault("X-Correlation-ID", correlation_id)
        return response

    application.add_exception_handler(ApplicationError, application_error_handler)
    application.add_exception_handler(HTTPException, http_exception_handler)
    application.add_exception_handler(RequestValidationError, validation_exception_handler)
    application.add_exception_handler(Exception, unhandled_exception_handler)
    application.include_router(api_router, prefix="/api/v1")
    return application


app = create_app()
