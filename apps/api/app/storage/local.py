import asyncio
from pathlib import Path

from app.storage.protocol import FileStorage, FileStorageError


class LocalFileStorage(FileStorage):
    """Local filesystem implementation of FileStorage for standalone / offline local use."""

    def __init__(self, base_dir: Path | str = "./data/storage") -> None:
        self._base_dir = Path(base_dir).resolve()
        self._base_dir.mkdir(parents=True, exist_ok=True)

    def _resolve_path(self, object_key: str) -> Path:
        target = (self._base_dir / object_key).resolve()
        if not target.is_relative_to(self._base_dir):
            raise FileStorageError("Path traversal detected")
        return target

    async def get(self, *, object_key: str) -> bytes:
        path = self._resolve_path(object_key)
        if not path.exists():
            raise FileStorageError(f"File not found: {object_key}")
        try:
            return await asyncio.to_thread(path.read_bytes)
        except OSError as e:
            raise FileStorageError(f"Failed to read file: {e}") from e

    async def put(
        self,
        *,
        object_key: str,
        content: bytes,
        media_type: str,
    ) -> None:
        path = self._resolve_path(object_key)
        try:
            await asyncio.to_thread(path.parent.mkdir, parents=True, exist_ok=True)
            await asyncio.to_thread(path.write_bytes, content)
        except OSError as e:
            raise FileStorageError(f"Failed to write file: {e}") from e

    async def delete(self, *, object_key: str) -> None:
        path = self._resolve_path(object_key)
        if path.exists():
            try:
                await asyncio.to_thread(path.unlink)
            except OSError as e:
                raise FileStorageError(f"Failed to delete file: {e}") from e

    async def check(self) -> None:
        try:
            self._base_dir.mkdir(parents=True, exist_ok=True)
            test_file = self._base_dir / ".health_check"
            await asyncio.to_thread(test_file.write_text, "ok")
            await asyncio.to_thread(test_file.unlink)
        except OSError as e:
            raise FileStorageError(f"Storage health check failed: {e}") from e
