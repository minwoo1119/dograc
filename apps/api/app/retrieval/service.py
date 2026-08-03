import uuid
from dataclasses import dataclass

from sqlalchemy.ext.asyncio import AsyncSession

from app.documents.repository import DocumentRepository
from app.models.embedding import EmbeddingModel, EmbeddingModelError
from app.retrieval.errors import RetrievalFailedError
from app.vector_store.protocol import VectorStore, VectorStoreError
from app.workspaces.service import WorkspaceService


@dataclass(frozen=True, slots=True)
class RetrievedChunk:
    chunk_id: uuid.UUID
    document_id: uuid.UUID
    document_version_id: uuid.UUID
    source_file_name: str
    page_number: int
    section_title: str | None
    chunk_index: int
    text: str
    score: float


class RetrievalService:
    def __init__(
        self,
        *,
        session: AsyncSession,
        embedding_model: EmbeddingModel,
        vector_store: VectorStore,
    ) -> None:
        self._session = session
        self._embedding_model = embedding_model
        self._vector_store = vector_store
        self._documents = DocumentRepository(session)

    async def retrieve(
        self,
        *,
        question: str,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
        document_ids: list[uuid.UUID] | None,
        top_k: int,
    ) -> list[RetrievedChunk]:
        await WorkspaceService(self._session).get(
            workspace_id=workspace_id,
            owner_id=owner_id,
        )
        try:
            query_vector = await self._embedding_model.embed_query(question)
            vector_results = await self._vector_store.search(
                query_vector=query_vector,
                workspace_id=workspace_id,
                document_ids=document_ids,
                limit=top_k,
            )
        except (EmbeddingModelError, VectorStoreError) as exc:
            raise RetrievalFailedError from exc

        chunks = await self._documents.get_chunks_for_retrieval(
            workspace_id=workspace_id,
            chunk_ids=[result.id for result in vector_results],
            document_ids=document_ids,
        )
        return [
            RetrievedChunk(
                chunk_id=chunk.id,
                document_id=chunk.document_id,
                document_version_id=chunk.document_version_id,
                source_file_name=chunk.source_file_name,
                page_number=chunk.page_number,
                section_title=chunk.section_title,
                chunk_index=chunk.chunk_index,
                text=chunk.text,
                score=result.score,
            )
            for result in vector_results
            if (chunk := chunks.get(result.id)) is not None
        ]
