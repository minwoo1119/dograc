import pymupdf
import pytest

from app.document_processing.parsers import ParserRegistry
from app.document_processing.protocol import DocumentParseError


def test_text_parser_preserves_utf8_text() -> None:
    parser = ParserRegistry().for_media_type("text/plain")

    parsed = parser.parse("첫 번째 문서".encode())

    assert parsed.parser_name == "text-utf8"
    assert parsed.pages[0].page_number == 1
    assert parsed.pages[0].text == "첫 번째 문서"


def test_pdf_parser_preserves_page_numbers() -> None:
    document = pymupdf.open()
    for text in ("first page", "second page"):
        page = document.new_page()
        page.insert_text((72, 72), text)
    content = document.tobytes()
    document.close()

    parsed = ParserRegistry().for_media_type("application/pdf").parse(content)

    assert [(page.page_number, page.text) for page in parsed.pages] == [
        (1, "first page"),
        (2, "second page"),
    ]


def test_pdf_parser_rejects_corrupt_content() -> None:
    with pytest.raises(DocumentParseError):
        ParserRegistry().for_media_type("application/pdf").parse(b"%PDF-corrupt")
