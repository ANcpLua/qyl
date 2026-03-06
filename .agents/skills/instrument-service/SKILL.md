---
name: instrument-service
description: Add compile-time OTel instrumentation to qyl services using source-generated interceptors. Use when adding tracing, metrics, or semantic convention tags to any qyl component.
---

# Instrument a qyl Service

qyl uses compile-time source generation for zero-cost observability. Annotate your code, the Roslyn generator produces interceptor methods with `[InterceptsLocation]`. No runtime agent, no reflection, no overhead when nobody is listening.

## Attribute Vocabulary

### Tracing (spans)

| Attribute | Target | What it generates |
|-----------|--------|-------------------|
| `[Traced("source.name")]` | Class or Method | Span around method via interceptor. Class-level traces all public methods. |
| `[NoTrace]` | Method | Opts out a method from class-level `[Traced]` |
| `[TracedTag]` or `[TracedTag("tag.name")]` | Parameter | `activity.SetTag()` call. Name from parameter if omitted. |
| `[TracedReturn("tag.name")]` | Return value | Captures return value as span tag. Supports `Property = "Nested.Path"`. |
| `[OTel("semconv.name")]` | Property or Parameter | `activity.SetTag()` with OTel semantic convention name |
| `[AgentTraced]` | Method | `gen_ai.agent.invoke` span via `qyl.agent` ActivitySource |

### Metrics (counters, histograms, gauges)

| Attribute | Target | What it generates |
|-----------|--------|-------------------|
| `[Meter("meter.name")]` | Class (must be `static partial`) | Static `Meter` instance + instrument fields |
| `[Counter("metric.name")]` | Method (must be `static partial`) | `Counter<long>.Add(1)` with tags |
| `[Histogram("metric.name")]` | Method (must be `static partial`) | `Histogram<double>.Record(value)` with tags |
| `[Gauge("metric.name")]` | Method (must be `static partial`) | `ObservableGauge` with stored value |
| `[UpDownCounter("metric.name")]` | Method (must be `static partial`) | `UpDownCounter<long>.Add(delta)` with tags |
| `[Tag("tag.name")]` | Parameter (on metric methods) | `KeyValuePair` tag on metric recording |

All metric attributes support optional `Unit` and `Description` properties.

## ActivitySources

Four sources, lazy-initialized (zero cost until first use):

| Source | Name | Signal | Used by |
|--------|------|--------|---------|
| `ActivitySources.GenAiSource` | `qyl.genai` | traces | GenAI SDK interception |
| `ActivitySources.DbSource` | `qyl.db` | traces | Database call interception |
| `ActivitySources.AgentSource` | `qyl.agent` | traces + metrics | Agent invocation tracing |
| `TracedActivitySources.*` | `qyl.traced` | traces | `[Traced]` method interceptors |

Two meters: `ActivitySources.GenAiMeter` (`qyl.genai`), `ActivitySources.AgentMeter` (`qyl.agent`).

## Generator Pipelines

`ServiceDefaultsSourceGenerator` runs 7 parallel pipelines, each gated by runtime availability AND MSBuild toggle:

| Pipeline | Analyzer | Emitter | Output | Toggle |
|----------|----------|---------|--------|--------|
| Builder | `CouldBeInvocation` | inline | `Intercepts.g.cs` | always |
| GenAI | `GenAiCallSiteAnalyzer` | `GenAiInterceptorEmitter` | `GenAiIntercepts.g.cs` | `QylGenAi` |
| Database | `DbCallSiteAnalyzer` | `DbInterceptorEmitter` | `DbIntercepts.g.cs` | `QylDatabase` |
| OTel Tags | `OTelTagAnalyzer` | `OTelTagsEmitter` | `OTelTagExtensions.g.cs` | always |
| Meter | `MeterAnalyzer` | `MeterEmitter` | `MeterImplementations.g.cs` | `QylMeter` |
| Traced | `TracedCallSiteAnalyzer` | `TracedInterceptorEmitter` | `TracedIntercepts.g.cs` | `QylTraced` |
| Agent | `AgentCallSiteAnalyzer` | `AgentInterceptorEmitter` | `AgentIntercepts.g.cs` | `QylAgent` |

Each pipeline follows the same pattern:
1. **Syntactic pre-filter** — cheap, runs on every syntax node (no semantic model)
2. **Semantic analysis** — validates attributes, extracts call site metadata
3. **Model building** — creates descriptors with `InterceptableLocation`
4. **Code emission** — generates interceptor methods with `[InterceptsLocation]`

### MSBuild Toggles

Disable a pipeline in `.csproj`:

```xml
<PropertyGroup>
  <QylGenAi>false</QylGenAi>
  <QylDatabase>false</QylDatabase>
  <QylAgent>false</QylAgent>
  <QylTraced>false</QylTraced>
  <QylMeter>false</QylMeter>
</PropertyGroup>
```

Default is `true` (enabled) when property is absent.

## Roslyn Utilities Foundation

The generator is built on `ANcpLua.Roslyn.Utilities`:

| Utility | Used for |
|---------|----------|
| `EquatableArray<T>` | Value-equality arrays for incremental generator caching |
| `DiagnosticFlow<T>` | Railway-oriented error accumulation in analyzer pipelines |
| `SymbolMatch` | Fluent pattern matching for Roslyn symbols |
| `InvocationMatch` | Fluent matching for method invocations |
| `SyntaxValueProviderExtensions` | `WhereNotNull()`, `SelectAndReportExceptions()`, `AddSource()` |
| `Guard.NotNull()` | Argument validation with `CallerArgumentExpression` |
| `OTelContext` | Shared OTel type resolution across analyzers |

## Add Tracing to a Service

1. Reference `qyl.servicedefaults` (already done for most src projects)

2. Add `[Traced]` to your class:

```csharp
[Traced("qyl.traced")]
public class MyService
{
    public async Task<Result> Process([TracedTag] string id) { ... }

    [NoTrace]
    internal void HelperMethod() { ... }
}
```

3. Build: `nuke`

4. Inspect generated code in `obj/` — look for `TracedIntercepts.g.cs`

5. Verify spans: run the app, check collector at `http://localhost:5100`

## Add Metrics to a Service

1. Create a partial class with `[Meter]`:

```csharp
[Meter("qyl.myservice")]
public static partial class MyServiceMetrics
{
    [Counter("myservice.requests", Unit = "{request}", Description = "Requests processed")]
    public static partial void RecordRequest([Tag("status")] string status);

    [Histogram("myservice.duration", Unit = "ms", Description = "Processing duration")]
    public static partial void RecordDuration(double ms, [Tag("operation")] string operation);
}
```

2. Build: `nuke`

3. Inspect `MeterImplementations.g.cs` in `obj/`

4. Call from your code: `MyServiceMetrics.RecordRequest("ok")`

## Add OTel Semantic Convention Tags

For types that carry OTel attributes (DTOs, records):

```csharp
public record ChatRequest(
    [OTel("gen_ai.request.model")] string Model,
    [OTel("gen_ai.request.max_tokens")] int? MaxTokens);
```

The generator produces tag extraction extension methods in `OTelTagExtensions.g.cs`.

## Add Agent Tracing

```csharp
[AgentTraced(AgentName = "curator")]
public async Task<AgentResponse> InvokeAgent(string prompt) { ... }
```

Generates a `gen_ai.agent.invoke` span on `qyl.agent` ActivitySource.

## Zero-Cost Guarantee

When no `ActivityListener` is registered for a source:

1. `ActivitySource.StartActivity()` checks `HasListeners()` → returns `null`
2. Generated interceptor: `if (activity is not null)` → skips all tag/event calls
3. Net cost: one boolean check per instrumented method call

This is the .NET equivalent of Java's `@WithSpan` — but resolved at compile time, not runtime bytecode weaving.

## Verify Instrumentation

```bash
# Build and confirm no errors
nuke

# Check generated files exist
find . -path "*/obj/*" -name "*Intercepts.g.cs" -o -name "*Implementations.g.cs" | head -10

# Run with collector to see spans
nuke Dev
# Then hit your instrumented endpoints
```
