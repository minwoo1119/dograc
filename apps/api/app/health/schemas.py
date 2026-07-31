from typing import Literal

from pydantic import BaseModel, Field


class DependencyCheck(BaseModel):
    name: str
    healthy: bool
    detail: str | None = None


class HealthResponse(BaseModel):
    status: Literal["ok", "unavailable"]
    checks: list[DependencyCheck] = Field(default_factory=list)
