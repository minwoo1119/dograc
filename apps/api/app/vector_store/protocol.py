import uuid
from dataclasses import dataclass
from typing import Protocol

PayloadValue = str | int | float | bool | None


class VectorStoreError(OSError):
    """Raised when a vector storage operation fails."""


@dataclass(frozen=True, slots=True)
class VectorRecord:
    id: uuid.UUID
    vector: list[float]
    payload: dict[str, PayloadValue]


@dataclass(frozen=True, slots=True)
class VectorSearchResult:
    id: uuid.UUID
    score: float
    payload: dict[str, PayloadValue]


class VectorStore(Protocol):
    async def ensure_collection(self, *, dimensions: int) -> None: ...

    async def upsert(self, records: list[VectorRecord]) -> None: ...

    async def search(
        self,
        *,
        query_vector: list[float],
        workspace_id: uuid.UUID,
        document_ids: list[uuid.UUID] | None,
        limit: int,
    ) -> list[VectorSearchResult]: ...

    async def delete_document(self, *, workspace_id: uuid.UUID, document_id: uuid.UUID) -> None: ...

    async def check(self) -> None: ...
