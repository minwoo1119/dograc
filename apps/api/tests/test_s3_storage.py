from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

import pytest
from botocore.exceptions import ClientError

from app.storage.s3 import S3FileStorage, S3StorageError


class RecordingS3Client:
    def __init__(self) -> None:
        self.calls: list[tuple[str, dict[str, object]]] = []
        self.error: ClientError | None = None

    async def put_object(self, **kwargs: object) -> object:
        self.calls.append(("put_object", kwargs))
        if self.error:
            raise self.error
        return {}

    async def delete_object(self, **kwargs: object) -> object:
        self.calls.append(("delete_object", kwargs))
        if self.error:
            raise self.error
        return {}

    async def head_bucket(self, **kwargs: object) -> object:
        self.calls.append(("head_bucket", kwargs))
        if self.error:
            raise self.error
        return {}


class RecordingSession:
    def __init__(self, client: RecordingS3Client) -> None:
        self.s3_client = client
        self.client_kwargs: dict[str, object] = {}

    @asynccontextmanager
    async def client(
        self,
        service_name: str,
        **kwargs: object,
    ) -> AsyncIterator[RecordingS3Client]:
        self.client_kwargs = {"service_name": service_name, **kwargs}
        yield self.s3_client


def create_storage(client: RecordingS3Client) -> S3FileStorage:
    return S3FileStorage(
        endpoint_url="http://minio:9000",
        access_key="access",
        secret_key="secret",
        bucket="documents",
        region="us-east-1",
        session=RecordingSession(client),
    )


@pytest.mark.asyncio
async def test_put_passes_content_and_metadata_to_s3() -> None:
    client = RecordingS3Client()
    storage = create_storage(client)

    await storage.put(
        object_key="workspaces/one/document.pdf",
        content=b"%PDF-1.7",
        media_type="application/pdf",
    )

    assert client.calls == [
        (
            "put_object",
            {
                "Bucket": "documents",
                "Key": "workspaces/one/document.pdf",
                "Body": b"%PDF-1.7",
                "ContentType": "application/pdf",
            },
        )
    ]


@pytest.mark.asyncio
async def test_adapter_hides_provider_error_details() -> None:
    client = RecordingS3Client()
    client.error = ClientError(
        {"Error": {"Code": "AccessDenied", "Message": "contains secret details"}},
        "PutObject",
    )

    with pytest.raises(S3StorageError, match="failed to store object") as raised:
        await create_storage(client).put(
            object_key="object",
            content=b"content",
            media_type="text/plain",
        )

    assert "contains secret details" not in str(raised.value)
