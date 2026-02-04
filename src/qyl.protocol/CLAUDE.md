# qyl.protocol - Shared Types

BCL-only shared types. Leaf dependency with zero external packages.

## Identity

| Property   | Value                              |
|------------|------------------------------------|
| SDK        | ANcpLua.NET.Sdk                    |
| Framework  | net10.0                            |
| Constraint | **BCL-only** (no PackageReference) |

## Why BCL-Only?

This project is referenced by both `qyl.collector` and `qyl.mcp`. Keeping it dependency-free:

- Avoids version conflicts
- Enables AOT compatibility for qyl.mcp
- Reduces deployment size
- Simplifies maintenance

## Directory Structure

```
Primitives/
  Scalars.g.cs          # TraceId, SpanId, SessionId (generated)

Enums/
  Enums.g.cs            # SpanKind, StatusCode, SeverityNumber (generated)

Models/
  *.g.cs                # Record types (generated)

Attributes/
  GenAiAttributes.cs    # GenAI provider constants (manual)
```

## Generated Scalars

Strongly-typed wrappers with parsing support:

```csharp
// Implements IParsable<T>, ISpanFormattable
public readonly record struct TraceId : IParsable<TraceId>, ISpanFormattable
{
    public static TraceId Parse(string s, IFormatProvider? provider);
    public static bool TryParse(string? s, IFormatProvider? provider, out TraceId result);
    public bool TryFormat(Span<char> destination, out int charsWritten, ...);
}

public readonly record struct SpanId : IParsable<SpanId>, ISpanFormattable;
public readonly record struct SessionId : IParsable<SessionId>, ISpanFormattable;
```

## Namespaces

| Namespace    | Content           |
|--------------|-------------------|
| `Qyl.Common` | Scalar primitives |
| `Qyl.Enums`  | OTel enums        |
| `Qyl.Models` | Record types      |

## Time Handling

Protocol uses `long` for cross-platform compatibility:

```csharp
// Protocol layer: signed 64-bit
public long StartTimeUnixNano { get; init; }
public long EndTimeUnixNano { get; init; }

// Collector converts for DuckDB: unsigned 64-bit
ulong timestamp = (ulong)span.StartTimeUnixNano;
```

## Changing Types

Types are generated from TypeSpec. To modify:

1. Edit `core/specs/*.tsp`
2. Run `npm run compile` (in core/specs)
3. Run `nuke Generate --force-generate`
4. Verify generated files

## Rules

- **Never add PackageReference** - must stay BCL-only
- **Never edit** `*.g.cs` files - they are generated
- Changes go through TypeSpec, not direct edits
