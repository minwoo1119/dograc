import asyncio
import uuid
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path
from unittest.mock import AsyncMock

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import func, select
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.core.config import Settings
from app.db.base import Base
from app.db.models.document import Document, DocumentChunk, DocumentPage
from app.db.models.workspace import Workspace
from app.documents.errors import DocumentProcessingError
from app.documents.service import DocumentService
from app.main import create_app
from app.vector_store.protocol import VectorRecord, VectorStoreError


class MemoryFileStorage:
    def __init__(self) -> None:
        self.objects: dict[str, tuple[bytes, str]] = {}
        self.deleted_keys: list[str] = []

    async def get(self, *, object_key: str) -> bytes:
        return self.objects[object_key][0]

    async def put(self, *, object_key: str, content: bytes, media_type: str) -> None:
        self.objects[object_key] = (content, media_type)

    async def delete(self, *, object_key: str) -> None:
        self.deleted_keys.append(object_key)
        self.objects.pop(object_key, None)

    async def check(self) -> None:
        return None


class DeterministicEmbeddingModel:
    dimensions = 3

    async def embed_documents(self, texts: list[str]) -> list[list[float]]:
        return [[float(len(text)), 1.0, 0.0] for text in texts]

    async def embed_query(self, text: str) -> list[float]:
        return (await self.embed_documents([text]))[0]


class MemoryVectorStore:
    def __init__(self) -> None:
        self.records: dict[uuid.UUID, VectorRecord] = {}

    async def ensure_collection(self, *, dimensions: int) -> None:
        assert dimensions == 3

    async def upsert(self, records: list[VectorRecord]) -> None:
        self.records.update({record.id: record for record in records})

    async def delete_document(
        self,
        *,
        workspace_id: uuid.UUID,
        document_id: uuid.UUID,
    ) -> None:
        self.records = {
            record_id: record
            for record_id, record in self.records.items()
            if not (
                record.payload["workspace_id"] == str(workspace_id)
                and record.payload["document_id"] == str(document_id)
            )
        }

    async def check(self) -> None:
        return None


class FailingVectorStore(MemoryVectorStore):
    async def upsert(self, records: list[VectorRecord]) -> None:
        raise VectorStoreError("qdrant unavailable")


@contextmanager
def document_client(
    database_path: Path,
    storage: MemoryFileStorage,
    vector_store: MemoryVectorStore | None = None,
    *,
    max_size_bytes: int = 1024,
) -> Iterator[TestClient]:
    database_url = f"sqlite+aiosqlite:///{database_path.as_posix()}"
    engine = create_async_engine(database_url)
    session_factory = async_sessionmaker(engine, expire_on_commit=False)

    async def create_schema() -> None:
        async with engine.begin() as connection:
            await connection.run_sync(Base.metadata.create_all)

    asyncio.run(create_schema())
    app = create_app(
        settings=Settings(
            database_url=database_url,
            max_document_size_bytes=max_size_bytes,
        ),
        readiness_checks=(),
        session_factory=session_factory,
        file_storage=storage,
        embedding_model=DeterministicEmbeddingModel(),
        vector_store=vector_store or MemoryVectorStore(),
    )
    with TestClient(app) as client:
        yield client
    asyncio.run(engine.dispose())


def create_workspace(client: TestClient, user_id: uuid.UUID) -> str:
    response = client.post(
        "/api/v1/workspaces",
        headers={"X-User-ID": str(user_id)},
        json={"name": "Documents"},
    )
    assert response.status_code == 201
    return response.json()["id"]


def test_upload_stores_original_and_document_metadata(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    user_id = uuid.uuid4()
    with document_client(tmp_path / "upload.db", storage) as client:
        workspace_id = create_workspace(client, user_id)
        response = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(user_id)},
            files={"file": ("manual.pdf", b"%PDF-1.7\nfixture", "application/pdf")},
        )

    assert response.status_code == 201
    assert response.json()["source_file_name"] == "manual.pdf"
    assert response.json()["status"] == "uploaded"
    assert len(storage.objects) == 1
    object_key, stored = next(iter(storage.objects.items()))
    assert object_key.startswith(f"workspaces/{workspace_id}/documents/")
    assert object_key.endswith("/original.pdf")
    assert stored == (b"%PDF-1.7\nfixture", "application/pdf")


def test_upload_rejects_workspace_owned_by_another_user(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    owner_id = uuid.uuid4()
    with document_client(tmp_path / "owner.db", storage) as client:
        workspace_id = create_workspace(client, owner_id)
        response = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(uuid.uuid4())},
            files={"file": ("notes.txt", b"private", "text/plain")},
        )

    assert response.status_code == 404
    assert response.json()["error"]["code"] == "WORKSPACE_NOT_FOUND"
    assert storage.objects == {}


def test_upload_rejects_oversized_file_before_storage(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    user_id = uuid.uuid4()
    with document_client(tmp_path / "size.db", storage, max_size_bytes=3) as client:
        workspace_id = create_workspace(client, user_id)
        response = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(user_id)},
            files={"file": ("notes.txt", b"1234", "text/plain")},
        )

    assert response.status_code == 413
    assert response.json()["error"]["code"] == "FILE_TOO_LARGE"
    assert storage.objects == {}


@pytest.mark.asyncio
async def test_upload_deletes_object_when_database_commit_fails(tmp_path: Path) -> None:
    engine = create_async_engine(f"sqlite+aiosqlite:///{(tmp_path / 'failure.db').as_posix()}")
    session_factory = async_sessionmaker(engine, expire_on_commit=False)
    async with engine.begin() as connection:
        await connection.run_sync(Base.metadata.create_all)

    owner_id = uuid.uuid4()
    workspace_id = uuid.uuid4()
    storage = MemoryFileStorage()
    async with session_factory() as session:
        session.add(Workspace(id=workspace_id, owner_id=owner_id, name="Documents"))
        await session.commit()
        session.commit = AsyncMock(side_effect=SQLAlchemyError("commit failed"))

        with pytest.raises(DocumentProcessingError):
            await DocumentService(
                session=session,
                file_storage=storage,
                max_size_bytes=1024,
            ).upload(
                workspace_id=workspace_id,
                owner_id=owner_id,
                file_name="notes.txt",
                declared_media_type="text/plain",
                content=b"content",
            )

    await engine.dispose()
    assert storage.objects == {}
    assert len(storage.deleted_keys) == 1


def test_process_document_persists_pages_idempotently(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    vector_store = MemoryVectorStore()
    user_id = uuid.uuid4()
    with document_client(tmp_path / "process.db", storage, vector_store) as client:
        workspace_id = create_workspace(client, user_id)
        uploaded = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(user_id)},
            files={"file": ("notes.txt", "페이지 내용".encode(), "text/plain")},
        )
        document_id = uploaded.json()["id"]
        first = client.post(
            f"/api/v1/documents/{document_id}/process",
            headers={"X-User-ID": str(user_id)},
        )
        second = client.post(
            f"/api/v1/documents/{document_id}/process",
            headers={"X-User-ID": str(user_id)},
        )

        async def load_pages() -> tuple[int, str, int]:
            async with client.app.state.session_factory() as session:
                count = await session.scalar(select(func.count()).select_from(DocumentPage))
                text = await session.scalar(select(DocumentPage.text))
                chunk_count = await session.scalar(select(func.count()).select_from(DocumentChunk))
                return int(count or 0), text or "", int(chunk_count or 0)

        page_count, page_text, chunk_count = asyncio.run(load_pages())

    assert first.status_code == 200
    assert second.status_code == 200
    assert second.json()["status"] == "ready"
    assert (page_count, page_text) == (1, "페이지 내용")
    assert chunk_count == 1
    assert len(vector_store.records) == 1
    record = next(iter(vector_store.records.values()))
    assert record.payload["workspace_id"] == workspace_id
    assert record.payload["page_number"] == 1


def test_process_document_records_parse_failure(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    user_id = uuid.uuid4()
    with document_client(tmp_path / "parse-failure.db", storage) as client:
        workspace_id = create_workspace(client, user_id)
        uploaded = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(user_id)},
            files={"file": ("broken.pdf", b"%PDF-corrupt", "application/pdf")},
        )
        document_id = uploaded.json()["id"]
        response = client.post(
            f"/api/v1/documents/{document_id}/process",
            headers={"X-User-ID": str(user_id)},
        )

        async def load_status() -> tuple[str, str | None]:
            async with client.app.state.session_factory() as session:
                document = await session.get(Document, uuid.UUID(document_id))
                assert document is not None
                return document.status.value, document.failure_code

        stored_status = asyncio.run(load_status())

    assert response.status_code == 422
    assert response.json()["error"]["code"] == "DOCUMENT_PARSE_FAILED"
    assert stored_status == ("failed", "DOCUMENT_PARSE_FAILED")


def test_process_document_hides_another_owners_document(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    owner_id = uuid.uuid4()
    with document_client(tmp_path / "process-owner.db", storage) as client:
        workspace_id = create_workspace(client, owner_id)
        uploaded = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(owner_id)},
            files={"file": ("notes.txt", b"private", "text/plain")},
        )
        response = client.post(
            f"/api/v1/documents/{uploaded.json()['id']}/process",
            headers={"X-User-ID": str(uuid.uuid4())},
        )

    assert response.status_code == 404
    assert response.json()["error"]["code"] == "DOCUMENT_NOT_FOUND"


def test_process_document_records_vector_index_failure(tmp_path: Path) -> None:
    storage = MemoryFileStorage()
    user_id = uuid.uuid4()
    vector_store = FailingVectorStore()
    with document_client(tmp_path / "vector-failure.db", storage, vector_store) as client:
        workspace_id = create_workspace(client, user_id)
        uploaded = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(user_id)},
            files={"file": ("notes.txt", b"content", "text/plain")},
        )
        document_id = uploaded.json()["id"]
        response = client.post(
            f"/api/v1/documents/{document_id}/process",
            headers={"X-User-ID": str(user_id)},
        )

        async def load_status() -> tuple[str, str | None]:
            async with client.app.state.session_factory() as session:
                document = await session.get(Document, uuid.UUID(document_id))
                assert document is not None
                return document.status.value, document.failure_code

        stored_status = asyncio.run(load_status())

    assert response.status_code == 500
    assert response.json()["error"]["code"] == "DOCUMENT_PROCESSING_FAILED"
    assert stored_status == ("failed", "DOCUMENT_PROCESSING_FAILED")
    assert vector_store.records == {}
