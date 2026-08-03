import uuid

from fastapi import APIRouter, Request, status

from app.api.dependencies import (
    CurrentUserId,
    DatabaseSession,
    EmbeddingModelDependency,
    VectorStoreDependency,
)
from app.core.errors import ErrorResponse
from app.retrieval.schemas import (
    RetrievalRequest,
    RetrievalResponse,
    RetrievedChunkResponse,
)
from app.retrieval.service import RetrievalService

router = APIRouter()


@router.post(
    "",
    response_model=RetrievalResponse,
    responses={
        status.HTTP_404_NOT_FOUND: {"model": ErrorResponse},
        status.HTTP_503_SERVICE_UNAVAILABLE: {"model": ErrorResponse},
    },
)
async def retrieve_chunks(
    workspace_id: uuid.UUID,
    payload: RetrievalRequest,
    request: Request,
    session: DatabaseSession,
    user_id: CurrentUserId,
    embedding_model: EmbeddingModelDependency,
    vector_store: VectorStoreDependency,
) -> RetrievalResponse:
    chunks = await RetrievalService(
        session=session,
        embedding_model=embedding_model,
        vector_store=vector_store,
    ).retrieve(
        question=payload.question,
        workspace_id=workspace_id,
        owner_id=user_id,
        document_ids=payload.document_ids,
        top_k=payload.top_k or request.app.state.settings.retrieval_top_k,
    )
    return RetrievalResponse(
        normalized_question=payload.question,
        chunks=[RetrievedChunkResponse.model_validate(chunk) for chunk in chunks],
    )
