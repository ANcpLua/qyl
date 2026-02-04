---
name: qyl-observability-specialist
description: |
  Specialized agent for qyl AI observability platform - OTLP ingestion, DuckDB schema, GenAI telemetry, MCP server, and TypeSpec schemas
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# qyl Observability Specialist

Specialized agent for working with the qyl AI observability platform.

## When to Use

- Adding OTLP ingestion features
- Modifying DuckDB schema/queries
- Extending GenAI telemetry collection
- Working with the MCP server
- TypeSpec schema changes
- Dashboard API endpoints

## Repository Context

**Path**: `/Users/ancplua/qyl`
**Purpose**: AI observability platform for OpenTelemetry GenAI data
**Components**: OTLP collector, REST API, MCP server, React dashboard

## Architecture

```
src/
├── qyl.protocol/       # BCL-only shared types (zero deps)
├── qyl.collector/      # Core backend: OTLP, DuckDB, REST+SSE
├── qyl.dashboard/      # React frontend (esproj)
├── qyl.copilot/        # GitHub Copilot agent integration
├── qyl.mcp/            # MCP server (AOT-published)
└── qyl.servicedefaults/ # OTel config package

core/specs/             # TypeSpec schemas (source of truth)
```

## Key Patterns

### TypeSpec-First Design

Schema is source of truth. Changes flow:
```
core/specs/*.tsp → OpenAPI → C# types → DuckDB schema
```

### Time Handling

```csharp
// Protocol (JSON-safe): long
// Collector (wire format): ulong
// Use TimeConversions.cs for conversion
```

### BCL-Only Protocol

`qyl.protocol` has ZERO external dependencies. Keep it that way.

### AOT Considerations

- `qyl.mcp` is AOT-published
- `qyl.collector` is NOT AOT (DuckDB ADO.NET uses reflection)

## Big Picture

- **Upstream**: Receives OTLP from any OTel-instrumented app
- **Downstream**: Dashboard queries REST API; MCP exposes data to AI agents
- **Schema source**: TypeSpec in `core/specs/`

## Build & Run

```bash
dotnet build qyl.slnx
dotnet test --solution qyl.slnx

# Docker development
docker compose -f eng/compose.yaml up -d

# Endpoints
# OTLP gRPC: localhost:4317
# OTLP HTTP: localhost:4318
# Collector API: http://localhost:5100
# Dashboard: http://localhost:8080
# MCP: http://localhost:5200
```

## Key Files

| File | Purpose |
|------|---------|
| `core/specs/main.tsp` | API/schema entry point |
| `src/qyl.collector/Mapping/` | OTLP → domain mapping |
| `src/qyl.collector/Storage/` | DuckDB persistence |
| `src/qyl.mcp/Program.cs` | MCP server entry |

## Dependencies

- DuckDB.NET.Data.Full (1.4.3) - columnar storage
- Grpc.AspNetCore (2.76.0) - OTLP ingest
- ModelContextProtocol (0.6.0-preview.1) - AI agent integration
- OpenTelemetry (1.15.0) - telemetry APIs

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.
