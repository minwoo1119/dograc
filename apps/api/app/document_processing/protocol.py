from dataclasses import dataclass
from typing import Protocol


class DocumentParseError(Exception):
    """Raised when a supported document cannot be parsed."""


@dataclass(frozen=True, slots=True)
class ParsedPage:
    page_number: int
    text: str


@dataclass(frozen=True, slots=True)
class ParsedDocument:
    parser_name: str
    pages: tuple[ParsedPage, ...]


class DocumentParser(Protocol):
    @property
    def name(self) -> str: ...

    def supports(self, media_type: str) -> bool: ...

    def parse(self, content: bytes) -> ParsedDocument: ...
