import uuid

from fastapi import APIRouter, Request, Response, status

from app.api.dependencies import (
    CurrentUserId,
    DatabaseSession,
    EmbeddingModelDependency,
    GenerationModelDependency,
    VectorStoreDependency,
)
from app.conversations.schemas import (
    ConversationCreate,
    ConversationDetailResponse,
    ConversationResponse,
    MessageCreate,
    MessageResponse,
)
from app.conversations.service import ConversationService
from app.core.errors import ErrorResponse

workspace_conversations_router = APIRouter()
conversations_router = APIRouter()


@workspace_conversations_router.post(
    "",
    response_model=ConversationResponse,
    status_code=status.HTTP_201_CREATED,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def create_conversation(
    workspace_id: uuid.UUID,
    payload: ConversationCreate,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> ConversationResponse:
    conversation = await ConversationService(session).create(
        workspace_id=workspace_id,
        owner_id=user_id,
        title=payload.title,
    )
    return ConversationResponse.model_validate(conversation)


@workspace_conversations_router.get(
    "",
    response_model=list[ConversationResponse],
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def list_conversations(
    workspace_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> list[ConversationResponse]:
    conversations = await ConversationService(session).list_for_workspace(
        workspace_id=workspace_id,
        owner_id=user_id,
    )
    return [ConversationResponse.model_validate(c) for c in conversations]


@conversations_router.get(
    "/{conversation_id}",
    response_model=ConversationDetailResponse,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def get_conversation(
    conversation_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> ConversationDetailResponse:
    conversation = await ConversationService(session).get(
        conversation_id=conversation_id,
        owner_id=user_id,
    )
    messages = [
        MessageResponse(
            id=m.id,
            conversation_id=m.conversation_id,
            role=m.role,
            content=m.content,
            created_at=m.created_at,
            trace_id=m.trace.id if m.trace is not None else None,
        )
        for m in conversation.messages
    ]
    return ConversationDetailResponse(
        id=conversation.id,
        workspace_id=conversation.workspace_id,
        title=conversation.title,
        created_at=conversation.created_at,
        updated_at=conversation.updated_at,
        messages=messages,
    )


@conversations_router.delete(
    "/{conversation_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def delete_conversation(
    conversation_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> Response:
    await ConversationService(session).delete(
        conversation_id=conversation_id,
        owner_id=user_id,
    )
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@conversations_router.post(
    "/{conversation_id}/messages",
    response_model=MessageResponse,
    status_code=status.HTTP_201_CREATED,
    responses={
        status.HTTP_404_NOT_FOUND: {"model": ErrorResponse},
        status.HTTP_503_SERVICE_UNAVAILABLE: {"model": ErrorResponse},
    },
)
async def send_message(
    conversation_id: uuid.UUID,
    payload: MessageCreate,
    request: Request,
    session: DatabaseSession,
    user_id: CurrentUserId,
    embedding_model: EmbeddingModelDependency,
    vector_store: VectorStoreDependency,
    generation_model: GenerationModelDependency,
) -> MessageResponse:
    assistant_message, trace = await ConversationService(session).send_message(
        conversation_id=conversation_id,
        owner_id=user_id,
        content=payload.content,
        embedding_model=embedding_model,
        vector_store=vector_store,
        generation_model=generation_model,
        top_k=request.app.state.settings.max_context_chunks,
    )
    return MessageResponse(
        id=assistant_message.id,
        conversation_id=assistant_message.conversation_id,
        role=assistant_message.role,
        content=assistant_message.content,
        created_at=assistant_message.created_at,
        trace_id=trace.id,
    )
