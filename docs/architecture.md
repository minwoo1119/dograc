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
