import uuid

from qdrant_client import AsyncQdrantClient, models
from qdrant_client.http.exceptions import UnexpectedResponse

from app.vector_store.protocol import VectorRecord, VectorSearchResult, VectorStoreError


class QdrantVectorStoreError(VectorStoreError):
    """Raised when a Qdrant operation fails."""


class QdrantVectorStore:
    def __init__(
        self,
        *,
        url: str,
        collection_name: str,
        api_key: str | None = None,
        client: AsyncQdrantClient | None = None,
    ) -> None:
        self._collection_name = collection_name
        self._client = client or AsyncQdrantClient(url=url, api_key=api_key)

    async def ensure_collection(self, *, dimensions: int) -> None:
        try:
            if await self._client.collection_exists(self._collection_name):
                return
            await self._client.create_collection(
                collection_name=self._collection_name,
                vectors_config=models.VectorParams(
                    size=dimensions,
                    distance=models.Distance.COSINE,
                ),
            )
        except (UnexpectedResponse, OSError, ValueError) as exc:
            raise QdrantVectorStoreError("failed to ensure vector collection") from exc

    async def upsert(self, records: list[VectorRecord]) -> None:
        if not records:
            return
        try:
            await self._client.upsert(
                collection_name=self._collection_name,
                points=[
                    models.PointStruct(
                        id=str(record.id),
                        vector=record.vector,
                        payload=record.payload,
                    )
                    for record in records
                ],
                wait=True,
            )
        except (UnexpectedResponse, OSError, ValueError) as exc:
            raise QdrantVectorStoreError("failed to upsert vectors") from exc

    async def delete_document(
        self,
        *,
        workspace_id: uuid.UUID,
        document_id: uuid.UUID,
    ) -> None:
        try:
            await self._client.delete(
                collection_name=self._collection_name,
                points_selector=models.FilterSelector(
                    filter=models.Filter(
                        must=[
                            models.FieldCondition(
                                key="workspace_id",
                                match=models.MatchValue(value=str(workspace_id)),
                            ),
                            models.FieldCondition(
                                key="document_id",
                                match=models.MatchValue(value=str(document_id)),
                            ),
                        ]
                    )
                ),
                wait=True,
            )
        except (UnexpectedResponse, OSError, ValueError) as exc:
            raise QdrantVectorStoreError("failed to delete document vectors") from exc

    async def search(
        self,
        *,
        query_vector: list[float],
        workspace_id: uuid.UUID,
        document_ids: list[uuid.UUID] | None,
        limit: int,
    ) -> list[VectorSearchResult]:
        conditions = [
            models.FieldCondition(
                key="workspace_id",
                match=models.MatchValue(value=str(workspace_id)),
            )
        ]
        if document_ids:
            conditions.append(
                models.FieldCondition(
                    key="document_id",
                    match=models.MatchAny(any=[str(document_id) for document_id in document_ids]),
                )
            )
        try:
            response = await self._client.query_points(
                collection_name=self._collection_name,
                query=query_vector,
                query_filter=models.Filter(must=conditions),
                limit=limit,
                with_payload=True,
                with_vectors=False,
            )
        except (UnexpectedResponse, OSError, ValueError) as exc:
            raise QdrantVectorStoreError("failed to search vectors") from exc
        return [
            VectorSearchResult(
                id=uuid.UUID(str(point.id)),
                score=point.score,
                payload=dict(point.payload or {}),
            )
            for point in response.points
        ]

    async def check(self) -> None:
        try:
            await self._client.get_collections()
        except (UnexpectedResponse, OSError, ValueError) as exc:
            raise QdrantVectorStoreError("vector store is unavailable") from exc
