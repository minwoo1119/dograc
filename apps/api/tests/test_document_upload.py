import asyncio
import uuid
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path
from unittest.mock import AsyncMock

import pytest
from fastapi.testclient import TestClient
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.core.config import Settings
from app.db.base import Base
from app.db.models.workspace import Workspace
from app.documents.errors import DocumentProcessingError
from app.documents.service import DocumentService
from app.main import create_app


class MemoryFileStorage:
    def __init__(self) -> None:
        self.objects: dict[str, tuple[bytes, str]] = {}
        self.deleted_keys: list[str] = []

    async def put(self, *, object_key: str, content: bytes, media_type: str) -> None:
        self.objects[object_key] = (content, media_type)

    async def delete(self, *, object_key: str) -> None:
        self.deleted_keys.append(object_key)
        self.objects.pop(object_key, None)

    async def check(self) -> None:
        return None


@contextmanager
def document_client(
    database_path: Path,
    storage: MemoryFileStorage,
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
