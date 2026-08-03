import asyncio
from typing import Protocol

from sentence_transformers import SentenceTransformer


class EmbeddingModel(Protocol):
    @property
    def dimensions(self) -> int: ...

    async def embed_documents(self, texts: list[str]) -> list[list[float]]: ...

    async def embed_query(self, text: str) -> list[float]: ...


class SentenceTransformerEmbeddingModel:
    def __init__(self, model_name: str) -> None:
        self._model = SentenceTransformer(model_name)
        self._dimensions = self._model.get_sentence_embedding_dimension()
        if self._dimensions is None:
            raise ValueError("Embedding model does not expose its dimensions")

    @property
    def dimensions(self) -> int:
        return self._dimensions

    async def embed_documents(self, texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        vectors = await asyncio.to_thread(
            self._model.encode,
            texts,
            normalize_embeddings=True,
            convert_to_numpy=True,
        )
        return vectors.tolist()

    async def embed_query(self, text: str) -> list[float]:
        return (await self.embed_documents([text]))[0]
