import pymupdf

from app.document_processing.protocol import (
    DocumentParseError,
    ParsedDocument,
    ParsedPage,
)


class TextDocumentParser:
    name = "text-utf8"

    def supports(self, media_type: str) -> bool:
        return media_type == "text/plain"

    def parse(self, content: bytes) -> ParsedDocument:
        try:
            text = content.decode("utf-8")
        except UnicodeDecodeError as exc:
            raise DocumentParseError("TXT content is not valid UTF-8") from exc
        return ParsedDocument(
            parser_name=self.name,
            pages=(ParsedPage(page_number=1, text=text),),
        )


class PdfDocumentParser:
    name = "pymupdf"

    def supports(self, media_type: str) -> bool:
        return media_type == "application/pdf"

    def parse(self, content: bytes) -> ParsedDocument:
        try:
            with pymupdf.open(stream=content, filetype="pdf") as document:
                if document.needs_pass:
                    raise DocumentParseError("Encrypted PDF files are not supported")
                pages = tuple(
                    ParsedPage(
                        page_number=index + 1,
                        text=page.get_text("text").strip(),
                    )
                    for index, page in enumerate(document)
                )
        except DocumentParseError:
            raise
        except (pymupdf.FileDataError, RuntimeError, ValueError) as exc:
            raise DocumentParseError("PDF content could not be parsed") from exc
        if not pages:
            raise DocumentParseError("PDF contains no pages")
        return ParsedDocument(parser_name=self.name, pages=pages)


class ParserRegistry:
    def __init__(self) -> None:
        self._parsers = (TextDocumentParser(), PdfDocumentParser())

    def for_media_type(self, media_type: str) -> TextDocumentParser | PdfDocumentParser:
        for parser in self._parsers:
            if parser.supports(media_type):
                return parser
        raise DocumentParseError("No parser supports this media type")
