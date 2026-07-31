from app.storage.protocol import FileStorage


class FileStorageReadinessCheck:
    name = "object_storage"

    def __init__(self, file_storage: FileStorage) -> None:
        self._file_storage = file_storage

    async def check(self) -> None:
        await self._file_storage.check()
