from typing import Protocol


class FileStorageError(OSError):
    """Raised when a file storage operation fails."""


class FileStorage(Protocol):
    async def put(
        self,
        *,
        object_key: str,
        content: bytes,
        media_type: str,
    ) -> None: ...

    async def delete(self, *, object_key: str) -> None: ...

    async def check(self) -> None: ...
