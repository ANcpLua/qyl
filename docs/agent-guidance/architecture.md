# Architecture and dependency rules

## Project architecture

- `qyl.collector` is the central ASP.NET host (HTTP + gRPC OTLP + SSE + dashboard embedding).
- `qyl.copilot` is a library that exposes AG-UI and workflow behavior.
- `qyl.mcp` is a stdio MCP server and must not project-reference `qyl.collector`.
- `qyl.protocol` is shared contracts only and is BCL-only.
- `qyl.servicedefaults` and `qyl.servicedefaults.generator` are shared instrumentation layers.
- `eng/build/` orchestrates TypeSpec, schema generation, and pipeline verification.

## Dependency chain (intentional)

```text
core/specs/*.tsp -> qyl.protocol -> qyl.collector -> qyl.dashboard
                                   -> qyl.mcp
                                   -> qyl.copilot (AG-UI + declarative workflows)
                                   -> qyl.servicedefaults -> qyl.servicedefaults.generator
eng/build/ -> orchestrates everything above
```

## Allowed/forbidden references

```yaml
allowed:
  collector -> protocol (ProjectReference)
  collector -> copilot (ProjectReference)
  mcp -> protocol (ProjectReference)
  copilot -> protocol (ProjectReference)
  dashboard -> collector (HTTP at runtime)
  mcp -> collector (HTTP at runtime)
forbidden:
  mcp -> collector (ProjectReference)    # must use HTTP
  protocol -> any-package                # protocol must stay BCL-only
  copilot -> collector (ProjectReference) # copilot is a library, not a host
```

## Runtime composition notes

- Docker image is the primary delivery artifact (`src/qyl.collector/Dockerfile`).
- Core ports:
  - `QYL_PORT`: `5100`
  - `QYL_GRPC_PORT`: `4317`
  - `QYL_OTLP_PORT`: `4318`
  - `PORT`: optional platform fallback for `QYL_PORT`
- Main data path default: `qyl.duckdb` / `QYL_DATA_PATH`.

## Design references

- `docs/plans/2026-03-03-qyl-agui-declarative-design.md`
- `docs/plans/2026-03-03-qyl-agui-declarative-impl.md`
- `docs/roadmap/loom-design.md`
