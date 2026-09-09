import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.conversations.errors import ConversationNotFoundError, TraceNotFoundError
from app.conversations.repository import ConversationRepository
from app.db.models.conversation import Conversation, Trace
from app.workspaces.service import WorkspaceService


class ConversationService:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session
        self._repository = ConversationRepository(session)

    async def create(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
        title: str | None = None,
    ) -> Conversation:
        await WorkspaceService(self._session).get(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )
        resolved_title = (title or "").strip() or "새 대화"
        conversation = Conversation(
            workspace_id=workspace_id,
            title=resolved_title,
        )
        await self._repository.add(conversation)
        await self._session.commit()
        return conversation

    async def list_for_workspace(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> list[Conversation]:
        await WorkspaceService(self._session).get(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )
        return await self._repository.list_for_workspace(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )

    async def get(
        self,
        *,
        conversation_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Conversation:
        conversation = await self._repository.get_for_owner(
            conversation_id=conversation_id,
            owner_id=owner_id,
        )
        if conversation is None:
            raise ConversationNotFoundError
        return conversation

    async def delete(
        self,
        *,
        conversation_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> None:
        conversation = await self.get(
            conversation_id=conversation_id,
            owner_id=owner_id,
        )
        await self._repository.delete(conversation)
        await self._session.commit()

    async def get_trace(
        self,
        *,
        trace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Trace:
        trace = await self._repository.get_trace_for_owner(
            trace_id=trace_id,
            owner_id=owner_id,
        )
        if trace is None:
            raise TraceNotFoundError
        return trace
