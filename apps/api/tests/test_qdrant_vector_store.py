import uuid

import pytest
from qdrant_client import AsyncQdrantClient

from app.vector_store.protocol import VectorRecord
from app.vector_store.qdrant import QdrantVectorStore


@pytest.mark.asyncio
async def test_qdrant_adapter_upserts_traceable_payload() -> None:
    client = AsyncQdrantClient(location=":memory:")
    store = QdrantVectorStore(url="http://unused", collection_name="chunks", client=client)
    await store.ensure_collection(dimensions=3)
    chunk_id = uuid.uuid4()
    workspace_id = uuid.uuid4()
    document_id = uuid.uuid4()

    await store.upsert(
        [
            VectorRecord(
                id=chunk_id,
                vector=[1.0, 0.0, 0.0],
                payload={
                    "workspace_id": str(workspace_id),
                    "document_id": str(document_id),
                    "page_number": 1,
                },
            )
        ]
    )

    result = await client.retrieve("chunks", ids=[str(chunk_id)], with_payload=True)
    assert result[0].payload == {
        "workspace_id": str(workspace_id),
        "document_id": str(document_id),
        "page_number": 1,
    }


@pytest.mark.asyncio
async def test_delete_document_is_scoped_by_workspace_and_document() -> None:
    client = AsyncQdrantClient(location=":memory:")
    store = QdrantVectorStore(url="http://unused", collection_name="chunks", client=client)
    await store.ensure_collection(dimensions=2)
    workspace_id = uuid.uuid4()
    document_id = uuid.uuid4()
    retained_id = uuid.uuid4()
    await store.upsert(
        [
            VectorRecord(
                id=uuid.uuid4(),
                vector=[1.0, 0.0],
                payload={
                    "workspace_id": str(workspace_id),
                    "document_id": str(document_id),
                },
            ),
            VectorRecord(
                id=retained_id,
                vector=[0.0, 1.0],
                payload={
                    "workspace_id": str(workspace_id),
                    "document_id": str(uuid.uuid4()),
                },
            ),
        ]
    )

    await store.delete_document(workspace_id=workspace_id, document_id=document_id)

    retained = await client.retrieve("chunks", ids=[str(retained_id)])
    assert len(retained) == 1


@pytest.mark.asyncio
async def test_search_always_filters_by_workspace() -> None:
    client = AsyncQdrantClient(location=":memory:")
    store = QdrantVectorStore(url="http://unused", collection_name="chunks", client=client)
    await store.ensure_collection(dimensions=2)
    workspace_id = uuid.uuid4()
    expected_id = uuid.uuid4()
    await store.upsert(
        [
            VectorRecord(
                id=expected_id,
                vector=[1.0, 0.0],
                payload={
                    "workspace_id": str(workspace_id),
                    "document_id": str(uuid.uuid4()),
                },
            ),
            VectorRecord(
                id=uuid.uuid4(),
                vector=[1.0, 0.0],
                payload={
                    "workspace_id": str(uuid.uuid4()),
                    "document_id": str(uuid.uuid4()),
                },
            ),
        ]
    )

    results = await store.search(
        query_vector=[1.0, 0.0],
        workspace_id=workspace_id,
        document_ids=None,
        limit=10,
    )

    assert [result.id for result in results] == [expected_id]
    assert results[0].score == pytest.approx(1.0)
