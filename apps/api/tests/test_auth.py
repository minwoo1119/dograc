import asyncio
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

from fastapi.testclient import TestClient
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.core.config import Settings
from app.db.base import Base
from app.main import create_app


@contextmanager
def auth_test_client(database_path: Path) -> Iterator[TestClient]:
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


def test_register_and_login_flow(tmp_path: Path) -> None:
    with auth_test_client(tmp_path / "auth_flow.db") as client:
        # 1. 회원가입
        reg_res = client.post(
            "/api/v1/auth/register",
            json={
                "email": "user@example.com",
                "username": "testuser",
                "password": "secretpassword",
            },
        )
        assert reg_res.status_code == 201
        reg_data = reg_res.json()
        assert "access_token" in reg_data
        assert reg_data["user"]["email"] == "user@example.com"
        assert reg_data["user"]["username"] == "testuser"

        token = reg_data["access_token"]

        # 2. 내 정보 조회 (Bearer Token)
        me_res = client.get(
            "/api/v1/auth/me",
            headers={"Authorization": f"Bearer {token}"},
        )
        assert me_res.status_code == 200
        me_data = me_res.json()
        assert me_data["email"] == "user@example.com"

        # 3. 로그인 (이메일로 로그인)
        login_res = client.post(
            "/api/v1/auth/login",
            json={
                "email_or_username": "user@example.com",
                "password": "secretpassword",
            },
        )
        assert login_res.status_code == 200
        assert "access_token" in login_res.json()

        # 4. 로그인 (아이디로 로그인)
        login_uname_res = client.post(
            "/api/v1/auth/login",
            json={
                "email_or_username": "testuser",
                "password": "secretpassword",
            },
        )
        assert login_uname_res.status_code == 200


def test_login_invalid_password(tmp_path: Path) -> None:
    with auth_test_client(tmp_path / "invalid_pwd.db") as client:
        client.post(
            "/api/v1/auth/register",
            json={
                "email": "user@example.com",
                "username": "testuser",
                "password": "correctpassword",
            },
        )
        res = client.post(
            "/api/v1/auth/login",
            json={
                "email_or_username": "testuser",
                "password": "wrongpassword",
            },
        )
        assert res.status_code == 401


def test_register_duplicate_email(tmp_path: Path) -> None:
    with auth_test_client(tmp_path / "dup.db") as client:
        client.post(
            "/api/v1/auth/register",
            json={
                "email": "dup@example.com",
                "username": "user1",
                "password": "password123",
            },
        )
        res = client.post(
            "/api/v1/auth/register",
            json={
                "email": "dup@example.com",
                "username": "user2",
                "password": "password123",
            },
        )
        assert res.status_code == 409


def test_access_workspace_with_bearer_token(tmp_path: Path) -> None:
    with auth_test_client(tmp_path / "workspace_auth.db") as client:
        reg_res = client.post(
            "/api/v1/auth/register",
            json={
                "email": "owner@example.com",
                "username": "owner",
                "password": "ownerpassword",
            },
        )
        token = reg_res.json()["access_token"]

        # 워크스페이스 생성 (Bearer Token 사용)
        ws_res = client.post(
            "/api/v1/workspaces",
            headers={"Authorization": f"Bearer {token}"},
            json={"name": "Protected Workspace"},
        )
        assert ws_res.status_code == 201
        ws_data = ws_res.json()
        assert ws_data["name"] == "Protected Workspace"

        # 워크스페이스 조회
        list_res = client.get(
            "/api/v1/workspaces",
            headers={"Authorization": f"Bearer {token}"},
        )
        assert list_res.status_code == 200
        assert len(list_res.json()) == 1
