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

