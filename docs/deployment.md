# Docker Compose deployment

The backend stack is designed to move between Docker hosts without changing
application code. It contains the API, PostgreSQL, Qdrant, MinIO, and a one-shot
MinIO bucket initializer.

## Prepare the host

Install Docker Engine with the Compose plugin, clone the repository, and create the
runtime environment file:

```bash
cp .env.example .env
```

At minimum, replace these values with long, randomly generated secrets:

```dotenv
POSTGRES_PASSWORD=change-me
OBJECT_STORAGE_ACCESS_KEY=change-me
OBJECT_STORAGE_SECRET_KEY=change-me
```

Keep `.env` only on the server. It is excluded from Git. `BIND_ADDRESS` defaults to
`127.0.0.1`; set it to `0.0.0.0` only when the API and storage administration ports
must be exposed directly. Prefer a TLS reverse proxy for remote access.

## Start and inspect

```bash
docker compose --env-file .env -f infra/compose.yaml config
docker compose --env-file .env -f infra/compose.yaml up -d --build
docker compose --env-file .env -f infra/compose.yaml ps
docker compose --env-file .env -f infra/compose.yaml logs -f api
```

The named volumes `postgres-data`, `qdrant-data`, and `minio-data` contain persistent
state. Back them up before moving hosts or upgrading stateful services. A source-code
copy alone is not a complete deployment backup.

## Stop or upgrade

```bash
docker compose --env-file .env -f infra/compose.yaml down
git pull --ff-only
docker compose --env-file .env -f infra/compose.yaml up -d --build
```

Do not add `--volumes` to `down` during a normal stop or upgrade because that deletes
the local persistent data.

