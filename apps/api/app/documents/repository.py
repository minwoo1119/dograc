import uuid

from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.db.models.document import (
    Document,
    DocumentChunk,
    DocumentPage,
    DocumentStatus,
    DocumentVersion,
)
from app.db.models.workspace import Workspace


class DocumentRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def add(self, document: Document) -> Document:
        self._session.add(document)
        await self._session.flush()
        await self._session.refresh(document)
        return document

    async def list_for_workspace(
        self,
        *,
        workspace_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> list[Document]:
        statement = (
            select(Document)
            .join(Workspace, Workspace.id == Document.workspace_id)
            .where(Document.workspace_id == workspace_id, Workspace.owner_id == owner_id)
            .order_by(Document.created_at.desc())
        )
        return list((await self._session.scalars(statement)).all())

    async def get_for_owner(
        self,
        *,
        document_id: uuid.UUID,
        owner_id: uuid.UUID,
    ) -> Document | None:
        statement = (
            select(Document)
            .join(Workspace, Workspace.id == Document.workspace_id)
            .where(Document.id == document_id, Workspace.owner_id == owner_id)
            .options(selectinload(Document.versions).selectinload(DocumentVersion.pages))
        )
        return await self._session.scalar(statement)

    async def delete(self, document: Document) -> None:
        await self._session.delete(document)
        await self._session.flush()

    async def replace_pages(
        self,
        *,
        version: DocumentVersion,
        pages: list[DocumentPage],
    ) -> None:
        await self._session.execute(
            delete(DocumentChunk).where(DocumentChunk.document_version_id == version.id)
        )
        await self._session.execute(
            delete(DocumentPage).where(DocumentPage.document_version_id == version.id)
        )
        version.pages = pages
        await self._session.flush()

    async def get_chunks_for_retrieval(
        self,
        *,
        workspace_id: uuid.UUID,
        chunk_ids: list[uuid.UUID],
        document_ids: list[uuid.UUID] | None,
    ) -> dict[uuid.UUID, DocumentChunk]:
        if not chunk_ids:
            return {}
        statement = (
            select(DocumentChunk)
            .join(Document, Document.id == DocumentChunk.document_id)
            .where(
                DocumentChunk.workspace_id == workspace_id,
                DocumentChunk.id.in_(chunk_ids),
                Document.status == DocumentStatus.READY,
            )
        )
        if document_ids:
            statement = statement.where(DocumentChunk.document_id.in_(document_ids))
        chunks = await self._session.scalars(statement)
        return {chunk.id: chunk for chunk in chunks}
