from fastapi import APIRouter, Request, status
from fastapi.responses import JSONResponse

from app.health.checks import run_readiness_checks
from app.health.schemas import HealthResponse

router = APIRouter()


@router.get("/live", response_model=HealthResponse)
async def liveness() -> HealthResponse:
    return HealthResponse(status="ok")


@router.get(
    "/ready",
    response_model=HealthResponse,
    responses={status.HTTP_503_SERVICE_UNAVAILABLE: {"model": HealthResponse}},
)
async def readiness(request: Request) -> HealthResponse | JSONResponse:
    checks = await run_readiness_checks(request.app.state.readiness_checks)
    is_ready = all(check.healthy for check in checks)
    response = HealthResponse(status="ok" if is_ready else "unavailable", checks=checks)
    if is_ready:
        return response
    return JSONResponse(
        status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
        content=response.model_dump(mode="json"),
    )
