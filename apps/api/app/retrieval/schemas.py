import uuid

from pydantic import BaseModel, ConfigDict, Field, field_validator


class RetrievalRequest(BaseModel):
    question: str = Field(min_length=1, max_length=2000)
    top_k: int | None = Field(default=None, ge=1, le=50)
    document_ids: list[uuid.UUID] | None = None

    @field_validator("question")
    @classmethod
    def normalize_question(cls, value: str) -> str:
        normalized = " ".join(value.split())
        if not normalized:
            raise ValueError("question must not be blank")
        return normalized


class RetrievedChunkResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    chunk_id: uuid.UUID
    document_id: uuid.UUID
    document_version_id: uuid.UUID
    source_file_name: str
    page_number: int
    section_title: str | None
    chunk_index: int
    text: str
    score: float


class RetrievalResponse(BaseModel):
    normalized_question: str
    chunks: list[RetrievedChunkResponse]
