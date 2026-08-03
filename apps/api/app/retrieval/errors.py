from fastapi import status

from app.core.errors import ApplicationError


class RetrievalFailedError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="RETRIEVAL_FAILED",
            message="Relevant document passages could not be retrieved.",
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
        )
