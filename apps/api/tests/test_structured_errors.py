import pytest
from httpx import ASGITransport, AsyncClient

from app.core.errors import ApplicationError
from app.main import create_app


@pytest.mark.asyncio
async def test_correlation_id_propagated_on_validation_error() -> None:
    app = create_app()
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post(
            "/api/v1/workspaces",
            json={},
            headers={
                "X-User-ID": "00000000-0000-0000-0000-000000000001",
                "X-Correlation-ID": "cid-test-1234",
            },
        )
    assert response.status_code == 422
    assert response.headers.get("X-Correlation-ID") == "cid-test-1234"
    body = response.json()
    assert body["error"]["code"] == "VALIDATION_ERROR"
    assert body["error"]["correlation_id"] == "cid-test-1234"


@pytest.mark.asyncio
async def test_unhandled_exception_returns_sanitized_json_without_stack_trace() -> None:
    app = create_app()

    @app.get("/api/v1/_test_boom")
    async def boom() -> None:
        raise RuntimeError("sensitive internal secret traceback detail")

    transport = ASGITransport(app=app, raise_app_exceptions=False)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.get(
            "/api/v1/_test_boom",
            headers={"X-Correlation-ID": "cid-boom-999"},
        )
    assert response.status_code == 500
    body = response.json()
    assert body["error"]["code"] == "INTERNAL_SERVER_ERROR"
    assert body["error"]["correlation_id"] == "cid-boom-999"
    assert "sensitive internal secret" not in body["error"]["message"]


@pytest.mark.asyncio
async def test_application_error_includes_stage_and_correlation_id() -> None:
    app = create_app()

    @app.get("/api/v1/_test_stage_err")
    async def stage_err() -> None:
        raise ApplicationError(
            code="DOCUMENT_PROCESSING_FAILED",
            message="임베딩 단계 실패",
            status_code=500,
            stage="embedding",
            details={"stage": "embedding", "stage_code": "DOCUMENT_EMBEDDING_FAILED"},
        )

    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.get(
            "/api/v1/_test_stage_err",
            headers={"X-Correlation-ID": "cid-stage-1"},
        )
    assert response.status_code == 500
    body = response.json()
    assert body["error"]["stage"] == "embedding"
    assert body["error"]["details"]["stage_code"] == "DOCUMENT_EMBEDDING_FAILED"
    assert body["error"]["correlation_id"] == "cid-stage-1"
    assert response.headers.get("X-Correlation-ID") == "cid-stage-1"
