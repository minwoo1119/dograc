from dataclasses import dataclass

from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import create_app


@dataclass(frozen=True)
class StubReadinessCheck:
    name: str
    error: Exception | None = None

    async def check(self) -> None:
        if self.error is not None:
            raise self.error


def test_liveness_returns_ok() -> None:
    with TestClient(create_app(settings=Settings())) as client:
        response = client.get("/api/v1/health/live")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "checks": []}


def test_readiness_returns_dependency_results() -> None:
    checks = (
        StubReadinessCheck(name="postgres"),
        StubReadinessCheck(name="qdrant"),
    )
    with TestClient(create_app(settings=Settings(), readiness_checks=checks)) as client:
        response = client.get("/api/v1/health/ready")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "checks": [
            {"name": "postgres", "healthy": True, "detail": None},
            {"name": "qdrant", "healthy": True, "detail": None},
        ],
    }


def test_readiness_returns_503_without_leaking_error_details() -> None:
    checks = (StubReadinessCheck(name="postgres", error=ConnectionError("contains a secret")),)
    with TestClient(create_app(settings=Settings(), readiness_checks=checks)) as client:
        response = client.get("/api/v1/health/ready")

    assert response.status_code == 503
    assert response.json() == {
        "status": "unavailable",
        "checks": [
            {"name": "postgres", "healthy": False, "detail": "ConnectionError"},
        ],
    }
    assert "contains a secret" not in response.text
