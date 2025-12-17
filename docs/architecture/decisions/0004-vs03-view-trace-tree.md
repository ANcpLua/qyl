# ADR-0004: VS-03 View Trace Tree

## Metadata

| Field      | Value            |
|------------|------------------|
| Status     | Draft            |
| Date       | 2025-12-16       |
| Slice      | VS-03            |
| Priority   | P1               |
| Depends On | ADR-0002 (VS-01) |
| Supersedes | -                |

## Context

Traces bestehen aus hierarchisch verknüpften Spans (Parent-Child via `parent_span_id`). Für Debugging und
Performance-Analyse wird eine Baum-Darstellung benötigt, die:

- Span-Hierarchie visualisiert
- Timing als Gantt-Chart zeigt
- Errors hervorhebt
- gen_ai.* Attribute inline anzeigt

## Decision

Implementierung einer Trace Tree Ansicht mit:

- Server-side Tree-Building (vermeidet komplexe Client-Logik)
- Gantt-Chart Visualisierung für Timing
- Collapsible Tree Nodes
- Inline Span Details

## Layers

### 1. TypeSpec (Contract)

```yaml
files:
  - core/specs/api/traces.tsp        # Trace endpoints
  - core/specs/models/traces.tsp     # TraceNode, TraceTree
generates:
  - core/generated/openapi/openapi.yaml
```

**traces.tsp Example:**

```typespec
@route("/api/v1/traces")
namespace Traces {
  @get @route("/{traceId}")
  op get(@path traceId: string): TraceTree | NotFoundResponse;

  @get @route("/{traceId}/spans")
  op getSpans(@path traceId: string): SpanRecord[];
}

model TraceNode {
  span: SpanRecord;
  children: TraceNode[];
  depth: int32;
  relativeStartMs: float64;  // Relative to trace start
  durationMs: float64;
}

model TraceTree {
  traceId: string;
  rootSpan: TraceNode;
  totalSpans: int32;
  totalDurationMs: float64;
  serviceName: string;
  hasErrors: boolean;
}
```

### 2. Protocol Layer

```yaml
files:
  - src/qyl.protocol/Models/TraceNode.cs
```

### 3. Query Layer

```yaml
files:
  - src/qyl.collector/Query/TraceQueryService.cs
methods:
  - "GetTraceTreeAsync(traceId)"
  - "GetTraceSpansAsync(traceId)"
  - "BuildTreeFromSpans(spans)"  # Recursive tree builder
```

**Tree Building Algorithm:**

```csharp
public TraceNode BuildTree(IReadOnlyList<SpanRecord> spans)
{
    var byId = spans.ToFrozenDictionary(s => s.SpanId);
    var root = spans.FirstOrDefault(s => s.ParentSpanId is null)
        ?? throw new InvalidOperationException("No root span");

    return BuildNode(root, byId, 0, root.StartTimeUnixNano);
}

private TraceNode BuildNode(SpanRecord span, FrozenDictionary<string, SpanRecord> byId, int depth, ulong traceStart)
{
    var children = byId.Values
        .Where(s => s.ParentSpanId == span.SpanId)
        .OrderBy(s => s.StartTimeUnixNano)
        .Select(s => BuildNode(s, byId, depth + 1, traceStart))
        .ToList();

    return new TraceNode(
        Span: span,
        Children: children,
        Depth: depth,
        RelativeStartMs: (span.StartTimeUnixNano - traceStart) / 1_000_000.0,
        DurationMs: (span.EndTimeUnixNano - span.StartTimeUnixNano) / 1_000_000.0
    );
}
```

### 4. API Layer

```yaml
endpoints:
  - "GET /api/v1/traces/{traceId}"        # Returns TraceTree
  - "GET /api/v1/traces/{traceId}/spans"  # Returns flat SpanRecord[]
files:
  - src/qyl.collector/Program.cs
```

### 5. MCP Layer

```yaml
files:
  - src/qyl.mcp/Tools/GetTraceTool.cs
tools:
  - name: "get_trace"
    description: "Get hierarchical trace tree"
    parameters:
      traceId: string (required)
    returns: "ASCII tree representation with timing"
```

**MCP Output Example:**

```
Trace: abc123 (123.45ms, 2 errors)
├─ [0ms] POST /chat (45.2ms) ✓
│  ├─ [2ms] db.query (3.1ms) ✓
│  ├─ [10ms] openai.chat (35.5ms) ✗ ERROR
│  │  └─ gen_ai: gpt-4, 150 input, 89 output tokens
│  └─ [48ms] cache.set (1.2ms) ✓
└─ [50ms] log.write (5.3ms) ✓
```

### 6. Dashboard Layer

```yaml
files:
  - src/qyl.dashboard/src/api/hooks.ts      # useTrace()
  - src/qyl.dashboard/src/components/traces/TraceTree.tsx
  - src/qyl.dashboard/src/components/traces/TraceGantt.tsx
  - src/qyl.dashboard/src/components/traces/SpanNode.tsx
  - src/qyl.dashboard/src/components/traces/SpanDetail.tsx
  - src/qyl.dashboard/src/pages/TracePage.tsx
patterns:
  - "Recursive rendering for tree nodes"
  - "TanStack Virtual for large trees"
  - "Recharts for Gantt visualization"
```

## Acceptance Criteria

- [ ] `GET /api/v1/traces/{traceId}` gibt TraceTree zurück
- [ ] Tree-Struktur korrekt aufgebaut (Parent-Child Beziehungen)
- [ ] Relative Timing berechnet (RelativeStartMs, DurationMs)
- [ ] Errors werden markiert (hasErrors)
- [ ] MCP `get_trace` Tool gibt ASCII-Tree aus
- [ ] Dashboard zeigt collapsible Tree
- [ ] Dashboard zeigt Gantt-Chart mit Timing
- [ ] SpanDetail Popup bei Klick auf Span
- [ ] gen_ai.* Attribute inline sichtbar

## Test Files

```yaml
unit_tests:
  - tests/qyl.collector.tests/Query/TraceQueryServiceTests.cs
  - tests/qyl.collector.tests/Query/TreeBuilderTests.cs
integration_tests:
  - tests/qyl.collector.tests/Api/TraceEndpointsTests.cs
```

## Consequences

### Positive

- **Debugging**: Klare Visualisierung von Request-Flow
- **Performance Analysis**: Timing-Bottlenecks sofort sichtbar
- **Error Tracking**: Errors im Kontext der gesamten Trace

### Negative

- **Memory**: Große Traces benötigen viel Memory für Tree-Building
- **Komplexität**: Rekursive Strukturen sind fehleranfällig

### Risks

| Risk                 | Impact | Likelihood | Mitigation                   |
|----------------------|--------|------------|------------------------------|
| Zirkuläre Referenzen | High   | Low        | Cycle Detection im Builder   |
| Sehr tiefe Bäume     | Medium | Low        | Max Depth Limit (100)        |
| Fehlende Root Spans  | Medium | Medium     | Synthetischer Root erstellen |

## References

- [ADR-0002](0002-vs01-span-ingestion.md) - VS-01 Span Ingestion (Dependency)
- [OpenTelemetry Trace Data Model](https://opentelemetry.io/docs/specs/otel/trace/api/)
- [qyl-architecture.yaml](../../qyl-architecture.yaml) Section: projects.qyl.collector.Query
