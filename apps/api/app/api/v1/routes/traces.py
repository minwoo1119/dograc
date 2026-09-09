import uuid

from fastapi import APIRouter, status

from app.api.dependencies import CurrentUserId, DatabaseSession
from app.conversations.schemas import TraceResponse
from app.conversations.service import ConversationService
from app.core.errors import ErrorResponse

traces_router = APIRouter()


@traces_router.get(
    "/{trace_id}",
    response_model=TraceResponse,
    responses={status.HTTP_404_NOT_FOUND: {"model": ErrorResponse}},
)
async def get_trace(
    trace_id: uuid.UUID,
    session: DatabaseSession,
    user_id: CurrentUserId,
) -> TraceResponse:
    trace = await ConversationService(session).get_trace(
        trace_id=trace_id,
        owner_id=user_id,
    )
    return TraceResponse.model_validate(trace)
