# Architecture

## API foundation

The backend is a versioned FastAPI application under `apps/api`. Route handlers are
kept thin and delegate infrastructure checks and future domain behavior to dedicated
modules.

Health endpoints:

- `GET /api/v1/health/live` confirms that the API process can serve requests.
- `GET /api/v1/health/ready` runs registered dependency checks concurrently and
  returns HTTP 503 when a required dependency is unavailable.

Readiness checks implement the `ReadinessCheck` protocol. PostgreSQL, Qdrant, and
object-storage adapters will register concrete checks when those adapters are added.
Exception messages are not returned to clients because they can contain connection
details or credentials.

## Container topology

`infra/compose.yaml` defines a portable single-host backend deployment:

- The API is built from `apps/api/Dockerfile` and runs as an unprivileged user.
- PostgreSQL owns application metadata.
- Qdrant owns vector search records.
- MinIO owns uploaded source files.
- A one-shot MinIO client creates the configured bucket idempotently.

Only host-bound ports are published. Service-to-service traffic uses the private
`backend` bridge network and internal DNS names. Named volumes keep state independent
from container lifecycles.

## Workspace ownership boundary

Workspace queries always include the current owner identifier in the database
predicate. During the pre-authentication MVP, the API receives this identifier from
the required `X-User-ID` header. This header is a development-only trust boundary,
not authentication. A production reverse proxy must not accept it directly from
untrusted clients; the future authentication layer will derive the identifier from a
verified session or token while preserving the same service boundary.

Single-workspace reads and deletes use both `workspace_id` and `owner_id` in their
lookup. Missing and unauthorized resources return the same `WORKSPACE_NOT_FOUND`
response so the endpoint does not reveal whether another owner has that identifier.

## Document ingestion boundary

`Document` stores workspace-scoped metadata and processing state. Immutable
`DocumentVersion` records preserve the object-storage key, SHA-256 content hash,
size, and version number required for idempotent processing and source tracing.

Before object storage or parsing, Phase 1 uploads pass framework-independent
validation. The validator rejects path components and control characters in file
names, unsupported extensions, empty or oversized content, invalid UTF-8 text,
binary text, and PDF/TXT content that conflicts with the extension or declared MIME
type. PDF and TXT are the only accepted formats in this phase.

Validated originals are written through the `FileStorage` protocol. The production
adapter uses asynchronous S3-compatible calls and supports MinIO through the
configured endpoint. An upload verifies Workspace ownership before writing, then
stores the immutable original and commits `Document` plus `DocumentVersion` metadata.
If the database commit fails after object creation, the service rolls back and
attempts a compensating object deletion. Storage provider exceptions are converted
to stable application errors without returning credential or endpoint details.

Document processing reads the immutable original through `FileStorage`, selects a
parser by trusted media type, and persists one `DocumentPage` per source page with
the parser name and SHA-256 text hash. Processing transitions through `processing`
to `ready`; parser or source-read failures end in `failed` with
`DOCUMENT_PARSE_FAILED`. Reprocessing replaces pages for the same version in one
transaction, so repeated execution does not create duplicate pages.

Each non-empty page is split with the configured recursive character strategy. The
current settings are explicitly character-based (`CHUNK_SIZE_CHARS` and
`CHUNK_OVERLAP_CHARS`), not tokenizer counts. Every persisted `DocumentChunk`
retains Workspace, document, version, page, source filename, parser, strategy,
sequence, and content-hash metadata. Reprocessing replaces pages and their cascaded
chunks atomically, preventing duplicate search records in PostgreSQL.

Chunk text is embedded through the `EmbeddingModel` protocol and indexed through
the `VectorStore` protocol. The default adapters use Sentence Transformers with
`BAAI/bge-m3` and asynchronous Qdrant operations. Every Qdrant payload includes the
Workspace and source-tracing metadata required for mandatory retrieval filtering and
citations. A document reaches `ready` only after vector upsert succeeds. Indexing or
embedding failures roll back PostgreSQL changes, remove document-scoped vectors when
possible, and record `DOCUMENT_PROCESSING_FAILED`.

Dense retrieval embeds the normalized question and requires `workspace_id` in every
`VectorStore.search` call. Optional document filters are combined with that mandatory
filter. Qdrant determines rank and score, but its payload is not treated as the
authorization source: candidate Chunk IDs are loaded again from PostgreSQL with
Workspace, selected-document, and `ready`-status predicates before text or source
metadata is returned.
