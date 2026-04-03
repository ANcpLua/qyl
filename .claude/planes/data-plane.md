# Data plane

Mission:
- ingest telemetry
- normalize it
- persist it cheaply and correctly

Owns:
- OTLP ingest
- canonical telemetry schema
- DuckDB storage
- promoted columns
- retention and compaction
- ingestion auth, quotas, and backpressure

Depends on:
- contracts and storage primitives only

Must not depend on:
- MAF
- AG-UI
- LLM providers
- workflow orchestration
- dashboard composition rules

Current qyl areas:
- `src/qyl.collector` ingest and storage paths
- `specs/collector.md`
- `specs/telemetry-data-model.md`

Success condition:
- same input produces the same stored truth regardless of any agent subsystem.
