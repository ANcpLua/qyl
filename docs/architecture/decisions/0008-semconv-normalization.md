# ADR-008: Semantic Convention Normalization

## Status

Accepted

## Date

2024-12-17

## Context

OpenTelemetry semantic conventions evolve over time. Different SDK versions emit different attribute names:

- SDK v1.17: `http.method`, `db.statement`, `gen_ai.system`
- SDK v1.21: `http.request.method`, `db.statement`, `gen_ai.system`
- SDK v1.24: `http.request.method`, `db.query.text`, `gen_ai.system`
- SDK v1.27+: `http.request.method`, `db.query.text`, `gen_ai.provider.name`

When polyglot applications send telemetry to qyl.collector, we receive inconsistent attribute names, breaking queries and visualizations.

### Forces

- Cannot control which SDK versions applications use
- Need consistent attribute names for DuckDB queries
- Must handle both old and new conventions
- Want to stay current with latest conventions (v1.38.0)
- Should be zero-latency (in-process, not external collector)

## Decision

Implement **SemconvNormalizer** in qyl.collector that normalizes all incoming OTLP telemetry to v1.38.0 semantic conventions during ingestion.

### Architecture

```
User App ──OTLP──► qyl.collector ──SemconvNormalizer──► DuckDB
     ↓                     ↓                              ↓
  Mixed schemas      Normalize to v1.38.0         Consistent queries
```

### Key Design Decisions

1. **In-Process Normalization**: No external OTel Collector needed
2. **FrozenDictionary Lookup**: O(1) attribute name resolution
3. **Idempotent**: Safe to apply multiple times, preserves newer conventions
4. **SchemaVersion Primitive**: Track source schema version if available

### Implementation

```csharp
// qyl.collector/Ingestion/SemconvNormalizer.cs
public static class SemconvNormalizer
{
    public static SchemaVersion TargetVersion => SchemaVersion.V1_38_0;

    // FrozenDictionary for O(1) hot path lookup
    private static readonly FrozenDictionary<string, string> AttributeRenames = ...;

    public static string Normalize(string attributeName) =>
        AttributeRenames.GetValueOrDefault(attributeName, attributeName);
}
```

## Consequences

### Positive

- **Consistency**: All telemetry uses v1.38.0 attributes in DuckDB
- **Zero External Dependencies**: No OTel Collector required
- **Minimal Latency**: In-process, FrozenDictionary lookup
- **Simple Queries**: Dashboard queries use single attribute names
- **No SDK Changes**: Works with any SDK version

### Negative

- **Hardcoded Renames**: Must update code when new conventions released
- **Memory Overhead**: FrozenDictionary in memory (negligible)

### Neutral

- Can still use OTel Collector in front if needed for other processing

## Alternatives Considered

### Alternative 1: External OTel Collector with OTTL

Rejected because:
- Extra network hop adds latency
- Additional component to deploy and maintain
- qyl.collector already receives OTLP directly

### Alternative 2: Require Latest SDK Versions

Rejected because:
- Cannot control third-party applications
- Breaking change for existing deployments
- Some SDKs lag behind conventions

### Alternative 3: Support Both Old and New in Queries

Rejected because:
- Query complexity explodes (OR conditions everywhere)
- Dashboard logic must handle both formats
- Tech debt accumulates over time

## References

- [OpenTelemetry Schema Files](https://opentelemetry.io/schemas/)
- [Semantic Convention Stability](https://opentelemetry.io/docs/specs/otel/versioning-and-stability/)
- [OTel Semconv v1.38.0](https://opentelemetry.io/docs/specs/semconv/)
