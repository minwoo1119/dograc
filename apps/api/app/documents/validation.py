import hashlib
import unicodedata
from dataclasses import dataclass
from pathlib import PurePosixPath

SUPPORTED_MEDIA_TYPES = {
    ".pdf": "application/pdf",
    ".txt": "text/plain",
}


class DocumentValidationError(Exception):
    def __init__(self, *, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


@dataclass(frozen=True, slots=True)
class ValidatedDocument:
    file_name: str
    media_type: str
    content: bytes
    content_hash: str
    size_bytes: int


def _validate_file_name(file_name: str) -> tuple[str, str]:
    normalized = unicodedata.normalize("NFC", file_name.strip())
    portable_name = normalized.replace("\\", "/")
    base_name = PurePosixPath(portable_name).name
    if (
        not normalized
        or base_name != portable_name
        or base_name in {".", ".."}
        or any(ord(character) < 32 for character in base_name)
        or len(base_name) > 255
    ):
        raise DocumentValidationError(
            code="INVALID_FILE_NAME",
            message="The file name is invalid.",
        )

    extension = PurePosixPath(base_name).suffix.lower()
    if extension not in SUPPORTED_MEDIA_TYPES:
        raise DocumentValidationError(
            code="UNSUPPORTED_FILE_TYPE",
            message="Only PDF and TXT files are supported.",
        )
    return base_name, extension


def _detect_media_type(content: bytes) -> str | None:
    if content.startswith(b"%PDF-"):
        return "application/pdf"
    if b"\x00" in content:
        return None
    try:
        content.decode("utf-8")
    except UnicodeDecodeError:
        return None
    return "text/plain"


def validate_document_upload(
    *,
    file_name: str,
    declared_media_type: str | None,
    content: bytes,
    max_size_bytes: int,
) -> ValidatedDocument:
    safe_file_name, extension = _validate_file_name(file_name)
    if not content:
        raise DocumentValidationError(
            code="EMPTY_FILE",
            message="The uploaded file is empty.",
        )
    if len(content) > max_size_bytes:
        raise DocumentValidationError(
            code="FILE_TOO_LARGE",
            message="The uploaded file exceeds the configured size limit.",
        )

    expected_media_type = SUPPORTED_MEDIA_TYPES[extension]
    detected_media_type = _detect_media_type(content)
    normalized_declared_type = (declared_media_type or "").partition(";")[0].strip().lower()
    if detected_media_type != expected_media_type or normalized_declared_type not in {
        "",
        "application/octet-stream",
        expected_media_type,
    }:
        raise DocumentValidationError(
            code="UNSUPPORTED_FILE_TYPE",
            message="The file content does not match its supported file type.",
        )

    return ValidatedDocument(
        file_name=safe_file_name,
        media_type=detected_media_type,
        content=content,
        content_hash=hashlib.sha256(content).hexdigest(),
        size_bytes=len(content),
    )
