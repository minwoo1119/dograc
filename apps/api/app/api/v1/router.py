from fastapi import APIRouter

from app.api.v1.routes.conversations import (
    conversations_router,
    workspace_conversations_router,
)
from app.api.v1.routes.documents import (
    document_router as document_items_router,
)
from app.api.v1.routes.documents import (
    router as documents_router,
)
from app.api.v1.routes.health import router as health_router
from app.api.v1.routes.processing import router as processing_router
from app.api.v1.routes.retrieval import router as retrieval_router
from app.api.v1.routes.traces import traces_router
from app.api.v1.routes.workspaces import router as workspaces_router

api_router = APIRouter()
api_router.include_router(health_router, prefix="/health", tags=["health"])
api_router.include_router(workspaces_router, prefix="/workspaces", tags=["workspaces"])
api_router.include_router(
    documents_router,
    prefix="/workspaces/{workspace_id}/documents",
    tags=["documents"],
)
api_router.include_router(document_items_router, prefix="/documents", tags=["documents"])
api_router.include_router(processing_router, prefix="/documents", tags=["documents"])
api_router.include_router(
    workspace_conversations_router,
    prefix="/workspaces/{workspace_id}/conversations",
    tags=["conversations"],
)
api_router.include_router(
    conversations_router,
    prefix="/conversations",
    tags=["conversations"],
)
api_router.include_router(
    traces_router,
    prefix="/traces",
    tags=["traces"],
)
api_router.include_router(
    retrieval_router,
    prefix="/workspaces/{workspace_id}/search",
    tags=["retrieval"],
)
