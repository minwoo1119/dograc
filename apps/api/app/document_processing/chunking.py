from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class ChunkDraft:
    text: str


class RecursiveCharacterChunker:
    name = "recursive-character-v1"
    _separators = ("\n\n", "\n", ". ", " ")

    def __init__(self, *, chunk_size: int, chunk_overlap: int) -> None:
        if chunk_size <= 0:
            raise ValueError("chunk_size must be positive")
        if chunk_overlap < 0 or chunk_overlap >= chunk_size:
            raise ValueError("chunk_overlap must be non-negative and smaller than chunk_size")
        self._chunk_size = chunk_size
        self._chunk_overlap = chunk_overlap

    def split(self, text: str) -> list[ChunkDraft]:
        normalized = text.strip()
        if not normalized:
            return []

        chunks: list[ChunkDraft] = []
        start = 0
        while start < len(normalized):
            end = min(start + self._chunk_size, len(normalized))
            if end < len(normalized):
                end = self._find_boundary(normalized, start, end)
            chunk_text = normalized[start:end].strip()
            if chunk_text:
                chunks.append(ChunkDraft(text=chunk_text))
            if end >= len(normalized):
                break
            start = max(start + 1, end - self._chunk_overlap)
        return chunks

    def _find_boundary(self, text: str, start: int, end: int) -> int:
        window = text[start:end]
        minimum = self._chunk_size // 2
        candidates = []
        for separator in self._separators:
            position = window.rfind(separator, minimum)
            if position >= 0:
                candidates.append(position + len(separator))
        return start + max(candidates) if candidates else end
