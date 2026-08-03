import uuid
from dataclasses import dataclass
from typing import Protocol

PayloadValue = str | int | float | bool | None


@dataclass(frozen=True, slots=True)
class VectorRecord:
    id: uuid.UUID
    vector: list[float]
    payload: dict[str, PayloadValue]


class VectorStore(Protocol):
    async def ensure_collection(self, *, dimensions: int) -> None: ...

    async def upsert(self, records: list[VectorRecord]) -> None: ...

    async def delete_document(self, *, workspace_id: uuid.UUID, document_id: uuid.UUID) -> None: ...

    async def check(self) -> None: ...
