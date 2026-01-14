# qyl.protocol

LEAF library - shared contracts layer. BCL dependencies ONLY.

## Role

Single source of truth for types consumed by multiple projects. All shared types live here; implementation details stay
in consuming projects.

## Type Inventory

### Primitives/ (Generated)

| Type        | Underlying        | Purpose                            |
|-------------|-------------------|------------------------------------|
| `SessionId` | `Guid`            | AI conversation session identifier |
| `UnixNano`  | `ulong`           | OTel wire format timestamp         |
| `TraceId`   | `ActivityTraceId` | 32-char hex trace identifier       |
| `SpanId`    | `ActivitySpanId`  | 16-char hex span identifier        |

All primitives implement `ISpanParsable<T>`, `IEquatable<T>`, `IComparable<T>`.

### Models/

| Type             | Purpose                                   |
|------------------|-------------------------------------------|
| `SpanRecord`     | Core span representation (storage + API)  |
| `SessionSummary` | Aggregated session metrics                |
| `TraceNode`      | Hierarchical trace tree node              |
| `GenAiSpanData`  | Extracted gen_ai.* attributes (OTel 1.39) |

### Contracts/

| Interface            | Implementor     | Purpose                     |
|----------------------|-----------------|-----------------------------|
| `ISpanStore`         | `qyl.collector` | Span CRUD operations        |
| `ISessionAggregator` | `qyl.collector` | Session aggregation queries |

### Attributes/

`GenAiAttributes` - OTel 1.39 semantic convention constants with:

- Current attribute keys (`GenAiProviderName`, `GenAiUsageInputTokens`, etc.)
- Deprecated keys with `[Obsolete]` markers
- `AllKeys` frozen set for validation
- `Migrations` dictionary for deprecated-to-current mapping
- `Normalize()` method for automatic migration

## Generated Files

Files matching `*.g.cs` are generated from `eng/build/Domain/CodeGen/QylSchema.cs`.

**Never edit manually.** Run `nuke Generate` to regenerate.

## Constraints

| Rule                   | Rationale                                |
|------------------------|------------------------------------------|
| BCL only               | Leaf dependency - no external packages   |
| No implementation      | Contracts define, collector implements   |
| AOT compatible         | `IsAotCompatible=true` in csproj         |
| Token types use `long` | Matches DuckDB BIGINT, prevents overflow |

## Known Issues

**ARCH-001**: If `SessionSummary` appears in `qyl.collector`, delete it - this is the authoritative location.
