import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.workspace import Workspace
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
