# qyl.protocol — Shared Contracts

@../../CLAUDE.md

## Purpose

Shared types between all qyl components:
- **Primitives**: `SessionId`, `UnixNano`
- **Models**: `SpanRecord`, `SessionSummary`, `GenAiSpanData`, `TraceNode`
- **Attributes**: `GenAiAttributes` (OTel 1.38 constants)
- **Contracts**: `ISpanStore`, `ISessionAggregator`

This is a **LEAF** project — it has NO dependencies on other qyl projects.

## Hard Rules

| ✅ MAY Reference | ❌ MUST NOT Reference |
|-----------------|----------------------|
| `System.*` | `qyl.collector` |
| `Microsoft.Extensions.Primitives` | `qyl.mcp` |
| | `qyl.dashboard` |
| | `DuckDB.*` |
| | `OpenTelemetry.*` |
| | `Grpc.*` |

## Structure

```
qyl.protocol/
├── qyl.protocol.csproj
├── Primitives/
│   ├── SessionId.cs          # readonly record struct
│   └── UnixNano.cs           # readonly record struct
├── Attributes/
│   └── GenAiAttributes.cs    # OTel 1.38 gen_ai.* constants
├── Models/
│   ├── SpanRecord.cs         # Flattened span for storage
│   ├── GenAiSpanData.cs      # Extracted gen_ai.* fields
│   ├── SessionSummary.cs     # Aggregated session
│   └── TraceNode.cs          # Hierarchical trace tree
└── Contracts/
    ├── ISpanStore.cs         # Storage abstraction
    └── ISessionAggregator.cs # Aggregation abstraction
```

## Key Types

### SessionId
```csharp
public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public static SessionId Parse(string s) => new(Guid.Parse(s));
    public override string ToString() => Value.ToString("N");
}
```

**Note**: Use BCL `ActivityTraceId` and `ActivitySpanId` for trace/span IDs — don't create custom wrappers.

### GenAiAttributes
Constants for OpenTelemetry Semantic Conventions v1.38:

```csharp
public static class GenAiAttributes
{
    public const string OperationName = "gen_ai.operation.name";
    public const string ProviderName = "gen_ai.provider.name";  // NOT gen_ai.system
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";  // NOT prompt_tokens
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";  // NOT completion_tokens
    // ...
}
```

### SpanRecord
Flattened span optimized for DuckDB storage:

```csharp
public sealed record SpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required UnixNano StartTime { get; init; }
    public required UnixNano EndTime { get; init; }
    public required int Status { get; init; }  // 0=Unset, 1=Ok, 2=Error
    public GenAiSpanData? GenAi { get; init; }  // Only for gen_ai spans
}
```

## Target Frameworks

Multi-targeting for broad compatibility:
- `net10.0` — Latest features
- `net8.0` — LTS
- `netstandard2.0` — Maximum compatibility

## Publishing

Published as NuGet package: `Qyl.Protocol`

Used by:
- `qyl.collector` (project reference)
- `qyl.mcp` (project reference)
- External consumers who want type definitions
