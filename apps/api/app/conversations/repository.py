import uuid

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.db.models.conversation import Conversation, Message, Trace
from app.db.models.workspace import Workspace


class ConversationRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def add(self, conversation: Conversation) -> Conversation:
        self._session.add(conversation)
        await self._session.flush()
        await self._session.refresh(conversation)
        return conversation

    async def list_for_workspace(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> list[Conversation]:
        statement = (
            select(Conversation)
            .join(Workspace, Workspace.id == Conversation.workspace_id)
            .where(
                Conversation.workspace_id == workspace_id,
                Workspace.owner_id == owner_id,
            )
            .order_by(Conversation.created_at.desc())
        )
        return list((await self._session.scalars(statement)).all())

    async def get_for_owner(
        self,
        *,
        conversation_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Conversation | None:
        statement = (
            select(Conversation)
            .join(Workspace, Workspace.id == Conversation.workspace_id)
            .where(
                Conversation.id == conversation_id,
                Workspace.owner_id == owner_id,
            )
            .options(
                selectinload(Conversation.messages).selectinload(Message.trace),
            )
        )
        return await self._session.scalar(statement)

    async def delete(self, conversation: Conversation) -> None:
        await self._session.delete(conversation)
        await self._session.flush()

    async def add_message(self, message: Message) -> Message:
        self._session.add(message)
        await self._session.flush()
        await self._session.refresh(message)
        return message

    async def add_trace(self, trace: Trace) -> Trace:
        self._session.add(trace)
        await self._session.flush()
        await self._session.refresh(trace)
        return trace

    async def get_trace_for_owner(
        self,
        *,
        trace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Trace | None:
        statement = (
            select(Trace)
            .join(Workspace, Workspace.id == Trace.workspace_id)
            .where(
                Trace.id == trace_id,
                Workspace.owner_id == owner_id,
            )
        )
        return await self._session.scalar(statement)
