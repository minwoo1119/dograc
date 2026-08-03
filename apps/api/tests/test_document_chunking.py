import pytest

from app.document_processing.chunking import RecursiveCharacterChunker


def test_chunker_splits_with_bounded_size_and_overlap() -> None:
    text = "First paragraph.\n\nSecond paragraph is longer.\n\nThird paragraph."
    chunker = RecursiveCharacterChunker(chunk_size=30, chunk_overlap=5)

    chunks = chunker.split(text)

    assert len(chunks) >= 2
    assert all(0 < len(chunk.text) <= 30 for chunk in chunks)
    assert chunks[0].text in text
    assert chunks[-1].text.endswith("Third paragraph.")


def test_chunker_does_not_create_chunks_for_blank_page() -> None:
    chunks = RecursiveCharacterChunker(chunk_size=10, chunk_overlap=2).split(" \n ")

    assert chunks == []


@pytest.mark.parametrize(
    ("chunk_size", "chunk_overlap"),
    [(0, 0), (10, -1), (10, 10), (10, 11)],
)
def test_chunker_rejects_invalid_configuration(
    chunk_size: int,
    chunk_overlap: int,
) -> None:
    with pytest.raises(ValueError):
        RecursiveCharacterChunker(
            chunk_size=chunk_size,
            chunk_overlap=chunk_overlap,
        )
