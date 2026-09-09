import asyncio
import uuid
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

from fastapi.testclient import TestClient
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.core.config import Settings
from app.db.base import Base
from app.db.models.conversation import Message, MessageRole, Trace
from app.main import create_app
from app.vector_store.protocol import VectorRecord, VectorSearchResult


class MemoryFileStorage:
    async def get(self, *, object_key: str) -> bytes:
        return b""

    async def put(self, *, object_key: str, content: bytes, media_type: str) -> None:
        pass

    async def delete(self, *, object_key: str) -> None:
        pass

    async def check(self) -> None:
        return None


class DeterministicEmbeddingModel:
    dimensions = 3

    async def embed_documents(self, texts: list[str]) -> list[list[float]]:
        return [[1.0, 0.0, 0.0] for _ in texts]

    async def embed_query(self, text: str) -> list[float]:
        return [1.0, 0.0, 0.0]


class MemoryVectorStore:
    async def ensure_collection(self, *, dimensions: int) -> None:
        pass

    async def upsert(self, records: list[VectorRecord]) -> None:
        pass

    async def delete_document(self, *, workspace_id: uuid.UUID, document_id: uuid.UUID) -> None:
        pass

    async def search(
        self,
        *,
        query_vector: list[float],
        workspace_id: uuid.UUID,
        document_ids: list[uuid.UUID] | None,
        limit: int,
    ) -> list[VectorSearchResult]:
        return []

    async def check(self) -> None:
        return None


class _TestContext:
    def __init__(self, db_path: Path) -> None:
        self.engine = create_async_engine(f"sqlite+aiosqlite:///{db_path}")
        self.session_factory = async_sessionmaker(self.engine, expire_on_commit=False)
        self.file_storage = MemoryFileStorage()
        self.embedding_model = DeterministicEmbeddingModel()
        self.vector_store = MemoryVectorStore()

    async def setup(self) -> None:
        async with self.engine.begin() as connection:
            await connection.run_sync(Base.metadata.create_all)

    async def teardown(self) -> None:
        await self.engine.dispose()


@contextmanager
def client_factory(tmp_path: Path) -> Iterator[tuple[TestClient, _TestContext]]:
    context = _TestContext(tmp_path / "test.db")
    asyncio.run(context.setup())
    app = create_app(
        settings=Settings(
            app_env="test",
            database_url="sqlite+aiosqlite:///:memory:",
        ),
        readiness_checks=(),
        session_factory=context.session_factory,
        file_storage=context.file_storage,
        embedding_model=context.embedding_model,
        vector_store=context.vector_store,
    )
    with TestClient(app) as test_client:
        yield test_client, context
    asyncio.run(context.teardown())


def test_conversation_crud_and_isolation(tmp_path: Path) -> None:
    with client_factory(tmp_path) as (client, _):
        owner_id = uuid.uuid4()
        other_user_id = uuid.uuid4()

        # Workspace 생성
        ws_res = client.post(
            "/api/v1/workspaces",
            json={"name": "Chat Workspace"},
            headers={"X-User-ID": str(owner_id)},
        )
        assert ws_res.status_code == 201
        workspace_id = ws_res.json()["id"]

        # Conversation 생성
        c_res = client.post(
            f"/api/v1/workspaces/{workspace_id}/conversations",
            json={"title": "Doc QA Session"},
            headers={"X-User-ID": str(owner_id)},
        )
        assert c_res.status_code == 201
        conv_id = c_res.json()["id"]
        assert c_res.json()["title"] == "Doc QA Session"

        # Conversation 목록 조회
        list_res = client.get(
            f"/api/v1/workspaces/{workspace_id}/conversations",
            headers={"X-User-ID": str(owner_id)},
        )
        assert list_res.status_code == 200
        items = list_res.json()
        assert len(items) == 1
        assert items[0]["id"] == conv_id

        # 다른 사용자의 조회 시 404
        assert (
            client.get(
                f"/api/v1/workspaces/{workspace_id}/conversations",
                headers={"X-User-ID": str(other_user_id)},
            ).status_code
            == 404
        )

        # Conversation 단건 조회
        get_res = client.get(
            f"/api/v1/conversations/{conv_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert get_res.status_code == 200
        assert get_res.json()["messages"] == []

        # 다른 사용자의 단건 조회 시 404
        assert (
            client.get(
                f"/api/v1/conversations/{conv_id}",
                headers={"X-User-ID": str(other_user_id)},
            ).status_code
            == 404
        )

        # Conversation 삭제
        del_res = client.delete(
            f"/api/v1/conversations/{conv_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert del_res.status_code == 204

        # 삭제 후 조회 시 404
        assert (
            client.get(
                f"/api/v1/conversations/{conv_id}",
                headers={"X-User-ID": str(owner_id)},
            ).status_code
            == 404
        )


def test_trace_retrieval(tmp_path: Path) -> None:
    with client_factory(tmp_path) as (client, context):
        owner_id = uuid.uuid4()
        other_user_id = uuid.uuid4()

        ws_res = client.post(
            "/api/v1/workspaces",
            json={"name": "Trace Workspace"},
            headers={"X-User-ID": str(owner_id)},
        )
        workspace_id = uuid.UUID(ws_res.json()["id"])

        c_res = client.post(
            f"/api/v1/workspaces/{workspace_id}/conversations",
            json={"title": "Trace Session"},
            headers={"X-User-ID": str(owner_id)},
        )
        conv_id = uuid.UUID(c_res.json()["id"])

        # 직접 메시지와 Trace를 DB에 추가
        trace_id = uuid.uuid4()
        message_id = uuid.uuid4()

        async def seed_trace() -> None:
            async with context.session_factory() as session:
                msg = Message(
                    id=message_id,
                    conversation_id=conv_id,
                    role=MessageRole.ASSISTANT,
                    content="Answer with citation",
                )
                trace = Trace(
                    id=trace_id,
                    message_id=message_id,
                    workspace_id=workspace_id,
                    retrieved_chunks=[
                        {
                            "chunk_id": str(uuid.uuid4()),
                            "source_file_name": "test.pdf",
                            "page_number": 1,
                            "score": 0.88,
                            "text": "sample context",
                        }
                    ],
                    prompt="Prompt string",
                    model_name="test-llm",
                    latency_ms=120,
                )
                session.add(msg)
                session.add(trace)
                await session.commit()

        asyncio.run(seed_trace())

        # Trace 조회
        t_res = client.get(
            f"/api/v1/traces/{trace_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert t_res.status_code == 200
        trace_data = t_res.json()
        assert trace_data["id"] == str(trace_id)
        assert trace_data["model_name"] == "test-llm"
        assert len(trace_data["retrieved_chunks"]) == 1

        # 다른 사용자의 Trace 조회 시 404
        assert (
            client.get(
                f"/api/v1/traces/{trace_id}",
                headers={"X-User-ID": str(other_user_id)},
            ).status_code
            == 404
        )
