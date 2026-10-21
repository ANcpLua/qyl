# Configuration

The environment variables of the Collector and of the build and test tools. The MCP server's
variables are in [services/qyl.mcp/README.md](../services/qyl.mcp/README.md#configuration).
`OTEL_*` variables keep their OpenTelemetry meaning.

## Collector

| Variable | Default and behavior |
| --- | --- |
| `QYL_BIND_ADDRESS` | IP address literal every listener binds to; defaults to `127.0.0.1`. |
| `QYL_PORT` | API listener; falls back to `PORT`, then `5100`. |
| `QYL_OTLP_PORT` | OTLP/HTTP listener; defaults to `4318`, and `0` disables it. The API listener also accepts OTLP/HTTP. |
| `QYL_GRPC_PORT` | OTLP/gRPC listener; defaults to `4317`, and `0` disables it. |
| `QYL_DATA_PATH` | DuckDB file; defaults to `qyl.duckdb` in the working directory. `qyl up` gives each Collector its own file under `~/.qyl/`. |
| `QYL_DB_MEMORY_LIMIT` | DuckDB `memory_limit`; unset keeps DuckDB's default. |
| `QYL_DB_TEMP_DIR` | DuckDB `temp_directory`; unset keeps DuckDB's default. |
| `QYL_DB_THREADS` | DuckDB worker threads, when positive; unset keeps DuckDB's default. |
| `QYL_OTLP_AUTH_MODE` | `Unsecured` or `ApiKey`; defaults to `Unsecured` in the `Development` environment and to `ApiKey` everywhere else. |
| `QYL_OTLP_PROJECT_KEYS` | Required in `ApiKey` mode. A JSON object mapping each project ID to its credentials, for example `{"acme":[{"key":"ingest-key","roles":["ingest"]},{"key":"operator-key","roles":["read","write","delete"]}]}`. Each credential has exactly `key` and a nonempty `roles` array of distinct roles: `ingest` (OTLP export), `read` (every query route and running a saved query), `write` (creating and changing annotations, saved queries and failure status) and `delete` (deleting annotations and saved queries; no route deletes telemetry). A key is unique across all projects; rotate by adding a second key to the project. Malformed entries stop the Collector at startup. A request carries its key in `x-otlp-api-key` and reads and writes that key's project: a key without the route's role gets `403`, and an `x-qyl-project` header or `qyl.project.id` resource attribute naming another project is refused. |
| `QYL_OTLP_CORS_ALLOWED_ORIGINS` | Comma-separated browser origins for OTLP/HTTP, `*` for any; unset disables OTLP CORS. |
| `QYL_OTLP_CORS_ALLOWED_HEADERS` | Comma-separated request headers allowed in addition to `content-type` and `x-otlp-api-key`. |
| `QYL_ANALYSIS_SIGNING_KEY` | Secret of at least 32 bytes that signs the project-bound pagination, refresh and verification tokens of the analysis routes, independent of the API keys. Unset, the Collector creates a random key beside the database file, owner-only on Unix (a process-lifetime key for `:memory:`). Keep it stable across replicas and restarts; changing it invalidates issued tokens. |
| `QYL_RETENTION_DAYS` | Age in days after which spans, logs, metric points and annotations are deleted; a metric series goes with its last point, and an annotation counts from its last change. Saved queries never expire. Defaults to `30`; `0` disables retention. |
| `QYL_RETENTION_INTERVAL_MINUTES` | Minutes between retention runs; defaults to `60`. |
| `QYL_STORAGE_MIN_FREE_MB` | Free disk space below which `/health` reports `degraded`; defaults to `2048`, and `0` disables the check. |

`Unsecured` mode accepts any caller and the project IDs it names, so it isolates no one; `qyl up`
uses it with every listener on loopback. Serve shared or customer data in `ApiKey` mode with
project keys.

## Build and test tools

| Variable | Used by | Behavior |
| --- | --- | --- |
| `QYL_VERBOSE` | `./eng/build.sh` | `1` prints the whole Fallout output, like `--verbose`. Unset, it prints one line per target, the failed target's last lines and the log path under `artifacts/logs/`. |
| `QYL_SMOKE_API_PORT`, `QYL_SMOKE_OTLP_HTTP_PORT`, `QYL_SMOKE_GRPC_PORT` | NativeAOT container smoke | Host ports for the API, OTLP/HTTP and OTLP/gRPC; default `5199`, `4318` and `4317`. |
| `QYL_SMOKE_PLATFORM` | NativeAOT container smoke | Docker build and run platform; defaults to `linux/amd64`. |
| `QYL_CI_OTLP_ENDPOINT` | Packed-tool smoke (`eng/tools/QylToolSmoke`) | OTLP/HTTP base URL; the smoke exports one span per phase to its `/v1/traces`, which `ci_log` reads. Unset, it exports nothing, and a failed export never fails the smoke. |
| `QYL_CI_API_KEY` | Packed-tool smoke | `x-otlp-api-key` for that export. |
| `QYL_CI_RUN_ID` | Packed-tool smoke | `session.id` of the phase spans, one per smoke run; defaults to `local-<machine>`. |
| `QYL_CI_LEG` | Packed-tool smoke | Leg name, as `cicd.pipeline.task.name` and in the service name `qyl-ci-smoke-<leg>`; defaults to the runtime identifier. |
| `QYL_COLLECTOR_DLL` | MCP answer key and project smoke | Collector build to start; defaults to the Release build under `artifacts/bin/qyl.collector/release/`. |
| `QYL_UPDATE_SNAPSHOTS` | XML generator tests | `1` rewrites the generated-source snapshots instead of comparing them. |
| `QYL_WORKLOAD_ONESHOT` | `Qyl.Run.Workload` | `1` emits one acceptance turn and exits; unset, it runs continuously. |
