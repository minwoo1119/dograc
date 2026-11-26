import logging
import uuid
from typing import Any

from fastapi import HTTPException, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel

logger = logging.getLogger("dograc.errors")


class ErrorDetail(BaseModel):
    code: str
    message: str
    correlation_id: str | None = None
    stage: str | None = None
    details: Any | None = None


class ErrorResponse(BaseModel):
    error: ErrorDetail


class ApplicationError(Exception):
    def __init__(
        self,
        *,
        code: str,
        message: str,
        status_code: int,
        stage: str | None = None,
        details: Any | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code
        self.stage = stage
        self.details = details


def resolve_correlation_id(request: Request) -> str:
    existing = getattr(request.state, "correlation_id", None)
    if isinstance(existing, str) and existing.strip():
        return existing.strip()
    header_val = request.headers.get("X-Correlation-ID", "").strip()
    if header_val:
        return header_val[:64]
    return uuid.uuid4().hex[:12]


async def application_error_handler(
    request: Request,
    error: ApplicationError,
) -> JSONResponse:
    correlation_id = resolve_correlation_id(request)
    error_body: dict[str, Any] = {
        "code": error.code,
        "message": error.message,
    }
    if request.headers.get("X-Correlation-ID"):
        error_body["correlation_id"] = correlation_id
    if error.stage is not None:
        error_body["stage"] = error.stage
    if error.details is not None:
        error_body["details"] = error.details

    return JSONResponse(
        status_code=error.status_code,
        content={"error": error_body},
        headers={"X-Correlation-ID": correlation_id},
    )


async def http_exception_handler(
    request: Request,
    exc: HTTPException,
) -> JSONResponse:
    correlation_id = resolve_correlation_id(request)
    code = f"HTTP_{exc.status_code}"
    message = str(exc.detail) if exc.detail else "요청 처리 중 오류가 발생했습니다."
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": {
                "code": code,
                "message": message,
                "correlation_id": correlation_id,
            }
        },
        headers={"X-Correlation-ID": correlation_id},
    )


async def validation_exception_handler(
    request: Request,
    exc: RequestValidationError,
) -> JSONResponse:
    correlation_id = resolve_correlation_id(request)
    errors = exc.errors()
    first_msg = errors[0].get("msg", "입력 데이터가 올바르지 않습니다.") if errors else "입력값 검증에 실패했습니다."
    field_loc = " -> ".join(str(loc) for loc in errors[0].get("loc", [])) if errors else ""
    friendly_msg = f"{field_loc}: {first_msg}" if field_loc else first_msg

    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
        content={
            "error": {
                "code": "VALIDATION_ERROR",
                "message": friendly_msg,
                "correlation_id": correlation_id,
                "details": errors,
            }
        },
        headers={"X-Correlation-ID": correlation_id},
    )


async def unhandled_exception_handler(
    request: Request,
    exc: Exception,
) -> JSONResponse:
    correlation_id = resolve_correlation_id(request)
    logger.exception(
        "Unhandled server exception [correlation_id=%s path=%s]",
        correlation_id,
        request.url.path,
        exc_info=exc,
    )
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={
            "error": {
                "code": "INTERNAL_SERVER_ERROR",
                "message": "서버 내부에서 예상하지 못한 오류가 발생했습니다. 로그 및 추적 ID를 확인해 주세요.",
                "correlation_id": correlation_id,
            }
        },
        headers={"X-Correlation-ID": correlation_id},
    )
