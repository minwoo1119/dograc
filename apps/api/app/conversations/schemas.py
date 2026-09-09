import uuid
from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field

from app.db.models.conversation import MessageRole


class ConversationCreate(BaseModel):
    title: str | None = Field(default=None, max_length=255)


class MessageCreate(BaseModel):
    content: str = Field(min_length=1)
    model_name: str | None = None
    endpoint_url: str | None = None


class MessageResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    conversation_id: uuid.UUID
    role: MessageRole
    content: str
    created_at: datetime
    trace_id: uuid.UUID | None = None


class ConversationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    workspace_id: uuid.UUID
    title: str
    created_at: datetime
    updated_at: datetime


class ConversationDetailResponse(ConversationResponse):
    messages: list[MessageResponse] = Field(default_factory=list)


class TraceResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    message_id: uuid.UUID
    workspace_id: uuid.UUID
    retrieved_chunks: list[dict[str, Any]]
    prompt: str | None = None
    model_name: str | None = None
    latency_ms: int | None = None
    created_at: datetime
