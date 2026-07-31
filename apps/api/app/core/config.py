from functools import lru_cache

from pydantic import Field
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


@lru_cache
def get_settings() -> Settings:
    return Settings()
