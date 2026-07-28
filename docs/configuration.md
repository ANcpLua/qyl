# qyl configuration

The complete `QYL_*` environment contract bound by product, developer, acceptance,
and CI code in this repository. Standard `OTEL_*`, `PORT`, and `VITE_*` settings
retain their upstream semantics and are not duplicated here.

This table moved out of `README.md` when that file became an architecture summary.
`VerifyConfigurationKnobs` (`eng/build/BuildConfigurationKnobs.cs`) reads it and fails
the build when a row has no code binding or a code binding has no row — so the table
cannot rot into decoration.

| Variable | Scope | Default and behavior |
| --- | --- | --- |
| `QYL_API_KEY` | `qyl codex` | Optional `x-otlp-api-key` used for workflow journal upload and its secondary OpenTelemetry projection; unset keeps encrypted batches in the local spool when the hosted collector requires authentication. |
| `QYL_BASE_URL` | Dashboard Playwright | Unset starts the embedded product at `http://127.0.0.1:5100`; a value targets an already-running product. |
| `QYL_BIND_ADDRESS` | Collector | IP address literal for every listener; defaults to `127.0.0.1`. |
| `QYL_CI_API_KEY` | NuGet consumer smoke | Optional `x-otlp-api-key` used only when CI telemetry export is enabled. |
| `QYL_CI_LEG` | NuGet consumer smoke | CI telemetry leg name; defaults to the current runtime identifier. |
| `QYL_CI_OTLP_ENDPOINT` | NuGet consumer smoke | OTLP/HTTP base URL for smoke telemetry; unset makes the emitter inert. |
| `QYL_CI_RUN_ID` | NuGet consumer smoke | CI telemetry session id; defaults to `local-<machine>`. |
| `QYL_CODEX_EXECUTABLE` | `qyl codex` | Codex executable to launch and inspect; defaults to `codex` on `PATH`. |
| `QYL_DATA_PATH` | Collector | DuckDB path; defaults to `qyl.duckdb` (`qyl up` assigns per-collector files under `~/.qyl`). |
| `QYL_DB_MEMORY_LIMIT` | Collector | Optional DuckDB `memory_limit` value; unset leaves the engine default. |
| `QYL_DB_TEMP_DIR` | Collector | Optional DuckDB `temp_directory`; unset leaves the engine default. |
| `QYL_DB_THREADS` | Collector | Optional positive DuckDB worker count; unset leaves the engine default. |
| `QYL_GRPC_PORT` | Collector | OTLP/gRPC listener; defaults to `4317`, and `0` disables it. |
| `QYL_OTLP_AUTH_MODE` | Collector | `Unsecured` or `ApiKey`; defaults to `Unsecured` only in Development and to `ApiKey` otherwise. |
| `QYL_OTLP_CORS_ALLOWED_HEADERS` | Collector | Optional comma-separated additions to `content-type` and `x-otlp-api-key`. |
| `QYL_OTLP_CORS_ALLOWED_ORIGINS` | Collector | Comma-separated OTLP/HTTP browser origins (`*` allowed); unset disables OTLP CORS. |
| `QYL_OTLP_PORT` | Collector | OTLP/HTTP listener; defaults to `4318`, and `0` disables it. |
| `QYL_OTLP_PRIMARY_API_KEY` | Collector | Primary `x-otlp-api-key`; at least one key is required in `ApiKey` mode. |
| `QYL_OTLP_SECONDARY_API_KEY` | Collector | Optional rotation key accepted alongside the primary key. |
| `QYL_PORT` | Collector | Product API/dashboard listener; falls back to `PORT`, then `5100`. |
| `QYL_RETENTION_DAYS` | Collector | Trace/log age bound in days; defaults to `30`, and `0` disables retention. |
| `QYL_RETENTION_INTERVAL_MINUTES` | Collector | Retention and disk-pressure check interval; defaults to `60`. |
| `QYL_SMOKE_API_PORT` | NativeAOT smoke | Host product-API port; defaults to `5199`. |
| `QYL_SMOKE_GRPC_PORT` | NativeAOT smoke | Host OTLP/gRPC port; defaults to `4317`. |
| `QYL_SMOKE_OTLP_HTTP_PORT` | NativeAOT smoke | Host OTLP/HTTP port; defaults to `4318`. |
| `QYL_SMOKE_PLATFORM` | NativeAOT smoke | Docker build/run platform; defaults to `linux/amd64`. |
| `QYL_STORAGE_MIN_FREE_MB` | Collector | Free-space threshold for degraded `/health`; defaults to `2048`, and `0` disables the threshold. |
| `QYL_WORKFLOW_CONTENT_KEY` | Collector | Base64-encoded 32-byte AES-GCM key for captured workflow content. Falls back to a key derived from `QYL_OTLP_PRIMARY_API_KEY`; Development and Testing use a fixed non-production key. Hosted startup fails when none is available. |
| `QYL_WORKLOAD_ONESHOT` | Synthetic workload | `1` emits one acceptance turn and exits; unset runs continuously. |
