# Ingestion Slice

OTLP-native telemetry ingestion and columnar storage.

## Domain Objects

| Object   | Description                                     | src/ Mapping                          |
|----------|-------------------------------------------------|---------------------------------------|
| Storage  | DuckDB columnar store, appender architecture    | `qyl.collector/Storage/`              |
| Protocol | OTLP gRPC + HTTP, triple-port (5100/4317/4318)  | `qyl.collector/` (Kestrel endpoints)  |

## Scope

- OTLP trace/log/metric ingestion (gRPC + HTTP)
- DuckDB schema generation from TypeSpec (`eng/build/SchemaGenerator.cs`)
- Appender-based bulk writes (`SpanStorageRow`, `LogStorageRow`)
- Port architecture: QYL_PORT (5100), QYL_GRPC_PORT (4317), QYL_OTLP_PORT (4318)
- Retention and purge policies

## Cross-Slice Dependencies

- **sdk/** provides compile-time instrumentation that produces the OTLP spans this slice ingests
- **query/** reads from DuckDB tables this slice writes
- **intelligence/** triggers autofix/triage pipelines based on ingested data

## Key Files

```text
src/qyl.collector/Storage/DuckDbStore.cs
src/qyl.collector/Storage/DuckDbSchema.g.cs
src/qyl.collector/Storage/DuckDbStore.Spans.cs
src/qyl.collector/Storage/DuckDbStore.Logs.cs
src/qyl.collector/Storage/DuckDbStore.Metrics.cs
eng/build/SchemaGenerator.cs
core/specs/otel/*.tsp
```

## Reference

- [loom-design.md §15.2](../roadmap/loom-design.md#152-duckdb-storage--appender-architecture) — DuckDB Appender Architecture
- [loom-design.md §15.8](../roadmap/loom-design.md#158-port-architecture-otlp-standard-compliance) — Port Architecture
