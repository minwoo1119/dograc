import uuid
from datetime import datetime

from pydantic import BaseModel, ConfigDict
from sqlalchemy import inspect as sa_inspect

from app.db.models.document import Document, DocumentStatus


class DocumentResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    workspace_id: uuid.UUID
    source_file_name: str
    media_type: str
    status: DocumentStatus
    failure_code: str | None = None
    size_bytes: int | None = None
    page_count: int | None = None
    chunk_count: int | None = None
    created_at: datetime
    updated_at: datetime

    @classmethod
    def from_document(cls, document: Document) -> "DocumentResponse":
        size_bytes: int | None = None
        page_count: int | None = None
        chunk_count: int | None = None

        state = sa_inspect(document)
        if "versions" not in state.unloaded and document.versions:
            latest = max(document.versions, key=lambda v: v.version_number)
            size_bytes = latest.size_bytes
            v_state = sa_inspect(latest)
            if "pages" not in v_state.unloaded:
                page_count = len(latest.pages)
                total_chunks = 0
                chunks_loaded = True
                for page in latest.pages:
                    p_state = sa_inspect(page)
                    if "chunks" in p_state.unloaded:
                        chunks_loaded = False
                        break
                    total_chunks += len(page.chunks)
                if chunks_loaded:
                    chunk_count = total_chunks

        return cls(
            id=document.id,
            workspace_id=document.workspace_id,
            source_file_name=document.source_file_name,
            media_type=document.media_type,
            status=document.status,
            failure_code=document.failure_code,
            size_bytes=size_bytes,
            page_count=page_count,
            chunk_count=chunk_count,
            created_at=document.created_at,
            updated_at=document.updated_at,
        )
