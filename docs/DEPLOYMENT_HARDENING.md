# dograc Desktop — Deployment, Persistence & Security Hardening Policy

## 1. Packaged Runtime Layout (Zero Git-Repo Dependency)

`scripts/package_wpf.ps1` produces both the portable ZIP (`dograc-wpf-win-x64.zip`) and the Velopack Windows installer (`dograc-win-Setup.exe`) with the following self-contained structure:

```text
<InstallDir>\                          # %LOCALAPPDATA%\dograc\current\ (Velopack) or portable folder
├─ dograc.exe                          # Self-contained .NET 10 WPF client (no separate .NET install needed)
├─ apps\api\                           # Bundled FastAPI backend source + pyproject.toml + uv.lock + alembic
├─ infra\compose.yaml                  # Bundled Docker Compose infrastructure definition
└─ .env.example                        # Default environment template
```

- **WPF Runtime**: Published `--self-contained true` for `win-x64`; end users do **not** need `.NET 10 SDK` or `.NET Desktop Runtime` pre-installed.
- **Python Backend Runtime**: On first launch without a local `.venv`, `LocalServiceManager` detects `uv` (or local `python` / `.venv`) and launches `uvicorn` against the bundled `apps\api` directory. If neither `uv` nor Python is installed, Setup Wizard directs the user to the official installer guide.
- **No Hardcoded Dev Paths**: `ProjectRootResolver` checks `DOGRAC_ROOT` → `AppContext.BaseDirectory` (packaged mode) → upward dev repo search. It never depends on `Documents\Github\dograc` or a Git checkout.

## 2. User Data Directory & Update/Uninstall Preservation

All mutable user data is stored **outside** the Velopack application binary directory, under `%LOCALAPPDATA%\dograc\` (overridable via `DOGRAC_DATA_DIR`):

```text
%LOCALAPPDATA%\dograc\
├─ settings.json                       # UI preferences, theme, Ollama endpoint, selected model
├─ .env                                # Per-user backend environment file (auto-created from .env.example)
├─ logs\                               # Structured JSONL diagnostic logs (secrets sanitized)
└─ data\                               # DOGRAC_DATA_DIR (preserved across updates & uninstalls)
   ├─ dograc_local.db                  # Persistent local SQLite database (standalone mode)
   ├─ storage\                         # Persistent local document originals (standalone mode)
   └─ qdrant\                          # Disk-persistent local Qdrant vectors (survives app restart!)
```

- **No Administrator Privileges Required**: Both Velopack per-user installation (`%LOCALAPPDATA%\dograc\current`) and all writable state (`%LOCALAPPDATA%\dograc\data`) run under standard user privileges without UAC elevation.
- **Velopack Auto-Update Preservation**: Updating `dograc` replaces only `current\`; `%LOCALAPPDATA%\dograc\data\`, `settings.json`, and `.env` remain untouched.
- **Update Failure Recovery**: Velopack stages updates atomically in `packages\` and swaps on restart; if download/verification fails, `GitHubUpdateService` maps structured error codes (`UPD-001`~`UPD-004`) and the existing version continues running unaffected.
- **Uninstall Data Preservation Policy**: Uninstalling via Windows Apps & Features removes the application binaries while preserving `%LOCALAPPDATA%\dograc\data\` and `settings.json` so users never lose indexed documents or conversation history by accident. Users who wish to wipe all local data can delete `%LOCALAPPDATA%\dograc\` manually.

## 3. Security, Privacy & Offline Operation

- **Local-Only Guarantee**: When `OllamaEndpointUrl` points to a loopback address (`localhost`, `127.0.0.1`, `::1`), all document parsing, embedding, vector search, and LLM generation stay strictly on the local machine.
- **External Endpoint Disclosure**: When a non-loopback endpoint URL is configured on the Models page, dograc displays a visible warning explaining that user questions and retrieved document excerpts will be sent to that external server.
- **Workspace & Deletion Isolation**: Every DB query and Qdrant vector query filters strictly by `workspace_id` and `owner_id`. Deleting a document removes its file object, Qdrant vectors, and DB record; retrieval joins against `Document.status == READY` so deleted or failed documents never appear in search results.
- **Prompt Injection Defense**: Retrieved chunks are enclosed in explicit `<document_context>` delimiters (with nested closing tags escaped) and the system prompt explicitly instructs the LLM to treat document text as untrusted reference data rather than executable instructions.
- **Crash & Concurrency Integrity**: Duplicate concurrent indexing requests return HTTP `409 Conflict` (`DOCUMENT_ALREADY_PROCESSING`). On backend startup, any document left stuck in `PROCESSING` after a forced crash is automatically transitioned to `FAILED` (`DOCUMENT_PROCESS_INTERRUPTED`) for safe re-indexing.
- **Offline / Restricted Network Notes**:
  - Once Python dependencies (`uv sync`) and Ollama models (`ollama pull qwen2.5:7b`) are cached locally, document upload, indexing, and RAG Q&A operate fully offline.
  - Update checks (`api.github.com`) fail gracefully with structured diagnostics without blocking offline workspace usage.
