"""Model provider protocols and adapters."""

from app.models.embedding import (
    EmbeddingModel,
    EmbeddingModelError,
    SentenceTransformerEmbeddingModel,
)
from app.models.generation import (
    GenerationModel,
    GenerationModelError,
    OpenAICompatibleGenerationModel,
)

__all__ = [
    "EmbeddingModel",
    "EmbeddingModelError",
    "GenerationModel",
    "GenerationModelError",
    "OpenAICompatibleGenerationModel",
    "SentenceTransformerEmbeddingModel",
]
