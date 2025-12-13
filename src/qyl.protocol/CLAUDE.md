# qyl.protocol

@import "../../CLAUDE.md"

## Scope

Shared types and contracts for qyl AI Observability Platform. **LEAF project** - references nothing except BCL.

## Dependency Rules

| Allowed                         | Forbidden                     |
|---------------------------------|-------------------------------|
| System.*                        | qyl.* (any other qyl project) |
| Microsoft.Extensions.Primitives | DuckDB.*                      |
|                                 | OpenTelemetry.*               |
|                                 | Grpc.*                        |

## Contents

```
Primitives/
├── SessionId.cs    # Strongly-typed session identifier
└── UnixNano.cs     # Unix timestamp in nanoseconds

Models/
├── SpanRecord.cs      # Core span representation
├── GenAiSpanData.cs   # GenAI-specific extracted data
├── SessionSummary.cs  # Aggregated session info
└── TraceNode.cs       # Hierarchical trace tree

Attributes/
└── GenAiAttributes.cs # OTel 1.38 gen_ai.* constants

Contracts/
├── ISpanStore.cs         # Span storage interface
└── ISessionAggregator.cs # Session aggregation interface
```

## Multi-Targeting

This project targets multiple frameworks for broad compatibility:

- `net10.0` - Latest features
- `net8.0` - LTS support
- `netstandard2.0` - Legacy compatibility

## Validation Rules

Before adding ANY code:

- [ ] Does NOT reference any qyl.* project
- [ ] Does NOT use DuckDB, OpenTelemetry, or Grpc types
- [ ] Is a shared contract or primitive needed by multiple components
- [ ] If it's implementation-specific, it belongs in qyl.collector
