from fastapi import APIRouter

from app.api.v1.routes.documents import router as documents_router
from app.api.v1.routes.health import router as health_router
from app.api.v1.routes.processing import router as processing_router
from app.api.v1.routes.workspaces import router as workspaces_router

api_router = APIRouter()
api_router.include_router(health_router, prefix="/health", tags=["health"])
api_router.include_router(workspaces_router, prefix="/workspaces", tags=["workspaces"])
api_router.include_router(
    documents_router,
    prefix="/workspaces/{workspace_id}/documents",
    tags=["documents"],
)
api_router.include_router(processing_router, prefix="/documents", tags=["documents"])
