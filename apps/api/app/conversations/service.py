import time
import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.conversations.errors import (
    ConversationNotFoundError,
    GenerationFailedError,
    TraceNotFoundError,
)
from app.conversations.repository import ConversationRepository
from app.db.models.conversation import Conversation, Message, MessageRole, Trace
from app.models.embedding import EmbeddingModel
from app.models.generation import GenerationModel, GenerationModelError
from app.retrieval.service import RetrievalService, RetrievedChunk
from app.vector_store.protocol import VectorStore
from app.workspaces.service import WorkspaceService

SYSTEM_PROMPT = (
    "당신은 주어진 문서 문맥(Context)에만 근거하여 질문에 답변하는 "
    "신뢰할 수 있는 RAG 어시스턴트입니다.\n"
    "규칙:\n"
    "1. 반드시 제공된 [문서 문맥]에 있는 내용만을 기반으로 답변하세요.\n"
    "2. 문맥에 없는 내용이나 유추할 수 없는 내용은 추측하지 말고 반드시 "
    "'문서에서 관련 내용을 확인할 수 없습니다.'라고 답변하세요.\n"
    "3. 답변 시 참고한 내용마다 [파일명, p.페이지] 형식으로 근거를 명시하세요."
)


def build_rag_prompt(question: str, chunks: list[RetrievedChunk]) -> str:
    if not chunks:
        return (
            "참고할 수 있는 문서 문맥이 없습니다.\n\n"
            f"질문: {question}\n\n"
            "답변: 문서에서 관련 내용을 확인할 수 없습니다."
        )
    context_sections = []
    for i, chunk in enumerate(chunks, 1):
        context_sections.append(
            f"[문서 {i}] 파일: {chunk.source_file_name} (페이지 {chunk.page_number}):\n{chunk.text}"
        )
    joined_context = "\n\n".join(context_sections)
    return (
        f"다음 제공된 [문서 문맥]을 주의 깊게 읽고 질문에 답변하세요.\n\n"
        f"[문서 문맥]\n{joined_context}\n\n"
        f"질문: {question}\n\n"
        f"답변:"
    )


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

    async def send_message(
        self,
        *,
        conversation_id: uuid.UUID,
        owner_id: uuid.UUID,
        content: str,
        embedding_model: EmbeddingModel,
        vector_store: VectorStore,
        generation_model: GenerationModel,
        top_k: int = 5,
        document_ids: list[uuid.UUID] | None = None,
    ) -> tuple[Message, Trace]:
        conversation = await self.get(
            conversation_id=conversation_id,
            owner_id=owner_id,
        )

        user_message = Message(
            conversation_id=conversation.id,
            role=MessageRole.USER,
            content=content,
        )
        await self._repository.add_message(user_message)

        retrieval_service = RetrievalService(
            session=self._session,
            embedding_model=embedding_model,
            vector_store=vector_store,
        )
        chunks = await retrieval_service.retrieve(
            question=content,
            workspace_id=conversation.workspace_id,
            owner_id=owner_id,
            document_ids=document_ids,
            top_k=top_k,
        )

        prompt = build_rag_prompt(content, chunks)

        start = time.perf_counter()
        try:
            answer = await generation_model.generate(
                prompt=prompt,
                system_prompt=SYSTEM_PROMPT,
            )
        except GenerationModelError as exc:
            raise GenerationFailedError from exc
        latency_ms = int((time.perf_counter() - start) * 1000)

        assistant_message = Message(
            conversation_id=conversation.id,
            role=MessageRole.ASSISTANT,
            content=answer,
        )
        await self._repository.add_message(assistant_message)

        trace = Trace(
            message_id=assistant_message.id,
            workspace_id=conversation.workspace_id,
            retrieved_chunks=[
                {
                    "chunk_id": str(c.chunk_id),
                    "document_id": str(c.document_id),
                    "document_version_id": str(c.document_version_id),
                    "source_file_name": c.source_file_name,
                    "page_number": c.page_number,
                    "section_title": c.section_title,
                    "chunk_index": c.chunk_index,
                    "text": c.text,
                    "score": c.score,
                }
                for c in chunks
            ],
            prompt=prompt,
            model_name=generation_model.model_name,
            latency_ms=latency_ms,
        )
        await self._repository.add_trace(trace)
        await self._session.commit()
        await self._session.refresh(assistant_message)
        return assistant_message, trace
