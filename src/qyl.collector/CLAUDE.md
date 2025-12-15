# qyl.collector

@import "../../CLAUDE.md"

## Scope

Backend service: OTLP ingestion, DuckDB storage, REST/SSE APIs

## Project Info

| Property | Value |
|----------|-------|
| Layer | backend |
| Framework | net10.0 |
| Workflow | explore-plan-code-commit |
| Test Coverage | 80% |

## Critical Files

| File | Reason |
|------|--------|
| Storage/DuckDbStore.cs | Core persistence - schema changes break data |
| Storage/DuckDbSchema.cs | DDL definitions - must match DuckDbStore |
| Ingestion/GenAiExtractor.cs | OTel 1.38 compliance - attribute mapping |
| Program.cs | DI registration - service configuration |
