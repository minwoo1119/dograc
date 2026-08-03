import asyncio
from typing import Protocol


class EmbeddingModelError(OSError):
    """Raised when an embedding provider cannot produce vectors."""


class EmbeddingModel(Protocol):
    @property
    def dimensions(self) -> int: ...

    async def embed_documents(self, texts: list[str]) -> list[list[float]]: ...

    async def embed_query(self, text: str) -> list[float]: ...


class SentenceTransformerEmbeddingModel:
    def __init__(self, model_name: str, *, dimensions: int) -> None:
        self._model_name = model_name
        self._model = None
        self._dimensions = dimensions

    @property
    def dimensions(self) -> int:
        return self._dimensions

    async def embed_documents(self, texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        try:
            model = await asyncio.to_thread(self._get_or_load_model)
            vectors = await asyncio.to_thread(
                model.encode,
                texts,
                normalize_embeddings=True,
                convert_to_numpy=True,
            )
        except (OSError, ValueError, RuntimeError) as exc:
            raise EmbeddingModelError("failed to create embeddings") from exc
        return vectors.tolist()

    async def embed_query(self, text: str) -> list[float]:
        return (await self.embed_documents([text]))[0]

    def _get_or_load_model(self):
        if self._model is None:
            from sentence_transformers import SentenceTransformer

            model = SentenceTransformer(self._model_name)
            actual_dimensions = model.get_sentence_embedding_dimension()
            if actual_dimensions != self._dimensions:
                raise ValueError(
                    f"Configured embedding dimensions {self._dimensions} "
                    f"do not match model dimensions {actual_dimensions}"
                )
            self._model = model
        return self._model
