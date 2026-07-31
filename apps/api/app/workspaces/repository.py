import uuid

from sqlalchemy import Select, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.models.workspace import Workspace


class WorkspaceRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def add(self, workspace: Workspace) -> Workspace:
        self._session.add(workspace)
        await self._session.flush()
        await self._session.refresh(workspace)
        return workspace

    async def list_for_owner(self, owner_id: uuid.UUID) -> list[Workspace]:
        statement: Select[tuple[Workspace]] = (
            select(Workspace)
            .where(Workspace.owner_id == owner_id)
            .order_by(Workspace.created_at.desc(), Workspace.id.desc())
        )
        result = await self._session.scalars(statement)
        return list(result)

    async def get_for_owner(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Workspace | None:
        statement = select(Workspace).where(
            Workspace.id == workspace_id,
            Workspace.owner_id == owner_id,
        )
        return await self._session.scalar(statement)

    async def delete(self, workspace: Workspace) -> None:
        await self._session.delete(workspace)
