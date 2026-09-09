import uuid
from datetime import datetime

from pydantic import BaseModel, ConfigDict

from app.db.models.document import DocumentStatus


class DocumentResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    workspace_id: uuid.UUID
    source_file_name: str
    media_type: str
    status: DocumentStatus
    failure_code: str | None = None
    created_at: datetime
    updated_at: datetime
