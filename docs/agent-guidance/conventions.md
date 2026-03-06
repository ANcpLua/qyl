# Conventions and project constraints

## Coding constraints (high impact)

- Keep protocol and schema aligned:
  - `core/specs/*.tsp` drives generated code in protocol/collector/dashboard APIs.
  - Avoid edits in generated files outside explicit generator inputs.
- Observability conventions:
  - OTel Semantic Conventions v1.40.
  - Instrumentation patterns come from `qyl.servicedefaults` and instrumentation generators.
- Store constraints:
  - DuckDB schema and migrations are generated/managed by `eng/build` and migration scripts.

## Component rules to apply by path

- `src/qyl.collector/`  
  - REST + gRPC OTLP collector; DuckDB storage + single-writer patterns.
- `src/qyl.copilot/`  
  - AG-UI and workflow orchestration; protocol-only coupling to collector via HTTP.
- `src/qyl.mcp/`  
  - stdio transport, HTTP-to-collector boundary is mandatory.
- `src/qyl.protocol/`  
  - BCL-only project, no external package references.
- `src/qyl.dashboard/`  
  - Typed API client generation from OpenAPI first; shadcn/ui components are treated as generated unless explicitly custom.

## Environment references

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | `5100` | Dashboard + REST API |
| `QYL_GRPC_PORT` | `4317` | OTLP gRPC |
| `QYL_OTLP_PORT` | `4318` | OTLP HTTP |
| `QYL_DATA_PATH` | `qyl.duckdb` | DuckDB file path |
| `PORT` | fallback | Platform fallback for `QYL_PORT` |

## Cross-file instruction map

- Additional rules exist per component in `.claude/rules/`:
  - `build-system.md`
  - `collector.md`
  - `mcp-server.md`
  - `protocol.md`
  - `frontend.md`
  - `testing.md`
  - `roslyn-generators.md`
