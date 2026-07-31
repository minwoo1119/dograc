import asyncio
import uuid
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

from fastapi.testclient import TestClient
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.core.config import Settings
from app.db.base import Base
from app.main import create_app


@contextmanager
def workspace_client(database_path: Path) -> Iterator[TestClient]:
    database_url = f"sqlite+aiosqlite:///{database_path.as_posix()}"
    engine = create_async_engine(database_url)
    session_factory = async_sessionmaker(engine, expire_on_commit=False)

    async def create_schema() -> None:
        async with engine.begin() as connection:
            await connection.run_sync(Base.metadata.create_all)

    asyncio.run(create_schema())
    app = create_app(
        settings=Settings(database_url=database_url),
        readiness_checks=(),
        session_factory=session_factory,
    )
    with TestClient(app) as client:
        yield client
    asyncio.run(engine.dispose())


def test_workspace_requires_user_identity(tmp_path: Path) -> None:
    with workspace_client(tmp_path / "required-user.db") as client:
        response = client.get("/api/v1/workspaces")

    assert response.status_code == 422


def test_create_workspace_normalizes_name(tmp_path: Path) -> None:
    user_id = uuid.uuid4()
    with workspace_client(tmp_path / "create.db") as client:
        response = client.post(
            "/api/v1/workspaces",
            headers={"X-User-ID": str(user_id)},
            json={"name": "  Product   Manuals  "},
        )

    assert response.status_code == 201
    assert response.json()["name"] == "Product Manuals"
    assert "id" in response.json()


def test_list_workspaces_is_isolated_by_owner(tmp_path: Path) -> None:
    first_user_id = uuid.uuid4()
    second_user_id = uuid.uuid4()
    with workspace_client(tmp_path / "isolation.db") as client:
        client.post(
            "/api/v1/workspaces",
            headers={"X-User-ID": str(first_user_id)},
            json={"name": "First user workspace"},
        )
        client.post(
            "/api/v1/workspaces",
            headers={"X-User-ID": str(second_user_id)},
            json={"name": "Second user workspace"},
        )

        response = client.get(
            "/api/v1/workspaces",
            headers={"X-User-ID": str(first_user_id)},
        )

    assert response.status_code == 200
    assert [workspace["name"] for workspace in response.json()] == ["First user workspace"]
