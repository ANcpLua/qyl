# qyl AI Observability Platform

AI-focused OpenTelemetry backend for gen_ai.* semantic conventions. Built on .NET 10 / C# 14.

## Architecture

```
User App ──OTLP──► qyl.collector ──DuckDB──► Storage
                        │                       │
                        │                       └──► REST/SSE ──► qyl.dashboard
                        │
                        └──► REST ──► qyl.mcp ──stdio──► Claude/AI Agent
```

## Projects (4 Fixed)

| Project         | Type    | Purpose                   | Dependencies                                   |
|-----------------|---------|---------------------------|------------------------------------------------|
| `qyl.protocol`  | Library | Shared contracts (LEAF)   | BCL only                                       |
| `qyl.collector` | Web API | Backend, DuckDB, REST/SSE | protocol, DuckDB.NET.Data.Full@1.4.3           |
| `qyl.mcp`       | Console | MCP Server for AI Agents  | protocol, ModelContextProtocol                 |
| `qyl.dashboard` | SPA     | React 19 Frontend         | React 19, Vite 6, Tailwind 4, TanStack Query 5 |

## Dependency Rules

```
dashboard ──HTTP──► collector ◄──HTTP── mcp
                        │
                        ▼
                    protocol
```

**Critical:** `qyl.mcp` communicates with `qyl.collector` via HTTP ONLY. No ProjectReference allowed.

## Tech Stack

- **Runtime:** .NET 10 / C# 14
- **SDK:** ANcpLua.NET.Sdk@1.1.8
- **Storage:** DuckDB.NET.Data.Full@1.4.3
- **OTel:** Semantic Conventions v1.38.0
- **Frontend:** React 19, Vite 6, Tailwind 4, TanStack Query 5

---

## ⚠️ TYPE OWNERSHIP (MANDATORY)

### Golden Rule

> **If a type is needed by MORE than one project → `qyl.protocol`**  
> **If a type is needed by ONLY one project → that project**

### Ownership Matrix

| Owner           | Types                                     | Consumers                   |
|-----------------|-------------------------------------------|-----------------------------|
| `qyl.protocol`  | SessionId, UnixNano, TraceId, SpanId      | collector, mcp              |
| `qyl.protocol`  | SpanRecord, SessionSummary, TraceNode     | collector, mcp, dashboard   |
| `qyl.protocol`  | GenAiSpanData, GenAiAttributes            | collector, mcp              |
| `qyl.protocol`  | ISpanStore, ISessionAggregator            | collector (implements)      |
| `qyl.collector` | DuckDbStore, DuckDbSchema, SpanStorageRow | INTERNAL ONLY               |
| `qyl.collector` | OtlpJsonSpanParser, OtlpTypes             | INTERNAL ONLY               |
| `qyl.dashboard` | types/generated/*                         | 🔧 Generated from QylSchema |

### Decision Tree

```
New Type Needed?
├── Used by collector only? → collector/Storage/ or collector/Ingestion/
├── Used by mcp only? → mcp/
├── Used by dashboard only? → dashboard/src/
└── Used by multiple projects? → protocol/
```

### Common Mistakes (BANNED)

| ❌ Wrong                             | ✅ Correct                                |
|-------------------------------------|------------------------------------------|
| `collector/Primitives/SessionId.cs` | `protocol/Primitives/SessionId.cs`       |
| `collector/GenAiAttributes.cs`      | `protocol/Attributes/GenAiAttributes.cs` |
| Editing `*.g.cs` files              | Edit `QylSchema.cs`, run `nuke Generate` |

---

## Vertical Slices

Features are implemented end-to-end through all layers in one pass.

```
TypeSpec → Storage → Query → API → MCP → Dashboard
                One Feature, Complete
```

### Slice Registry

| ID    | Name            | Priority | Status      | ADR                                                              |
|-------|-----------------|----------|-------------|------------------------------------------------------------------|
| VS-01 | Span Ingestion  | P0       | PARTIAL     | [0002](docs/architecture/decisions/0002-vs01-span-ingestion.md)  |
| VS-02 | List Sessions   | P0       | IN_PROGRESS | [0003](docs/architecture/decisions/0003-vs02-list-sessions.md)   |
| VS-03 | View Trace Tree | P1       | NOT_STARTED | [0004](docs/architecture/decisions/0004-vs03-view-trace-tree.md) |
| VS-04 | GenAI Analytics | P1       | NOT_STARTED | [0005](docs/architecture/decisions/0005-vs04-genai-analytics.md) |
| VS-05 | Live Streaming  | P2       | PARTIAL     | [0006](docs/architecture/decisions/0006-vs05-live-streaming.md)  |
| VS-06 | MCP Query Tool  | P2       | NOT_STARTED | [0007](docs/architecture/decisions/0007-vs06-mcp-query-tool.md)  |

### Dependency Graph

```
VS-01 ──► VS-02 ──► VS-04
  │         └────► VS-06
  ├──► VS-03
  └──► VS-05
```

### Implementation Order

| Phase              | Slices       | Goal                                   |
|--------------------|--------------|----------------------------------------|
| P0 (Foundation)    | VS-01, VS-02 | Core ingestion + session listing       |
| P1 (Core Features) | VS-03, VS-04 | Trace visualization + GenAI analytics  |
| P2 (Enhancement)   | VS-05, VS-06 | Real-time streaming + AI agent tooling |

---

## DuckDB 1.4.3 API

### Package

```xml
<PackageReference Include="DuckDB.NET.Data.Full" Version="1.4.3" />
```

Use `.Full` package — includes bundled native DuckDB binaries.

### Infinity Date Handling (1.4.3 Feature)

DuckDB 1.4.3 supports ±Infinity dates. **Must check before conversion:**

```csharp
var duckDate = reader.GetFieldValue<DuckDBDateOnly>(i);

if (duckDate.IsInfinity || duckDate.IsPositiveInfinity || duckDate.IsNegativeInfinity)
{
    // Cannot convert to .NET DateTime/DateOnly - handle specially
    return null; // or DateTimeOffset.MaxValue
}
else
{
    DateOnly netDate = duckDate;  // Safe conversion
}
```

### Connection Patterns

```csharp
// Standard connection
using var connection = new DuckDBConnection("DataSource=qyl.duckdb");
connection.Open();

// Read-only connection (parallel reads)
using var readConn = new DuckDBConnection("DataSource=qyl.duckdb;ACCESS_MODE=READ_ONLY");
```

### OTel 1.38 Column Names

Use quoted identifiers for OTel semantic convention columns:

```sql
-- Correct
SELECT "session.id", "gen_ai.provider.name", "gen_ai.usage.input_tokens"
FROM spans
WHERE "gen_ai.provider.name" IS NOT NULL

-- Wrong (will fail)
SELECT session.id, gen_ai.provider.name
```

### Token Types

- **Column:** `BIGINT` (not INT)
- **C#:** `long` (not int)

---

## Code Generation (QylSchema)

### Single Source of Truth

```
eng/build/Domain/CodeGen/QylSchema.cs
         │
         ├──► CSharpGenerator    → protocol/*.g.cs
         ├──► TypeScriptGenerator → dashboard/types/generated/
         └──► DuckDbGenerator    → collector/Storage/DuckDbSchema.g.cs
```

### Commands

```bash
nuke Generate                    # All generators
nuke Generate --ForceGenerate    # Overwrite existing
nuke Generate --DryRunGenerate   # Preview only
```

### Generated Files (DO NOT EDIT)

| Pattern             | Location                       | Source       |
|---------------------|--------------------------------|--------------|
| `*.g.cs`            | protocol/, collector/Storage/  | QylSchema.cs |
| `models.ts`         | dashboard/src/types/generated/ | QylSchema.cs |
| `DuckDbSchema.g.cs` | collector/Storage/             | QylSchema.cs |

**To change generated code:** Edit `QylSchema.cs`, then `nuke Generate`.

---

## Single Source of Truth

| Resource                | Source                                       | Rule                                 |
|-------------------------|----------------------------------------------|--------------------------------------|
| **Type Definitions**    | `eng/build/Domain/CodeGen/QylSchema.cs`      | ALL model definitions                |
| **Shared Primitives**   | `qyl.protocol/Primitives/`                   | SessionId, UnixNano, TraceId, SpanId |
| **Shared Models**       | `qyl.protocol/Models/`                       | SpanRecord, SessionSummary, etc.     |
| **OTel Constants**      | `qyl.protocol/Attributes/GenAiAttributes.cs` | ALL gen_ai.* strings                 |
| **Storage Internals**   | `qyl.collector/Storage/`                     | DuckDB-specific types                |
| **TypeScript Types**    | `qyl.dashboard/src/types/generated/`         | 🔧 Generated - DO NOT EDIT           |
| **DuckDB DDL**          | `qyl.collector/Storage/DuckDbSchema.cs`      | ALL table definitions                |
| **Session Aggregation** | `qyl.collector/Query/SessionQueryService.cs` | ALL aggregation SQL                  |

---

## Validated Patterns (95% Spec Compliance)

### Concurrency - Lock vs SemaphoreSlim

```csharp
// SYNC code: Use .NET 9+ Lock
private readonly Lock _lock = new();

using (_lock.EnterScope())
{
    // Critical section (NO await!)
}

// ASYNC code: Use SemaphoreSlim(1,1)
private readonly SemaphoreSlim _asyncLock = new(1, 1);

await _asyncLock.WaitAsync(ct);
try { await SomeAsyncOp(ct); }
finally { _asyncLock.Release(); }
```

### Hot Paths - ValueTask + Channel

```csharp
// Bounded Channel with Backpressure
Channel.CreateBounded<SpanRecord>(new BoundedChannelOptions(10_000)
{
    FullMode = BoundedChannelFullMode.Wait
});

// ValueTask for hot path
public ValueTask PublishAsync(SpanRecord span, CancellationToken ct = default)
    => _channel.Writer.WriteAsync(span, ct);

// IAsyncEnumerable for streaming
public IAsyncEnumerable<SpanRecord> SubscribeAsync(CancellationToken ct = default)
    => _channel.Reader.ReadAllAsync(ct);
```

### OTel 1.38 Migration

```csharp
// Fallback from deprecated gen_ai.system to gen_ai.provider.name
Provider = attrs.GetValueOrDefault(GenAiAttributes.ProviderName)?.StringValue
        ?? attrs.GetValueOrDefault(GenAiAttributes.System)?.StringValue,
```

### MCP HTTP-Only Communication

```csharp
// QylCollectorClient uses HttpClient, NO ProjectReference
public sealed class QylCollectorClient(HttpClient http)
{
    public async Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(...)
    {
        return await http.GetFromJsonAsync<List<SpanRecord>>(url, ct) ?? [];
    }
}
```

---

## Banned APIs (Build Errors)

| Banned                      | Use Instead                       |
|-----------------------------|-----------------------------------|
| `DateTime.Now/UtcNow`       | `TimeProvider.System.GetUtcNow()` |
| `DateTimeOffset.Now/UtcNow` | `TimeProvider.System.GetUtcNow()` |
| `object _lock`              | `Lock _lock = new()`              |
| `Monitor.Enter/Exit`        | `Lock.EnterScope()`               |
| `Newtonsoft.Json`           | `System.Text.Json`                |
| `Task.Delay(int)`           | `TimeProvider.Delay()`            |

---

## Rejected Alternatives

Documented decisions to avoid re-discussion:

| Idea                          | Rejected Because                                              |
|-------------------------------|---------------------------------------------------------------|
| ZLinq for qyl                 | Wrong bottleneck — DuckDB I/O is the limit, not LINQ          |
| Custom TraceId/SpanId structs | BCL ActivityTraceId/ActivitySpanId are sufficient             |
| Qyl.Sdk NuGet for apps        | Users should use standard OpenTelemetry — qyl is backend only |
| qyl.telemetry as project      | Redundant — content merged into qyl.protocol                  |
| TypeSpec for C# primitives    | C#-specific patterns (ISpanParsable) cannot be expressed      |
| Full TypeSpec code generation | Overkill for 4 projects — hybrid approach is better           |

---

## Testing (xUnit v3 + MTP)

### Packages

| Package                                     | Purpose                     |
|---------------------------------------------|-----------------------------|
| `xunit.v3`                                  | Test framework + MTP runner |
| `Microsoft.Testing.Extensions.TrxReport`    | TRX reports                 |
| `Microsoft.Testing.Extensions.CodeCoverage` | Native coverage             |

### Run Tests

```bash
# Via Nuke (recommended)
./eng/build.sh Test
./eng/build.sh Coverage

# Direct execution
./tests/qyl.collector.Tests/bin/Debug/net10.0/qyl.collector.Tests

# Filter tests
./tests/qyl.collector.Tests/bin/Debug/net10.0/qyl.collector.Tests \
  --filter-namespace "*.Storage.*"
```

### Key xUnit v3 Pattern

```csharp
// IAsyncLifetime uses ValueTask (not Task)
public sealed class MyTests : IAsyncLifetime
{
    public async ValueTask InitializeAsync() { }
    public async ValueTask DisposeAsync() { }
}
```

---

## Build Tooling (NUKE 10.1)

### Build Commands

```bash
nuke Compile                # Build all projects
nuke Test                   # Run tests (MTP)
nuke Coverage               # Tests + coverage report
nuke Generate               # Generate from QylSchema
nuke Changelog              # Preview CHANGELOG (dry run)
nuke Release                # Bump version + CHANGELOG + tag
```

### MTP Test Arguments

```csharp
var mtp = MtpExtensions.Mtp()
    .ResultsDirectory(TestResultsDirectory)
    .ReportTrx("results.trx")
    .FilterNamespace("*.Unit.*")
    .CoverageCobertura(coverageOutput);
```

---

## Code Review Checklist

| Pattern                   | Location                          | Status    |
|---------------------------|-----------------------------------|-----------|
| `Lock _lock = new()`      | DuckDbStore.cs                    | Validated |
| ValueTask for hot path    | SpanBroadcaster.PublishAsync      | Validated |
| IAsyncEnumerable<T>       | SpanBroadcaster.SubscribeAsync    | Validated |
| Channel<T> bounded        | BoundedChannelOptions(10_000)     | Validated |
| HTTP-ONLY for MCP         | QylCollectorClient via HttpClient | Validated |
| No duplicate types        | Each type in ONE project only     | Validated |
| Generated files untouched | *.g.cs files not manually edited  | Validated |

---

## Quick Reference

**Run Services:**

```bash
# Collector (API: 5100, gRPC: 4317, OTLP: 4318)
dotnet run --project src/qyl.collector

# Dashboard (Port: 5173)
npm run dev --prefix src/qyl.dashboard
```

**Generate Code:**

```bash
nuke Generate --ForceGenerate
```

---

MIT License
