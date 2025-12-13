# qyl.collector

@import "../../CLAUDE.md"

## Scope

Backend service: receives OTLP telemetry, normalizes semconv, extracts `gen_ai.*`, persists to DuckDB, exposes REST +
SSE APIs.

## Dependency Rules

Allowed:

- `src/qyl.protocol` (ProjectReference)
- `DuckDB.NET.Data`, `Google.Protobuf`, `Grpc.AspNetCore`, `OpenTelemetry.Api`
- ASP.NET Core minimal APIs

Forbidden:

- `src/qyl.mcp` / `src/qyl.dashboard` project references
- Any direct coupling to dashboard or MCP (HTTP-only consumers)

## Owned Files

| File/Directory                                   | Responsibility               | Notes                |
|--------------------------------------------------|------------------------------|----------------------|
| `src/qyl.collector/Storage/DuckDbSchema.cs`      | DuckDB DDL + indexes         | SINGLE schema source |
| `src/qyl.collector/Storage/DuckDbStore.cs`       | Storage implementation       | Must follow schema   |
| `src/qyl.collector/Query/SessionQueryService.cs` | Session aggregation          | SQL-only (DuckDB)    |
| `src/qyl.collector/QylSerializerContext.cs`      | JSON source-gen registry     | Required for AOT     |
| `src/qyl.collector/Realtime/`                    | SSE streaming                | Fan-out + writers    |
| `src/qyl.collector/Ingestion/`                   | OTLP parsing + normalization | SemConv migrations   |

## Single Source Rules

- DuckDB schema: `src/qyl.collector/Storage/DuckDbSchema.cs`
- Session aggregation: `src/qyl.collector/Query/SessionQueryService.cs` (SQL-only)
- OTel attribute keys: `src/qyl.protocol/Attributes/GenAiAttributes.cs`

## AOT Requirements

Collector is Native AOT.

- Prefer source-generated JSON (`JsonSerializerContext`) over reflection.
- Avoid runtime codegen and dynamic loading.

## AOT Registration

If you add a new endpoint returning a new type, register it in `src/qyl.collector/QylSerializerContext.cs` or Native AOT
builds will fail.

## Forbidden Actions

- Do not create additional schema definitions outside `DuckDbSchema.cs`
- Do not add in-memory aggregation paths (aggregation is SQL in DuckDB)
- Do not introduce new runtime components/services (see root `CLAUDE.md`)

## Commands

```bash
dotnet run --project src/qyl.collector
docker compose -f eng/compose.yaml up -d qyl-collector
```
