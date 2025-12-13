# qyl.mcp

@import "../../CLAUDE.md"

## Scope

MCP server for AI assistants. Provides tools that query `qyl.collector` via HTTP APIs (no direct DB access).

## Dependency Rules

Allowed:

- `src/qyl.protocol` (shared contracts)
- `ModelContextProtocol` SDK + .NET host infrastructure
- `System.Net.Http` (collector communication)

Forbidden:

- `src/qyl.collector` project reference
- `DuckDB.*` (collector-only)

## Communication Rule (HTTP Only)

MCP communicates with collector via HTTP only. Never read DuckDB directly.

## AOT Requirements

MCP is Native AOT.

- Use source-generated JSON via `src/qyl.mcp/Tools/TelemetryJsonContext.cs`
- Keep tools small and composable (one responsibility per tool)

## Commands

```bash
dotnet run --project src/qyl.mcp
```
