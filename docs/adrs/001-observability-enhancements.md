# ADR-001: qyl Observability Enhancements v1.0

Date: 2026-02-14
Status: Accepted

## Context

AI-assisted diagnosis needed faster build failure triage and source-aware log inspection.

## Decision

1. Store build-failure metadata in DuckDB (`build_failures`) and keep binlogs on disk.
2. Enrich OTLP logs with source metadata fields on ingestion.
3. Resolve log source with attrs-first (`code.file.path`, `code.line.number`, `code.column.number`, `code.function.name`) and fallback stacktrace/PDB best effort.
4. Expose build-failure data through collector REST endpoints and MCP tools.

## Consequences

- Faster diagnosis via MCP tools without manual repository search.
- Small storage overhead for new source columns and build failure rows.
- Graceful degradation when symbols are missing.

## Runtime Controls

- `QYL_BUILD_FAILURE_CAPTURE_ENABLED` (default true)
- `QYL_MAX_BUILD_FAILURES` (default 10)
- `QYL_BINLOG_DIR` (default `.qyl/binlogs`)
