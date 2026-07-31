from fastapi import Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel


class ErrorDetail(BaseModel):
    code: str
    message: str


class ErrorResponse(BaseModel):
    error: ErrorDetail


class ApplicationError(Exception):
    def __init__(self, *, code: str, message: str, status_code: int) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code


async def application_error_handler(
    _request: Request,
    error: ApplicationError,
) -> JSONResponse:
    response = ErrorResponse(
        error=ErrorDetail(code=error.code, message=error.message),
    )
    return JSONResponse(
        status_code=error.status_code,
        content=response.model_dump(mode="json"),
    )
