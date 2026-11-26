from typing import Any

from fastapi import status

from app.core.errors import ApplicationError


class ConversationNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="CONVERSATION_NOT_FOUND",
            message="Conversation was not found.",
            status_code=status.HTTP_404_NOT_FOUND,
        )


class TraceNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="TRACE_NOT_FOUND",
            message="Trace was not found.",
            status_code=status.HTTP_404_NOT_FOUND,
        )


class GenerationFailedError(ApplicationError):
    def __init__(
        self,
        message: str = "Failed to generate completion from LLM.",
        *,
        details: Any | None = None,
    ) -> None:
        super().__init__(
            code="GENERATION_FAILED",
            message=message,
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            stage="generation",
            details=details,
        )
