import uuid

from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.db.models.document import Document, DocumentChunk, DocumentPage, DocumentVersion
from app.db.models.workspace import Workspace


class DocumentRepository:
    def __init__(self, session: AsyncSession) -> None:
        self._session = session

    async def add(self, document: Document) -> Document:
        self._session.add(document)
        await self._session.flush()
        await self._session.refresh(document)
        return document

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
