# qyl — AI Observability Platform

@Version.props

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Docker image IS the product.

## Core Rules

- When you learn something non-obvious, update MEMORY.md or this file.
- Always follow established coding patterns and conventions in the codebase.
- If something doesn't make sense architecturally, add it to Requests to Humans below.

## Architecture

```text
              +------------------+
              |   qyl.dashboard  |
              |    (React 19)    |
              +--------+---------+
                       | HTTP
                       v
+----------+  +------------------+  +------+
| qyl.mcp  |->|  qyl.collector   |<-| OTLP |
| (stdio)  |  |  (ASP.NET Core)  |  |Clients|
+----------+  +--------+---------+  +------+
                       |
                       v
              +------------------+
              |     DuckDB       |
              +------------------+
```

## Dependency Chain

```text
core/specs/*.tsp → qyl.protocol → qyl.collector → qyl.dashboard
                                 → qyl.mcp
                                 → qyl.servicedefaults → qyl.servicedefaults.generator
eng/build/ → orchestrates everything above
```

## Dependency Rules

```yaml
allowed:
  collector -> protocol (ProjectReference)
  mcp -> protocol (ProjectReference)
  dashboard -> collector (HTTP at runtime)
  mcp -> collector (HTTP at runtime)
forbidden:
  mcp -> collector (ProjectReference)    # must use HTTP
  protocol -> any-package                # must stay BCL-only
```

## Tech Stack (training-prior overrides)

| Layer     | Technology                                    |
|-----------|-----------------------------------------------|
| Runtime   | .NET 10.0 LTS, C# 14, net10.0                |
| Frontend  | React 19, Vite 7, Tailwind CSS 4              |
| Storage   | DuckDB (columnar, glibc required)             |
| Protocol  | OTel Semantic Conventions 1.40                |
| Testing   | xUnit v3, Microsoft Testing Platform          |
| Build     | NUKE                                          |

## Environment Variables

| Variable        | Default    | Purpose                            |
|-----------------|------------|------------------------------------|
| `QYL_PORT`      | 5100       | Dashboard + REST API port          |
| `QYL_GRPC_PORT` | 4317       | gRPC OTLP port (0=disable)         |
| `QYL_OTLP_PORT` | 4318       | HTTP OTLP port (0=disable)         |
| `QYL_DATA_PATH` | qyl.duckdb | DuckDB file path                   |
| `PORT`          | —          | Railway/PaaS fallback for QYL_PORT |

## Requests to Humans

- [ ] ...
