# qyl.collector

@import "../../CLAUDE.md"

## Scope

OTLP ingestion, DuckDB storage, REST/SSE APIs for the qyl observability platform.

## Project Info

| Property | Value |
|----------|-------|
| Layer | Web API |
| Framework | net10.0 |
| Storage | DuckDB (in-process) |
| Schema | OTel Semantic Conventions v1.39 |

## Critical Files

| File | Reason |
|------|--------|
| Storage/DuckDbStore.cs | Core persistence - schema changes break data |
| Storage/DuckDbSchema.g.cs | Generated DDL - regenerate with `nuke Generate` |
| Query/SpanQueryBuilder.cs | SQL builder with SpanColumn enum |
| Program.cs | DI registration - service configuration |

## Schema (v20260113)

### Spans Table Columns

| Column | Type | Description |
|--------|------|-------------|
| `span_id` | VARCHAR(16) | Primary key |
| `trace_id` | VARCHAR(32) | Trace identifier |
| `start_time_unix_nano` | UBIGINT | Start timestamp in nanoseconds |
| `end_time_unix_nano` | UBIGINT | End timestamp in nanoseconds |
| `duration_ns` | UBIGINT | Duration in nanoseconds |
| `kind` | TINYINT | Span kind (0-5) |
| `status_code` | TINYINT | Status (0=UNSET, 1=OK, 2=ERROR) |
| `gen_ai_system` | VARCHAR | GenAI provider (openai, anthropic, etc.) |
| `gen_ai_input_tokens` | BIGINT | Input token count |
| `gen_ai_output_tokens` | BIGINT | Output token count |
| `gen_ai_cost_usd` | DOUBLE | Cost in USD |
| `attributes_json` | VARCHAR | Non-promoted attributes |
| `resource_json` | VARCHAR | Resource attributes |

### Key Patterns

- Timestamps: Always `ulong` nanoseconds (not DateTime)
- Enum fields: `byte` (Kind, StatusCode)
- Cost: `double?` (not decimal)
- GenAI prefix: `gen_ai_*` columns (OTel v1.39 naming)
