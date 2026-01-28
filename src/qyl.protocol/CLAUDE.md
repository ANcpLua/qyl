# qyl.protocol

BCL-only shared types. Leaf dependency with zero external packages.

## identity

```yaml
sdk: ANcpLua.NET.Sdk
constraint: BCL-only (no PackageReference)
role: shared-types
```

## structure

```yaml
Primitives/
  Scalars.g.cs    # TraceId, SpanId, SessionId (generated)

Enums/
  Enums.g.cs      # SpanKind, StatusCode, SeverityNumber (generated)

Models/
  *.g.cs          # Record types with JSON serialization (generated)
```

## generated-scalars

```csharp
// Strongly-typed wrappers with parsing
public readonly record struct TraceId : IParsable<TraceId>, ISpanFormattable
public readonly record struct SpanId : IParsable<SpanId>, ISpanFormattable
public readonly record struct SessionId : IParsable<SessionId>, ISpanFormattable
```

## namespaces

```yaml
Qyl.Common: Scalar primitives
Qyl.Enums: OTel enums
Qyl.Models: Record types
```

## rules

- NEVER add PackageReference (must stay BCL-only)
- NEVER edit *.g.cs files (generated from TypeSpec)
- To change types: edit core/specs/*.tsp, run nuke Generate
