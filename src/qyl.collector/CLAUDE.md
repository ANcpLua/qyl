# qyl.collector

Inherits: [Root CLAUDE.md](../../CLAUDE.md)

OpenTelemetry backend for GenAI observability. Ingests OTLP, stores in DuckDB, exposes REST/SSE APIs.

## Architecture

```
OTLP (gRPC/HTTP) ──► Ingestion ──► DuckDB ──► REST/SSE ──► Dashboard
       │                                          │
       └──► MCP Server ◄──────────────────────────┘
```

## Project Info

| Property  | Value                    |
|-----------|--------------------------|
| Layer     | Backend API              |
| Framework | net10.0                  |
| Ports     | 5100 (HTTP), 4317 (gRPC) |
| Storage   | DuckDB (embedded)        |

## Directory Structure

| Directory    | Purpose                                 |
|--------------|-----------------------------------------|
| `Ingestion/` | OTLP parsing, attribute normalization   |
| `Storage/`   | DuckDB schema, store, reader extensions |
| `Query/`     | Session/span query services             |
| `Endpoints/` | REST API endpoints                      |
| `Realtime/`  | SSE live streaming                      |
| `Mcp/`       | MCP server for AI agents                |
| `Grpc/`      | gRPC OTLP receiver                      |
| `Telemetry/` | Self-instrumentation                    |

## Critical Files

| File                              | Reason                                       |
|-----------------------------------|----------------------------------------------|
| `Storage/DuckDbStore.cs`          | Core persistence - schema changes break data |
| `Storage/DuckDbSchema.cs`         | Manual DDL extensions                        |
| `Storage/DuckDbSchema.g.cs`       | Generated DDL from TypeSpec                  |
| `Ingestion/OtlpJsonSpanParser.cs` | OTLP/JSON parsing - hot path                 |
| `Ingestion/SemconvNormalizer.cs`  | OTel attribute normalization                 |
| `Program.cs`                      | DI registration, endpoint mapping            |

## Key Patterns

### Span Ingestion Flow

```
OTLP Request → OtlpJsonSpanParser → SemconvNormalizer → SpanRecord → DuckDbStore
```

### Generated vs Manual Code

| Type                | Source                                      |
|---------------------|---------------------------------------------|
| `DuckDbSchema.g.cs` | Generated from TypeSpec via `nuke Generate` |
| `DuckDbSchema.cs`   | Manual extensions (indexes, views)          |
| `CollectorTypes.cs` | Internal ingestion types                    |

## Commands

```bash
# Run collector
dotnet run --project src/qyl.collector

# Run with hot reload
dotnet watch --project src/qyl.collector

# Docker
docker compose -f src/qyl.collector/docker-compose.yml up
```
