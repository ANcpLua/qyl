# qyl configuration

The `QYL_*` environment variables of the Collector, the qyl tool and the .NET build and smoke
tools. Standard `OTEL_*` and `PORT` settings keep their upstream semantics. The MCP server's
variables are in [services/qyl.mcp/README.md](../services/qyl.mcp/README.md#configuration).

| Variable | Scope | Default and behavior |
| --- | --- | --- |
| `QYL_BIND_ADDRESS` | Collector | IP address literal for every listener; defaults to `127.0.0.1`. |
| `QYL_CI_API_KEY` | NuGet consumer smoke | Optional `x-otlp-api-key` header on the smoke's phase-span export; read only when `QYL_CI_OTLP_ENDPOINT` is set. |
| `QYL_CI_LEG` | NuGet consumer smoke | Leg name on the exported phase spans (`cicd.pipeline.task.name`, service `qyl-ci-smoke-<leg>`); defaults to the current runtime identifier. |
| `QYL_CI_OTLP_ENDPOINT` | NuGet consumer smoke | OTLP/HTTP base URL the smoke exports one span per phase to (`/v1/traces`); unset makes the emitter inert, and a failed export never fails the smoke. |
| `QYL_CI_RUN_ID` | NuGet consumer smoke | `session.id` on the exported phase spans, grouping one smoke run; defaults to `local-<machine>`. |
| `QYL_DATA_PATH` | Collector | DuckDB path; defaults to `qyl.duckdb` (`qyl up` assigns per-collector files under `~/.qyl`). |
| `QYL_DB_MEMORY_LIMIT` | Collector | Optional DuckDB `memory_limit` value; unset leaves the engine default. |
| `QYL_DB_TEMP_DIR` | Collector | Optional DuckDB `temp_directory`; unset leaves the engine default. |
| `QYL_DB_THREADS` | Collector | Optional positive DuckDB worker count; unset leaves the engine default. |
| `QYL_GRPC_PORT` | Collector | OTLP/gRPC listener; defaults to `4317`, and `0` disables it. |
| `QYL_OTLP_AUTH_MODE` | Collector | `Unsecured` or `ApiKey`; defaults to `Unsecured` only in Development and to `ApiKey` otherwise. |
| `QYL_OTLP_CORS_ALLOWED_HEADERS` | Collector | Optional comma-separated additions to `content-type` and `x-otlp-api-key`. |
| `QYL_OTLP_CORS_ALLOWED_ORIGINS` | Collector | Comma-separated OTLP/HTTP browser origins (`*` allowed); unset disables OTLP CORS. |
| `QYL_OTLP_PORT` | Collector | OTLP/HTTP listener; defaults to `4318`, and `0` disables it. |
| `QYL_OTLP_PROJECT_KEYS` | Collector | JSON object mapping each project ID to an array of explicitly scoped `x-otlp-api-key` credentials, for example `{"acme":[{"key":"ingest-key","roles":["ingest"]},{"key":"operator-key","roles":["read","write","delete"]}]}`. Required in `ApiKey` mode. Every credential must be an object with exactly `key` and a nonempty `roles` array. The closed role set is `ingest` (OTLP export), `read` (every query route and running a saved query), `write` (creating and updating annotations and saved queries) and `delete` (deleting them; telemetry is never deleted through the API). Roles are independent. String-only keys, missing or empty roles, duplicate or unknown roles, and duplicate object fields fail collector startup. A key without a route's role gets `403`. Keys must be unique across projects; rotate by adding a second key to the same project. In this mode, reads and writes use the authenticated project, and conflicting project headers or resource attributes are rejected. |
| `QYL_ANALYSIS_SIGNING_KEY` | Collector | Optional secret signing material for project-bound analysis pagination, refresh and verification tokens. At least 32 bytes; independent of API credentials. If unset locally, a random key is persisted beside the database with owner-only permissions (in-memory databases use a process-lifetime key). Keep this value stable across replicas and restarts when tokens must remain valid; changing it invalidates existing tokens. |
| `QYL_PORT` | Collector | Product API listener; falls back to `PORT`, then `5100`. |
| `QYL_RETENTION_DAYS` | Collector | Trace/log age bound in days; defaults to `30`, and `0` disables retention. |
| `QYL_RETENTION_INTERVAL_MINUTES` | Collector | Retention and disk-pressure check interval; defaults to `60`. |
| `QYL_SMOKE_API_PORT` | NativeAOT smoke | Host product-API port; defaults to `5199`. |
| `QYL_SMOKE_GRPC_PORT` | NativeAOT smoke | Host OTLP/gRPC port; defaults to `4317`. |
| `QYL_SMOKE_OTLP_HTTP_PORT` | NativeAOT smoke | Host OTLP/HTTP port; defaults to `4318`. |
| `QYL_SMOKE_PLATFORM` | NativeAOT smoke | Docker build/run platform; defaults to `linux/amd64`. |
| `QYL_STORAGE_MIN_FREE_MB` | Collector | Free-space threshold for degraded `/health`; defaults to `2048`, and `0` disables the threshold. |
| `QYL_VERBOSE` | Build scripts | `1` makes `./eng/build.sh` (and `mise run check`, `check-full`) print the whole Fallout output, like `--verbose`; unset prints one line per target, the failed target's last lines and the log path under `artifacts/logs/`. |
| `QYL_WORKLOAD_ONESHOT` | Synthetic workload | `1` emits one acceptance turn and exits; unset runs continuously. |

`Unsecured` mode is intended for local development. It continues to accept caller-provided
project IDs and does not isolate customers. Use `ApiKey` mode with project-bound keys for
customer data.
