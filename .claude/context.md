<!-- AUTO-GENERATED: Do not edit manually -->
<!-- Generated: 2025-12-17T22:54:44.0961350Z -->

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

## Vertical Slices
Features are implemented end-to-end through all layers in one pass.

```
TypeSpec → Storage → Query → API → MCP → Dashboard
                One Feature, Complete
```

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
