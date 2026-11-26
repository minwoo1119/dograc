from typing import Any

from fastapi import status

from app.core.errors import ApplicationError
from app.documents.validation import DocumentValidationError


class DocumentUploadValidationError(ApplicationError):
    def __init__(self, error: DocumentValidationError) -> None:
        status_code = {
            "FILE_TOO_LARGE": status.HTTP_413_CONTENT_TOO_LARGE,
            "UNSUPPORTED_FILE_TYPE": status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            "DOCUMENT_ENCODING_ERROR": status.HTTP_422_UNPROCESSABLE_CONTENT,
        }.get(error.code, status.HTTP_400_BAD_REQUEST)
        super().__init__(
            code=error.code,
            message=error.message,
            status_code=status_code,
            stage="upload",
        )


class DocumentProcessingError(ApplicationError):
    def __init__(
        self,
        message: str = "문서를 저장하거나 색인하는 과정에서 오류가 발생했습니다.",
        *,
        stage: str | None = None,
        details: Any | None = None,
    ) -> None:
        super().__init__(
            code="DOCUMENT_PROCESSING_FAILED",
            message=message,
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            stage=stage,
            details=details,
        )


class DocumentNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="DOCUMENT_NOT_FOUND",
            message="Document was not found.",
            status_code=status.HTTP_404_NOT_FOUND,
        )


class DocumentAlreadyProcessingError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="DOCUMENT_ALREADY_PROCESSING",
            message="이미 색인 처리가 진행 중인 문서입니다. 완료될 때까지 잠시 기다려 주세요.",
            status_code=status.HTTP_409_CONFLICT,
            stage="processing",
        )


class DocumentParseFailedError(ApplicationError):
    def __init__(
        self,
        message: str = "The document could not be parsed.",
        *,
        stage: str = "text_extraction",
        details: Any | None = None,
    ) -> None:
        super().__init__(
            code="DOCUMENT_PARSE_FAILED",
            message=message,
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            stage=stage,
            details=details,
        )
