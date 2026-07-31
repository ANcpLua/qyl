# qyl repository contract

Owns collector behavior, OTLP ingestion, normalization, DuckDB persistence,
product API, embedded dashboard, CLI, Runner, and workflow observation. The
normative cross-repository architecture is `ARCHITECTURE-1.0.0.md`.

OTLP messages remain OpenTelemetry-owned. Public qyl HTTP/SSE/Runner/workflow
models come from `Qyl.Api.Contracts`. Physical rows, SQL, retention, and
checkpoints remain private. The collector must never export telemetry to its own
ingest endpoint. NativeAOT is the collector delivery contract.

Change generated files through their owner and regenerate. Run focused tests,
then `dotnet run --project eng/build/build.csproj -- Ci`.
