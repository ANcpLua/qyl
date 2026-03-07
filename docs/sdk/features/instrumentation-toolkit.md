# QYL Instrumentation Toolkit — Canonical Reference (Roslyn Generator)

## Core principle

- All instrumentation for these domains is generated at **compile time** through an `IIncrementalGenerator` setup in:
  - `src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs`
- There is **no reflection-based runtime AOP** for these attributes.
- Generation is implemented through generator pipelines, analyzers, and emitters.
- Generated code is emitted as part of the compile and uses direct OpenTelemetry API calls.

## Implemented attribute surface (12 attributes + 1 non-attribute DB pipeline)

### Tracing attributes

- `[Traced]`
  - Target: class or method
  - Purpose: auto-generate Activity span wrappers. Class-level applies to public methods, method-level is selective.

- `[TracedTag("key")]`
  - Target: parameter or property
  - Purpose: captures value as an Activity tag.

- `[TracedReturn("path")]`
  - Target: method
  - Purpose: captures return value (or dotted property path like `Result.Id`) as a span attribute.

- `[NoTrace]`
  - Target: method
  - Purpose: opt-out when class has `[Traced]`.

### Agent attributes

- `[AgentTraced]`
  - Target: method
  - Purpose: generates `gen_ai.agent.invoke` spans following OpenTelemetry GenAI semantic conventions for Agent SDK invocations.

### OTel convention attribute

- `[OTel("semantic.convention.name")]`
  - Target: property or parameter
  - Purpose: overrides attribute key used by generated tag mappings.

### Meter attributes

- `[Meter]`
  - Target: partial class
  - Purpose: marks a class for automatic meter and instrument registration through DI.

- `[Counter]`
  - Target: partial method
  - Purpose: generates a `Counter<long>` instrument (`Add(delta, tags)`).

- `[Histogram]`
  - Target: partial method
  - Purpose: generates a `Histogram<T>` instrument (`Record(value, tags)`).

- `[Gauge]`
  - Target: partial method
  - Purpose: generates an `ObservableGauge<T>` using a stored-value callback pattern.

- `[UpDownCounter]`
  - Target: partial method
  - Purpose: generates `UpDownCounter<long>` (supports signed deltas).

- `[Tag("key")]`
  - Target: parameter
  - Purpose: emits metric dimensions used by metric methods.

### DB interception domain

- **not attribute-driven**
- A dedicated DB interception pipeline instruments ADO.NET `DbCommand` flows (interceptor-style runtime path).

## Pipeline map (source-of-truth)

| Pipeline | Trigger | Purpose | Generated output |
|---|---|---|---|
| `Builder Interception` | `WebApplicationBuilder.Build()` | Auto-registration of Qyl service defaults | `Intercepts.g.cs` |
| `GenAI SDK Instrumentation` | GenAI SDK call patterns (chat, embeddings, text\_completion, agent ops, content generation) | OTel GenAI semantic-convention spans via `GenAiInstrumentation` runtime | `GenAiIntercepts.g.cs` |
| `DB Invocation` | ADO.NET `DbCommand` call sites | Runtime DB interception path support (separate from attribute domain) | `DbIntercepts.g.cs` |
| `OTel Tag Binding` | `[OTel("...")]` | OTel tag and convention-name binding for properties/parameters | `OTelTagExtensions.g.cs` |
| `Meter` | `[Meter]`, `[Counter]`, `[Histogram]`, `[Gauge]`, `[UpDownCounter]`, `[Tag]` | DI registration + meters + unified instrument emitters (`Counter`/`Histogram`/`Gauge`/`UpDownCounter`) | `MeterImplementations.g.cs` |
| `Tracing` | `[Traced]`, `[TracedTag]`, `[TracedReturn]`, `[NoTrace]` | Activity start/stop, tag capture, return capture, exception handling | `TracedIntercepts.g.cs` |
| `Agent` | `[AgentTraced]` + Microsoft.Agents.AI SDK patterns | `gen_ai.agent.invoke` span trees with agent semantic conventions | `AgentIntercepts.g.cs` |

## Precise correction vs. earlier 7-attribute abstraction

- `[Counter]`, `[Histogram]`, `[Gauge]`, `[UpDownCounter]`, `[Tag]` are emitted through **one unified Meter pipeline** (single pipeline, shared emitter), not as separate pipelines.
- `[OTel]` is **not only mapping-only**; it is part of a dedicated tag-binding pipeline.
- DB interception is **additional and domain-specific**, not attribute-driven.

## Short, unambiguous semantic definitions

### Tracing

- Class or method `[Traced]` creates Activity lifecycle wrappers.
- `[TracedTag]` on parameters/properties emits span tags.
- `[TracedReturn]` emits return-value tags (with property-path support).
- `[NoTrace]` excludes methods when class-level tracing is active.

### Agent

- `[AgentTraced]` or recognized Agents SDK invocations emit GenAI-compatible Activities.

### Metrics

- `[Meter]` marks a container class.
- `[Counter]` => `.Add(...)`
- `[Histogram]` => `.Record(...)`
- `[Gauge]` => `ObservableGauge<T>`
- `[UpDownCounter]` => signed counter updates via generated instrument usage
- `[Tag]` => adds dimensions/tags for metric records.

### OTel conventions

- `[OTel]` on property/parameter drives semantic-convention attribute naming in generated code.

## File map

- Runtime attribute definitions: `src/qyl.servicedefaults/Instrumentation/*.cs`
- Generator wiring: `src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs`
- Analyzer/Emitter pairs:
  - Builder registration: `src/qyl.servicedefaults.generator/`
  - Tracing:
    - `src/qyl.servicedefaults.generator/Analyzers/TracedCallSiteAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/TracedInterceptorEmitter.cs`
  - OTel tags:
    - `src/qyl.servicedefaults.generator/Analyzers/OTelTagAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/OTelTagsEmitter.cs`
  - Agent:
    - `src/qyl.servicedefaults.generator/Analyzers/AgentCallSiteAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/AgentInterceptorEmitter.cs`
  - Metrics:
    - `src/qyl.servicedefaults.generator/Analyzers/MeterAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/MeterEmitter.cs`
  - DB:
    - `src/qyl.servicedefaults.generator/Analyzers/DbCallSiteAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/DbInterceptorEmitter.cs`
  - GenAI:
    - `src/qyl.servicedefaults.generator/Analyzers/GenAiCallSiteAnalyzer.cs`
    - `src/qyl.servicedefaults.generator/Emitters/GenAiInterceptorEmitter.cs`
