# Ingestion — Requirements

Extracted from [loom-design.md §22](../roadmap/loom-design.md#22-requirements-registry).

## Requirements

| ID      | Capability                   | Domain   | Scope                | Evidence                                | Verification                                         |
|---------|------------------------------|----------|----------------------|-----------------------------------------|------------------------------------------------------|
| QYL-002 | DuckDB Appender Architecture | Storage  | `IMPLEMENTED-IN-QYL` | SpanStorageRow (26 col), LogStorageRow (16 col) | Run OTLP ingest → verify rows in DuckDB         |
| QYL-008 | Port Architecture (OTLP)    | Protocol | `IMPLEMENTED-IN-QYL` | 5100/4317/4318 triple-port              | Verify all 3 ports accept OTLP, health checks pass  |

## Acceptance Criteria

- [ ] `curl -X POST http://localhost:4318/v1/traces` returns 202
- [ ] `curl -X POST http://localhost:5100/v1/traces` returns 202 (backward compat)
- [ ] gRPC OTLP on port 4317 accepts spans
- [ ] DuckDB contains ingested spans after OTLP POST
- [ ] Schema drift CI check passes (`nuke Generate` is idempotent)
