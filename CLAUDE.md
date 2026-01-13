# qyl — AI Observability Platform

OpenTelemetry backend for `gen_ai.*` semantic conventions. .NET 10 / C# 14.

## Architecture

```
User App ──OTLP──► qyl.collector ──DuckDB──► Storage
                        │                       │
                        │                       └──► REST/SSE ──► qyl.dashboard
                        └──► REST ──► qyl.mcp ──stdio──► Claude
```

| Project | Type | Purpose | Dependencies |
|---------|------|---------|--------------|
| `qyl.protocol` | Library | Shared contracts (LEAF) | BCL only |
| `qyl.collector` | Web API | OTLP ingestion, DuckDB, REST/SSE | protocol |
| `qyl.mcp` | Console | MCP Server for AI agents | protocol (HTTP to collector) |
| `qyl.dashboard` | SPA | React 19 frontend | REST/SSE from collector |

---

## Dependency Rules (CRITICAL)

```
dashboard ──HTTP──► collector ◄──HTTP── mcp
                        │
                        ▼
                    protocol
```

| Rule | Constraint |
|------|------------|
| `qyl.protocol` | BCL only. No external packages. Leaf dependency. |
| `qyl.mcp` → `qyl.collector` | **HTTP ONLY**. No ProjectReference. |
| `qyl.dashboard` → `qyl.collector` | REST/SSE endpoints only |

---

## Type Ownership

**Golden Rule**: Multiple consumers → `qyl.protocol`. Single consumer → that project.

| Owner | Types | Consumers |
|-------|-------|-----------|
| `qyl.protocol` | SessionId, UnixNano, TraceId, SpanId | all |
| `qyl.protocol` | SpanRecord, SessionSummary, TraceNode | all |
| `qyl.protocol` | GenAiSpanData, GenAiAttributes | collector, mcp |
| `qyl.collector` | DuckDbStore, DuckDbSchema, OtlpJsonSpanParser | internal only |
| `qyl.dashboard` | types/generated/* | internal only |

### New Type Decision Tree

```
New Type?
├── Used by collector only? → collector/Storage/ or collector/Ingestion/
├── Used by mcp only? → mcp/
├── Used by dashboard only? → dashboard/src/types/
└── Used by 2+ projects? → protocol/
```

---

## Banned APIs

See `eng/MSBuild/BannedSymbols.txt` for full list.

| Category | Required Pattern |
|----------|------------------|
| Time | `TimeProvider.System.GetUtcNow()` |
| Locking | `Lock _lock = new()` |
| JSON | `System.Text.Json` |
| String search | `string.Contains('x')` (char overload, CA1847) |
| Hex convert | `Convert.ToHexString()` (CA1872) |

---

## Required Patterns

### Locking

```csharp
// SYNC: Use .NET 9+ Lock
private readonly Lock _lock = new();
using (_lock.EnterScope()) { /* NO await */ }

// ASYNC: Use SemaphoreSlim
private readonly SemaphoreSlim _asyncLock = new(1, 1);
await _asyncLock.WaitAsync(ct);
try { await Op(ct); }
finally { _asyncLock.Release(); }
```

### Hot Paths

```csharp
// Bounded channel with backpressure
Channel.CreateBounded<SpanRecord>(new BoundedChannelOptions(10_000)
{
    FullMode = BoundedChannelFullMode.Wait
});

// ValueTask for hot paths
public ValueTask PublishAsync(SpanRecord span, CancellationToken ct = default)
    => _channel.Writer.WriteAsync(span, ct);
```

### JSON Serializer Options

```csharp
// Cache as static readonly (CA1869)
private static readonly JsonSerializerOptions s_options = new() { /* ... */ };
```

---

## Code Generation

**Single Source of Truth**: `eng/build/Domain/CodeGen/QylSchema.cs`

| Generator | Output | Location |
|-----------|--------|----------|
| CSharpGenerator | `*.g.cs` | protocol/, collector/Storage/ |
| TypeScriptGenerator | `models.ts` | dashboard/src/types/generated/ |
| DuckDbGenerator | `DuckDbSchema.g.cs` | collector/Storage/ |

```bash
nuke Generate                 # All generators
nuke Generate --ForceGenerate # Overwrite existing
```

**Never edit `*.g.cs` files manually.**

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 / C# 14 |
| SDK | ANcpLua.NET.Sdk 1.6.2 (from nuget.org) |
| Storage | DuckDB.NET.Data.Full |
| OTel | Semantic Conventions v1.38.0 |
| Frontend | React 19, Vite 6, Tailwind 4, TanStack Query 5 |
| Testing | xUnit v3 + Microsoft Testing Platform (MTP) |

---

## Testing (xUnit v3 + MTP)

```bash
./eng/build.sh Test      # Run all tests
./eng/build.sh Coverage  # With coverage
```

### IAsyncLifetime Pattern

```csharp
// xUnit v3: ValueTask not Task
public sealed class MyTests : IAsyncLifetime
{
    public async ValueTask InitializeAsync() { }
    public async ValueTask DisposeAsync() { }
}
```

---

## Quick Commands

```bash
# Collector (HTTP: 5100, gRPC: 4317)
dotnet run --project src/qyl.collector

# Dashboard (Port: 5173)
cd src/qyl.dashboard && npm run dev

# Tests
dotnet test

# Code generation
nuke Generate --ForceGenerate
```

---

## Warning Suppressions

Use pragmas with clear comments for intentional violations:

```csharp
#pragma warning disable CA1720 // Identifiers intentionally match OTel type names
public enum AttributeValueType { String, Int, Long, Double, Boolean }
#pragma warning restore CA1720
```

---

## Project CLAUDE.md Files

Each component has its own CLAUDE.md with component-specific rules:

- `src/qyl.collector/CLAUDE.md` — Storage, ingestion, API details
- `src/qyl.mcp/CLAUDE.md` — MCP tools, HTTP client patterns
- `src/qyl.dashboard/CLAUDE.md` — React patterns, type generation
- `src/qyl.protocol/CLAUDE.md` — Type inventory, constraints
- `tests/CLAUDE.md` — Test conventions
- `eng/CLAUDE.md` — Build system, generators

---

MIT License
