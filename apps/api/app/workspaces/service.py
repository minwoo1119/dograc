import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.workspace import Workspace
from app.workspaces.errors import WorkspaceNotFoundError
from app.workspaces.repository import WorkspaceRepository


class WorkspaceService:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session
        self._repository = WorkspaceRepository(session)

    async def create(self, *, owner_id: uuid.UUID, name: str) -> Workspace:
        workspace = Workspace(owner_id=owner_id, name=name)
        await self._repository.add(workspace)
        await self._session.commit()
        return workspace

    async def list_for_owner(self, owner_id: uuid.UUID) -> list[Workspace]:
        return await self._repository.list_for_owner(owner_id)

    async def get(self, *, workspace_id: uuid.UUID, owner_id: uuid.UUID) -> Workspace:
        workspace = await self._repository.get_for_owner(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )
        if workspace is None:
            raise WorkspaceNotFoundError
        return workspace

    async def delete(self, *, workspace_id: uuid.UUID, owner_id: uuid.UUID) -> None:
        workspace = await self.get(workspace_id=workspace_id, owner_id=owner_id)
        await self._repository.delete(workspace)
        await self._session.commit()
