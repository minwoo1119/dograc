from fastapi import status

from app.core.errors import ApplicationError


class WorkspaceNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="WORKSPACE_NOT_FOUND",
            message="Workspace was not found.",
            status_code=status.HTTP_404_NOT_FOUND,
        )
