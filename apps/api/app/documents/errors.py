from fastapi import status

from app.core.errors import ApplicationError
from app.documents.validation import DocumentValidationError


class DocumentUploadValidationError(ApplicationError):
    def __init__(self, error: DocumentValidationError) -> None:
        status_code = {
            "FILE_TOO_LARGE": status.HTTP_413_CONTENT_TOO_LARGE,
            "UNSUPPORTED_FILE_TYPE": status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
        }.get(error.code, status.HTTP_400_BAD_REQUEST)
        super().__init__(
            code=error.code,
            message=error.message,
            status_code=status_code,
        )


class DocumentProcessingError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="DOCUMENT_PROCESSING_FAILED",
            message="The document could not be stored.",
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        )


class DocumentNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="DOCUMENT_NOT_FOUND",
            message="Document was not found.",
            status_code=status.HTTP_404_NOT_FOUND,
        )


class DocumentParseFailedError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="DOCUMENT_PARSE_FAILED",
            message="The document could not be parsed.",
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
        )
