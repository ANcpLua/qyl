# qyl - AI Observability Platform

**Question Your Logs** - observe everything, judge nothing, document perfectly.

## Architecture Overview

```
                      +------------------+
                      |   qyl.dashboard  |
                      |    (React 19)    |
                      +--------+---------+
                               | HTTP
                               v
+-------------+       +------------------+       +-------------+
|  qyl.mcp    |------>|  qyl.collector   |<------|    OTLP     |
| (MCP stdio) | HTTP  |  (ASP.NET Core)  | gRPC  |   Clients   |
+-------------+       +--------+---------+       +-------------+
                               |
                               v
                      +------------------+
                      |     DuckDB       |
                      | (columnar store) |
                      +------------------+
```

## Quick Start

```bash
# Development - run collector
dotnet run --project src/qyl.collector

# Development - run dashboard (separate terminal)
cd src/qyl.dashboard && npm run dev

# Production - Docker
docker run -d -p 5100:5100 -p 4317:4317 -v ~/.qyl:/data ghcr.io/ancplua/qyl:latest
```

## Build Commands

```bash
# Full build with dashboard embedding
nuke Full

# Regenerate types from TypeSpec
nuke Generate --force-generate

# Docker image
nuke DockerBuild

# Run tests
dotnet test
```

## Project Structure

| Directory | Purpose |
|-----------|---------|
| `core/` | TypeSpec schemas (source of truth) |
| `eng/` | NUKE build system |
| `src/qyl.collector/` | Backend API service |
| `src/qyl.dashboard/` | React frontend |
| `src/qyl.mcp/` | MCP server for AI agents |
| `src/qyl.protocol/` | Shared types (BCL-only) |
| `src/qyl.servicedefaults/` | Aspire-style defaults |
| `src/qyl.servicedefaults.generator/` | GenAI interceptor generator |
| `src/qyl.Analyzers/` | Roslyn analyzers (QYL001-015) |
| `src/qyl.Analyzers.CodeFixes/` | Code fix providers |
| `src/qyl.copilot/` | GitHub Copilot integration |
| `tests/` | Test projects |

## TypeSpec-First Design

All types are defined in TypeSpec (`core/specs/`) and generated downstream:

```
core/specs/*.tsp
       |
       v (tsp compile)
core/openapi/openapi.yaml
       |
       v (nuke Generate)
+------+------+------+
|      |      |      |
v      v      v      v
C#    DuckDB  TS    JSON
```

**Rule**: Never edit `*.g.cs` or `api.ts` - edit TypeSpec and regenerate.

## Component Dependencies

```yaml
allowed:
  collector -> protocol (ProjectReference)
  mcp -> protocol (ProjectReference)
  dashboard -> collector (HTTP runtime)
  mcp -> collector (HTTP runtime)

forbidden:
  mcp -> collector (ProjectReference)  # must use HTTP
  protocol -> any-package              # must stay BCL-only
```

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10.0 LTS, C# 14 |
| Frontend | React 19, Vite 7, Tailwind CSS 4 |
| Storage | DuckDB (columnar, glibc required) |
| Protocol | OpenTelemetry Semantic Conventions 1.39 |
| Testing | xUnit v3, Microsoft Testing Platform |

## Key Patterns

### Time Handling

```csharp
// Protocol layer (cross-platform): long (signed 64-bit)
long timestampNanos = ...;

// Collector layer (DuckDB): ulong (unsigned 64-bit)
ulong storedTimestamp = (ulong)timestampNanos;
```

### Locking

```csharp
// Sync context
private readonly Lock _lock = new();
using (_lock.EnterScope()) { /* sync only */ }

// Async context
private readonly SemaphoreSlim _asyncLock = new(1, 1);
await _asyncLock.WaitAsync(ct);
```

### JSON Options (CA1869)

```csharp
private static readonly JsonSerializerOptions s_options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5100 | HTTP | REST API, SSE, Dashboard |
| 4317 | gRPC | OTLP traces/logs/metrics |
| 5173 | HTTP | Dashboard dev server |

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | 5100 | HTTP API port |
| `QYL_GRPC_PORT` | 4317 | gRPC OTLP port (0 to disable) |
| `QYL_DATA_PATH` | ./qyl.duckdb | DuckDB file location |

## Documentation Map

| File | Purpose |
|------|---------|
| `.claude/rules/architecture-rules.md` | Type ownership, dependencies |
| `.claude/rules/coding-patterns.md` | .NET 10 patterns, banned APIs |
| `.claude/rules/genai-semconv.md` | OTel 1.39 GenAI conventions |
| `.claude/rules/build-workflow.md` | NUKE targets, Docker build |
| `core/CLAUDE.md` | TypeSpec schema details |
| `eng/CLAUDE.md` | Build system |
| `src/*/CLAUDE.md` | Component-specific docs |
