import hashlib

import pytest

from app.documents.validation import DocumentValidationError, validate_document_upload


def test_validates_pdf_from_content_signature() -> None:
    content = b"%PDF-1.7\nfixture"

    result = validate_document_upload(
        file_name="manual.PDF",
        declared_media_type="application/pdf",
        content=content,
        max_size_bytes=1024,
    )

    assert result.file_name == "manual.PDF"
    assert result.media_type == "application/pdf"
    assert result.size_bytes == len(content)
    assert result.content_hash == hashlib.sha256(content).hexdigest()


def test_validates_utf8_text() -> None:
    content = "문서 기반 질의응답".encode()

    result = validate_document_upload(
        file_name="guide.txt",
        declared_media_type="text/plain; charset=utf-8",
        content=content,
        max_size_bytes=1024,
    )

    assert result.media_type == "text/plain"


@pytest.mark.parametrize(
    ("file_name", "content", "expected_code"),
    [
        ("../secret.pdf", b"%PDF-1.7", "INVALID_FILE_NAME"),
        (r"..\secret.pdf", b"%PDF-1.7", "INVALID_FILE_NAME"),
        ("script.exe", b"MZ", "UNSUPPORTED_FILE_TYPE"),
        ("empty.txt", b"", "EMPTY_FILE"),
        ("binary.txt", b"text\x00hidden", "UNSUPPORTED_FILE_TYPE"),
        ("fake.pdf", b"plain text", "UNSUPPORTED_FILE_TYPE"),
    ],
)
def test_rejects_unsafe_or_mismatched_files(
    file_name: str,
    content: bytes,
    expected_code: str,
) -> None:
    with pytest.raises(DocumentValidationError) as raised:
        validate_document_upload(
            file_name=file_name,
            declared_media_type="application/octet-stream",
            content=content,
            max_size_bytes=1024,
        )

    assert raised.value.code == expected_code


def test_rejects_file_over_configured_limit() -> None:
    with pytest.raises(DocumentValidationError) as raised:
        validate_document_upload(
            file_name="large.txt",
            declared_media_type="text/plain",
            content=b"1234",
            max_size_bytes=3,
        )

    assert raised.value.code == "FILE_TOO_LARGE"


def test_rejects_declared_mime_type_that_conflicts_with_content() -> None:
    with pytest.raises(DocumentValidationError) as raised:
        validate_document_upload(
            file_name="manual.pdf",
            declared_media_type="text/plain",
            content=b"%PDF-1.7",
            max_size_bytes=1024,
        )

    assert raised.value.code == "UNSUPPORTED_FILE_TYPE"
