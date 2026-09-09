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
from app.vector_store.protocol import VectorRecord, VectorSearchResult


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
        self.deleted_documents: list[tuple[uuid.UUID, uuid.UUID]] = []

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
        self.deleted_documents.append((workspace_id, document_id))
        self.records = {
            record_id: record
            for record_id, record in self.records.items()
            if record.payload.get("document_id") != str(document_id)
        }

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
            max_document_size_bytes=1024 * 1024,
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


def test_list_and_get_documents(tmp_path: Path) -> None:
    with client_factory(tmp_path) as (client, context):
        owner_id = uuid.uuid4()
        other_user_id = uuid.uuid4()

        # Workspace 생성
        ws_res = client.post(
            "/api/v1/workspaces",
            json={"name": "Research Workspace"},
            headers={"X-User-ID": str(owner_id)},
        )
        assert ws_res.status_code == 201
        workspace_id = ws_res.json()["id"]

        # 2개 문서 업로드
        res1 = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            files={"file": ("first.txt", b"First file content", "text/plain")},
            headers={"X-User-ID": str(owner_id)},
        )
        assert res1.status_code == 201
        doc1_id = res1.json()["id"]

        res2 = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            files={"file": ("second.txt", b"Second file content", "text/plain")},
            headers={"X-User-ID": str(owner_id)},
        )
        assert res2.status_code == 201
        doc2_id = res2.json()["id"]

        # 목록 조회
        list_res = client.get(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(owner_id)},
        )
        assert list_res.status_code == 200
        docs = list_res.json()
        assert len(docs) == 2
        assert docs[0]["id"] == doc2_id  # 최신순 정렬 확인
        assert docs[1]["id"] == doc1_id

        # 다른 사용자의 목록 조회 시 404
        other_list_res = client.get(
            f"/api/v1/workspaces/{workspace_id}/documents",
            headers={"X-User-ID": str(other_user_id)},
        )
        assert other_list_res.status_code == 404

        # 단건 상세 조회
        get_res = client.get(
            f"/api/v1/documents/{doc1_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert get_res.status_code == 200
        assert get_res.json()["source_file_name"] == "first.txt"

        # 다른 사용자의 단건 조회 시 404
        other_get_res = client.get(
            f"/api/v1/documents/{doc1_id}",
            headers={"X-User-ID": str(other_user_id)},
        )
        assert other_get_res.status_code == 404


def test_delete_document_success_and_cleanup(tmp_path: Path) -> None:
    with client_factory(tmp_path) as (client, context):
        owner_id = uuid.uuid4()
        other_user_id = uuid.uuid4()

        ws_res = client.post(
            "/api/v1/workspaces",
            json={"name": "Project Workspace"},
            headers={"X-User-ID": str(owner_id)},
        )
        workspace_id = ws_res.json()["id"]

        upload_res = client.post(
            f"/api/v1/workspaces/{workspace_id}/documents",
            files={"file": ("cleanup.txt", b"Cleanup test content", "text/plain")},
            headers={"X-User-ID": str(owner_id)},
        )
        doc_id = upload_res.json()["id"]
        assert len(context.file_storage.objects) == 1

        # 다른 사용자의 삭제 시도 -> 404
        forbidden_del = client.delete(
            f"/api/v1/documents/{doc_id}",
            headers={"X-User-ID": str(other_user_id)},
        )
        assert forbidden_del.status_code == 404
        assert len(context.file_storage.objects) == 1

        # 소유자의 삭제 실행 -> 204
        del_res = client.delete(
            f"/api/v1/documents/{doc_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert del_res.status_code == 204

        # 스토리지에서 삭제되었는지 확인
        assert len(context.file_storage.objects) == 0
        assert len(context.file_storage.deleted_keys) == 1

        # VectorStore의 delete_document 호출 확인
        deleted_pair = (uuid.UUID(workspace_id), uuid.UUID(doc_id))
        assert deleted_pair in context.vector_store.deleted_documents

        # 조회 시 404 확인
        get_res = client.get(
            f"/api/v1/documents/{doc_id}",
            headers={"X-User-ID": str(owner_id)},
        )
        assert get_res.status_code == 404
