from contextlib import AbstractAsyncContextManager
from typing import Protocol

import aioboto3
from botocore.exceptions import BotoCoreError, ClientError


class S3StorageError(Exception):
    """Raised when an S3-compatible storage operation fails."""


class S3Client(Protocol):
    async def put_object(self, **kwargs: object) -> object: ...

    async def delete_object(self, **kwargs: object) -> object: ...

    async def head_bucket(self, **kwargs: object) -> object: ...


class S3Session(Protocol):
    def client(
        self,
        service_name: str,
        **kwargs: object,
    ) -> AbstractAsyncContextManager[S3Client]: ...


class S3FileStorage:
    def __init__(
        self,
        *,
        endpoint_url: str,
        access_key: str,
        secret_key: str,
        bucket: str,
        region: str,
        session: S3Session | None = None,
    ) -> None:
        self._endpoint_url = endpoint_url
        self._access_key = access_key
        self._secret_key = secret_key
        self._bucket = bucket
        self._region = region
        self._session = session or aioboto3.Session()

    def _client(self) -> AbstractAsyncContextManager[S3Client]:
        return self._session.client(
            "s3",
            endpoint_url=self._endpoint_url,
            aws_access_key_id=self._access_key,
            aws_secret_access_key=self._secret_key,
            region_name=self._region,
        )

    async def put(
        self,
        *,
        object_key: str,
        content: bytes,
        media_type: str,
    ) -> None:
        try:
            async with self._client() as client:
                await client.put_object(
                    Bucket=self._bucket,
                    Key=object_key,
                    Body=content,
                    ContentType=media_type,
                )
        except (BotoCoreError, ClientError, OSError) as exc:
            raise S3StorageError("failed to store object") from exc

    async def delete(self, *, object_key: str) -> None:
        try:
            async with self._client() as client:
                await client.delete_object(Bucket=self._bucket, Key=object_key)
        except (BotoCoreError, ClientError, OSError) as exc:
            raise S3StorageError("failed to delete object") from exc

    async def check(self) -> None:
        try:
            async with self._client() as client:
                await client.head_bucket(Bucket=self._bucket)
        except (BotoCoreError, ClientError, OSError) as exc:
            raise S3StorageError("object storage is unavailable") from exc
