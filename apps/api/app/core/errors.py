from typing import Any

from fastapi import HTTPException, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel


class ErrorDetail(BaseModel):
    code: str
    message: str
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
        details: Any | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code
        self.details = details


async def application_error_handler(
    _request: Request,
    error: ApplicationError,
) -> JSONResponse:
    content: dict[str, Any] = {
        "error": {
            "code": error.code,
            "message": error.message,
        }
    }
    if error.details is not None:
        content["error"]["details"] = error.details

    return JSONResponse(
        status_code=error.status_code,
        content=content,
    )


async def http_exception_handler(
    _request: Request,
    exc: HTTPException,
) -> JSONResponse:
    code = f"HTTP_{exc.status_code}"
    message = str(exc.detail) if exc.detail else "요청 처리 중 오류가 발생했습니다."
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": {
                "code": code,
                "message": message,
            }
        },
    )


async def validation_exception_handler(
    _request: Request,
    exc: RequestValidationError,
) -> JSONResponse:
    errors = exc.errors()
    first_msg = errors[0].get("msg", "입력 데이터가 올바르지 않습니다.") if errors else "입력값 검증에 실패했습니다."
    field_loc = " -> ".join(str(loc) for loc in errors[0].get("loc", [])) if errors else ""
    friendly_msg = f"{field_loc}: {first_msg}" if field_loc else first_msg

    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content={
            "error": {
                "code": "VALIDATION_ERROR",
                "message": friendly_msg,
                "details": errors,
            }
        },
    )


async def validation_exception_handler(
    _request: Request,
    exc: RequestValidationError,
) -> JSONResponse:
    errors = exc.errors()
    first_msg = errors[0].get("msg", "입력 데이터가 올바르지 않습니다.") if errors else "입력값 검증에 실패했습니다."
    field_loc = " -> ".join(str(loc) for loc in errors[0].get("loc", [])) if errors else ""
    friendly_msg = f"{field_loc}: {first_msg}" if field_loc else first_msg

    response = ErrorResponse(
        error=ErrorDetail(
            code="VALIDATION_ERROR",
            message=friendly_msg,
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            details=errors,
        ),
    )
    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content=response.model_dump(mode="json"),
    )
