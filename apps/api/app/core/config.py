from functools import lru_cache

from pydantic import Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    app_env: str = "development"
    app_name: str = "dograc"
    api_host: str = "0.0.0.0"
    api_port: int = Field(default=8000, ge=1, le=65535)
    docs_enabled: bool = True
    database_url: str = "postgresql+asyncpg://dograc:dograc@localhost:5432/dograc"
    max_document_size_bytes: int = Field(default=25 * 1024 * 1024, gt=0)
    object_storage_endpoint: str = "http://localhost:9000"
    object_storage_access_key: str = "change-me"
    object_storage_secret_key: str = "change-me"
    object_storage_bucket: str = "dograc"
    object_storage_region: str = "us-east-1"
    chunk_size_chars: int = Field(default=1200, gt=0)
    chunk_overlap_chars: int = Field(default=150, ge=0)
    embedding_model: str = "BAAI/bge-m3"
    embedding_dimensions: int = Field(default=1024, gt=0)
    qdrant_url: str = "http://localhost:6333"
    qdrant_api_key: str | None = None
    qdrant_collection: str = "dograc_chunks"
    retrieval_top_k: int = Field(default=10, ge=1, le=50)

    @model_validator(mode="after")
    def validate_chunk_settings(self) -> "Settings":
        if self.chunk_overlap_chars >= self.chunk_size_chars:
            raise ValueError("CHUNK_OVERLAP_CHARS must be smaller than CHUNK_SIZE_CHARS")
        return self


@lru_cache
def get_settings() -> Settings:
    return Settings()
