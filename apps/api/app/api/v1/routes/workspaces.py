import uuid

from fastapi import APIRouter, Response, status

from app.api.dependencies import CurrentUserId, DatabaseSession
from app.core.errors import ErrorResponse
from app.workspaces.schemas import WorkspaceCreate, WorkspaceResponse
from app.workspaces.service import WorkspaceService

router = APIRouter()


@router.post("", response_model=WorkspaceResponse, status_code=status.HTTP_201_CREATED)
async def create_workspace(
    payload: WorkspaceCreate,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> WorkspaceResponse:
    workspace = await WorkspaceService(session).create(owner_id=user_id, name=payload.name)
    return WorkspaceResponse.model_validate(workspace)


@router.get("", response_model=list[WorkspaceResponse])
async def list_workspaces(
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> list[WorkspaceResponse]:
    workspaces = await WorkspaceService(session).list_for_owner(user_id)
    return [WorkspaceResponse.model_validate(workspace) for workspace in workspaces]


@router.get(
    "/{workspace_id}",
    response_model=WorkspaceResponse,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def get_workspace(
    workspace_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> WorkspaceResponse:
    workspace = await WorkspaceService(session).get(
        workspace_id=workspace_id,
        owner_id=user_id,
    )
    return WorkspaceResponse.model_validate(workspace)


@router.delete(
    "/{workspace_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def delete_workspace(
    workspace_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> Response:
    await WorkspaceService(session).delete(
        workspace_id=workspace_id,
        owner_id=user_id,
    )
    return Response(status_code=status.HTTP_204_NO_CONTENT)
