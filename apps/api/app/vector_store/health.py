from app.vector_store.protocol import VectorStore


class VectorStoreReadinessCheck:
    name = "qdrant"

    def __init__(self, vector_store: VectorStore) -> None:
        self._vector_store = vector_store

    async def check(self) -> None:
        await self._vector_store.check()
