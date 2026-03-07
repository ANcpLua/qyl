# Epic: Feature-Complete C# Tracing Annotations

**Status:** Planning
**Comparable:** Java `opentelemetry-instrumentation-annotations` + `@WithSpan` / `@SpanAttribute`
**Approach:** Roslyn source-gen interceptors (compile-time, zero runtime overhead)

---

## 1. Executive Summary

The Java OTel agent provides `@WithSpan` and `@SpanAttribute` as a zero-boilerplate way to create spans
around methods. `qyl.servicedefaults` already ships a C# equivalent (`[Traced]`, `[TracedTag]`, `[NoTrace]`)
that is structurally superior in several ways (class-level tracing, compile-time interception, no JVM agent).

This epic closes remaining gaps in feature parity and adds C#-specific enhancements that have no Java analog.

### Current implementation summary

| Component               | Location                                                                                  |
|-------------------------|-------------------------------------------------------------------------------------------|
| `[Traced]` attribute    | `qyl.servicedefaults/Instrumentation/TracedAttribute.cs`                                  |
| `[TracedTag]` attribute | `qyl.servicedefaults/Instrumentation/TracedTagAttribute.cs`                               |
| `[NoTrace]` attribute   | `qyl.servicedefaults/Instrumentation/NoTraceAttribute.cs`                                 |
| Call site analyzer      | `qyl.servicedefaults.generator/Analyzers/TracedCallSiteAnalyzer.cs`                       |
| Interceptor emitter     | `qyl.servicedefaults.generator/Emitters/TracedInterceptorEmitter.cs`                      |
| Data model              | `qyl.servicedefaults.generator/Models/Models.cs` (`TracedCallSite`, `TracedTagParameter`) |
| Generator pipeline      | `ServiceDefaultsSourceGenerator.cs` → `QSG005` → `TracedIntercepts.g.cs`                  |

---

## 2. Current vs. Java Feature Matrix

| Java Feature                                       | C# Status                      | Notes                                                                       |
|----------------------------------------------------|--------------------------------|-----------------------------------------------------------------------------|
| `@WithSpan` – method-level                         | ✅ `[Traced("source")]`         | Required constructor arg for ActivitySource name                            |
| `@WithSpan` – class-level                          | ✅ **Better**                   | All public methods traced; `[NoTrace]` opt-out                              |
| `@SpanAttribute` – parameters                      | ✅ `[TracedTag]`                | Includes `SkipIfNull` opt-in                                                |
| `@SpanAttribute` – name derivation from param name | ✅                              | Falls back to parameter name                                                |
| `value` → custom span name                         | ✅ `SpanName = "..."`           | Named property on `[Traced]`                                                |
| `kind` → SpanKind                                  | ✅ `Kind = ActivityKind.Client` | Full `ActivityKind` enum                                                    |
| `inheritContext = false` (root span)               | ❌ Missing                      | **Story T-001**                                                             |
| `CompletableFuture` / async return span lifetime   | ✅ `Task`/`ValueTask`           | **See gap T-002 for IAsyncEnumerable**                                      |
| Exception recording                                | ⚠️ Partial                     | Uses wrong attributes — **Story T-003**                                     |
| `@SpanAttribute` on properties                     | ⚠️ Declared, not generated     | `[TracedTag]` targets `Property` but generator ignores it — **Story T-004** |
| Config-based suppression                           | ❌ N/A                          | `[NoTrace]` is strictly better                                              |
| Config-based method include                        | ❌ N/A                          | Not applicable (compile-time)                                               |
| Misuse diagnostics                                 | ❌ None                         | **Story T-005**                                                             |

---

## 3. Feature Stories

### T-001 · `RootSpan` — orphaned / context-breaking spans

**Java analog:** `@WithSpan(inheritContext = false)`
**C# design:**

```csharp
[Traced("MyApp.Background", RootSpan = true)]
public async Task ProcessInBackground() { ... }
```

**What it does:** Passes `startTime: default, parentContext: default` to `ActivitySource.StartActivity`,
creating a span with no parent even if one exists on the current thread.

**Primary use cases:**

- Background jobs / hosted services that should be traced independently
- Incoming message consumers (queue/Kafka) where the upstream context is irrelevant

**Generator change:**
In `TracedInterceptorEmitter` emit:

```csharp
// When RootSpan = true:
using var activity = _source.StartActivity(
    name: "...",
    kind: ActivityKind.Internal,
    parentContext: default);   // ← breaks context inheritance
```

**Model change:** Add `bool RootSpan` to `TracedCallSite` and `bool RootSpan { get; set; }` to
`TracedAttribute`.

---

### T-002 · `IAsyncEnumerable<T>` streaming span lifetime

**Java analog:** `reactor.core.publisher.Flux` / `io.reactivex.Flowable`
**Current behavior:** `IAsyncEnumerable<T>` return is treated as a normal async method. The span closes
when the first item is yielded (the `await foreach` call-site returns), not when the stream is exhausted.

**Correct behavior:** The span should remain open until the consumer has iterated all items or an exception
is thrown during iteration.

**C# design:**

The interceptor wraps the source `IAsyncEnumerable<T>` in a helper that keeps the activity alive:

```csharp
// Emitted for IAsyncEnumerable<T> return type:
[InterceptsLocation(...)]
public static async IAsyncEnumerable<T> Intercept_Traced_N<T>(
    this MyClass @this,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    using var activity = _source.StartActivity("...", ActivityKind.Internal);
    try
    {
        await foreach (var item in @this.OriginalMethod(ct).WithCancellation(ct))
        {
            yield return item;
        }
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        RecordException(activity, ex);
        throw;
    }
}
```

**Complexity:** Requires the emitter to detect `IAsyncEnumerable<T>` return type (distinct from
`Task<IAsyncEnumerable<T>>`), emit an `async IAsyncEnumerable` interceptor with `yield return`, and
handle `[EnumeratorCancellation]` on the `CancellationToken` parameter if present.

**Model change:** Add `bool IsAsyncEnumerable` to `TracedCallSite`.
Detection in `TracedCallSiteAnalyzer`: check
`ReturnTypeName.StartsWith("System.Collections.Generic.IAsyncEnumerable<")`.

---

### T-003 · Standard OTel exception attributes

**Bug:** The current emitter records exceptions using:

```csharp
new ActivityTagsCollection
{
    { GenAiAttributes.ExceptionType, ex.GetType().FullName },
    { GenAiAttributes.ExceptionMessage, ex.Message }
}
```

`GenAiAttributes` is the **wrong** attribute set for a generic tracing primitive. The correct OTel semconv
attributes are `exception.*` ([spec](https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-spans/)):

| Attribute              | Key                      | Source                  |
|------------------------|--------------------------|-------------------------|
| `exception.type`       | `"exception.type"`       | `ex.GetType().FullName` |
| `exception.message`    | `"exception.message"`    | `ex.Message`            |
| `exception.stacktrace` | `"exception.stacktrace"` | `ex.ToString()`         |
| `exception.escaped`    | `"exception.escaped"`    | `true` (re-thrown)      |

**Fix:** Replace `GenAiAttributes.*` references in `TracedInterceptorEmitter` with either:

- Direct string literals (`"exception.type"` etc.), or
- `ExceptionAttributes.*` constants from `qyl.protocol` / generated semconv

**Generated code after fix:**

```csharp
catch (global::System.Exception ex)
{
    activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
    activity?.AddEvent(new global::System.Diagnostics.ActivityEvent(
        "exception",
        tags: new global::System.Diagnostics.ActivityTagsCollection
        {
            { "exception.type",       ex.GetType().FullName },
            { "exception.message",    ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped",    true },
        }));
    throw;
}
```

---

### T-004 · `[TracedTag]` on properties

**Current:** `TracedTagAttribute` is declared with `AttributeTargets.Parameter | AttributeTargets.Property`
but `TracedCallSiteAnalyzer.ExtractTracedTags` only iterates `method.Parameters`. The property target is dead.

**Use case:** Capture the current object state as span context without threading it through as a parameter:

```csharp
[Traced("MyApp.Orders")]
public class OrderService
{
    [TracedTag("tenant.id")]
    public string TenantId { get; init; }   // captured on every span from this instance

    public async Task ProcessOrder(string orderId) { ... }
}
```

**Design options:**

**Option A — Instance property capture at method entry:**
At each intercepted call, emit `activity?.SetTag("tenant.id", @this.TenantId)` for each property on the
containing type decorated with `[TracedTag]`.

Requires the analyzer to also collect `[TracedTag]`-decorated properties from the containing type, not just
parameters. Extend `TracedCallSite` to carry `EquatableArray<TracedTagProperty> TracedTagProperties`.

**Option B — Opt-in only, not class-scan:**
Only when method has explicit `[Traced]` (not class-level), scan instance properties.

**Recommended:** Option A with a visibility filter (only public/internal properties).

**Model change:**

```csharp
internal sealed record TracedTagProperty(
    string PropertyName,
    string TagName,
    bool SkipIfNull,
    bool IsNullable,
    bool IsStatic);
```

---

### T-005 · Analyzer diagnostics for attribute misuse

No Roslyn diagnostics currently exist for the `[Traced]` family. These would surface as build warnings/errors.

| Diagnostic                                                     | ID       | Severity | Condition                                                                      |
|----------------------------------------------------------------|----------|----------|--------------------------------------------------------------------------------|
| `[TracedTag]` without `[Traced]` on method or containing class | `QSD001` | Warning  | Parameter has `[TracedTag]` but no `[Traced]` in scope                         |
| `[NoTrace]` without class-level `[Traced]`                     | `QSD002` | Info     | `[NoTrace]` on a method whose class lacks `[Traced]`                           |
| `[Traced]` on non-interceptable method                         | `QSD003` | Warning  | Abstract, extern, or partial method                                            |
| `[Traced]` `ActivitySourceName` not registered                 | `QSD004` | Warning  | Checks `builder.Services.AddActivitySource(name)` call existence (best-effort) |
| `[TracedTag]` on `out`/`ref` parameter                         | `QSD005` | Error    | Cannot safely read `out` params before method body runs                        |

**Implementation approach:**
Add `TracedDiagnosticAnalyzer : DiagnosticAnalyzerBase` (from `ANcpLua.Roslyn.Utilities.Analyzers`) in
`qyl.servicedefaults.generator/Analyzers/`. Register it alongside the source generator.

---

### T-006 · `[TracedTag]` `SkipIfDefault` — value type null-equivalent

**Current:** `SkipIfNull` works for nullable reference types and `Nullable<T>`. But for value types like
`int`, `Guid`, `bool`, and `TimeSpan`, null is never possible — yet recording `0`, `Guid.Empty`, or `false`
is often noise.

**C# design:**

```csharp
[Traced("MyApp.Payments")]
public void Charge(
    [TracedTag("payment.amount", SkipIfDefault = true)] decimal amount,
    [TracedTag("payment.currency")] string currency) { ... }
```

When `SkipIfDefault = true` and the parameter is a value type, emit:

```csharp
if (!global::System.Collections.Generic.EqualityComparer<decimal>.Default.Equals(amount, default))
    activity.SetTag("payment.amount", amount);
```

**Model change:** Add `bool SkipIfDefault` to `TracedTagParameter` and `TracedTagAttribute`.

---

### T-007 · `[TracedReturn]` — capture return value as span attribute

No Java equivalent. C# advantage enabled by compile-time interception.

```csharp
[Traced("MyApp.Catalog")]
[return: TracedReturn("product.sku")]
public Product GetProduct(string id) { ... }
```

For void/Task returns this is a no-op (compile warning via T-005 diagnostic QSD006).

**Emitted pattern:**

```csharp
var result = OriginalMethod(args);
activity?.SetTag("product.sku", result?.Sku);   // accessor TBD
return result;
```

**Design question:** `result` is typed as `Product` — what to tag? Options:

- String representation: `result?.ToString()` (simple, always works)
- Specific property: `[return: TracedReturn("product.sku", Property = nameof(Product.Sku))]`
- Full OTel-tagged object: if `Product` has `[OTel]`-decorated properties, use existing `OTelTagBinding` infrastructure

**Recommended:** Start with `ToString()` default + optional `Property` path for dotted member access
(`"Usage.InputTokenCount"` style, matching what `ProviderRegistry.TokenUsageDefinition` already does).

---

### T-008 · `[SpanEvent]` — ad-hoc span events from parameters

Enables recording milestone events within a span without manual `Activity.Current.AddEvent(...)` calls:

```csharp
[Traced("MyApp.Import")]
public async Task ImportFile(
    [TracedTag("import.path")] string path,
    [SpanEvent("import.started")] bool _ = false)   // sentinel, ignored as value
{ ... }
```

More realistic — event at a specific code path without attribute:

**Alternative design** — method-level event markers via a separate attribute on the class:

```csharp
[SpanEvent("import.validation.passed")]
private void ValidateRow(Row row) { ... }   // generates AddEvent on current activity
```

This is scope-creep for this epic. **Defer to a separate epic.**

---

## 4. C#-Specific Advantages Over Java (Implemented or Planned)

| Feature                      | Java                                  | C#                                                        |
|------------------------------|---------------------------------------|-----------------------------------------------------------|
| Instrumentation point        | Runtime bytecode weaving (JVM agent)  | Compile-time Roslyn interceptors                          |
| Overhead                     | JVM startup + per-call reflection     | Zero (direct call in generated IL)                        |
| Class-level tracing          | ❌ Method-only                         | ✅ `[Traced]` on class                                     |
| Opt-out at method level      | Config string (`"ClassName[method]"`) | ✅ `[NoTrace]`                                             |
| MSBuild toggle               | ❌                                     | ✅ `<QylTraced>false</QylTraced>` disables entire pipeline |
| Generic methods              | Limited                               | ✅ Full generic parameter + constraints                    |
| Static methods               | N/A                                   | ✅ Supported                                               |
| SkipIfNull                   | ❌                                     | ✅                                                         |
| SkipIfDefault                | ❌                                     | 🔲 Story T-006                                            |
| Return value capture         | ❌                                     | 🔲 Story T-007                                            |
| Property-level tags          | ❌                                     | 🔲 Story T-004                                            |
| IAsyncEnumerable lifetime    | ❌                                     | 🔲 Story T-002                                            |
| Diagnostics / build warnings | ❌                                     | 🔲 Story T-005                                            |

---

## 5. Implementation Sequence

Dependencies and recommended order:

```
T-003  (exception attributes — bug fix, no model change, do first)
  ↓
T-001  (RootSpan — small model + emitter change)
T-006  (SkipIfDefault — small model + emitter change, parallel with T-001)
  ↓
T-002  (IAsyncEnumerable — medium complexity, needs model + emitter)
T-004  (property TracedTag — medium, needs analyzer + model + emitter)
  ↓
T-005  (diagnostics — can be done any time, no emitter dependency)
T-007  (TracedReturn — largest scope, defer until T-002/T-004 stable)
```

### Priority recommendation

| Priority | Story                      | Effort |
|----------|----------------------------|--------|
| P0 (bug) | T-003 exception attributes | XS     |
| P1       | T-001 RootSpan             | S      |
| P1       | T-006 SkipIfDefault        | S      |
| P2       | T-002 IAsyncEnumerable     | M      |
| P2       | T-004 property TracedTag   | M      |
| P2       | T-005 diagnostics          | M      |
| P3       | T-007 TracedReturn         | L      |

---

## 6. Files Touched Per Story

| Story | Attributes (`servicedefaults`) | Analyzer (`servicedefaults.generator`) | Emitter                       | Model       |
|-------|--------------------------------|----------------------------------------|-------------------------------|-------------|
| T-001 | `TracedAttribute.cs`           | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-002 | —                              | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-003 | —                              | —                                      | `TracedInterceptorEmitter.cs` | —           |
| T-004 | —                              | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-005 | —                              | new `TracedDiagnosticAnalyzer.cs`      | —                             | —           |
| T-006 | `TracedTagAttribute.cs`        | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-007 | new `TracedReturnAttribute.cs` | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |

---

## 7. Test Plan

Each story requires:

1. **Unit test** in `qyl.collector.tests` (or a new `qyl.servicedefaults.generator.tests` project):
    - Roslyn compilation test using `CSharpGeneratorDriver`
    - Verify emitted interceptor matches snapshot
    - Verify `TracedCallSite` model is correctly extracted

2. **Integration test** (live span):
    - Method with `[Traced]` is called
    - Assert `ActivitySource` produces a span with expected name, kind, tags
    - Assert exception event is recorded with correct attribute keys

3. **Negative test** (diagnostic):
    - T-005: Confirm each diagnostic fires at the right syntax location

### Suggested test structure for generator tests

```
tests/qyl.servicedefaults.generator.tests/
├── Traced/
│   ├── TracedInterceptorEmitterTests.cs    (snapshot tests)
│   ├── TracedCallSiteAnalyzerTests.cs      (model extraction)
│   └── TracedDiagnosticsTests.cs           (T-005)
└── Helpers/
    └── GeneratorTestHarness.cs
```

---

## 8. Open Questions

1. **T-004 — Property tag capture scope:** Should `[TracedTag]` on a property be captured for every span on
   that instance, or only when the method itself has `[Traced]` (not class-level)? Instance-level capture
   implies the emitter reads `@this.PropertyName`, which requires the containing type to be known.

2. **T-007 — Property accessor syntax:** Should `Property = "Usage.InputTokenCount"` use dotted member
   access reflection-style, or require a `nameof` expression? Dotted access is more flexible but needs a
   small path-walker in the emitter.

3. **T-002 — `EnumeratorCancellation` passthrough:** If the original method has a `CancellationToken` param
   without `[EnumeratorCancellation]`, the interceptor `IAsyncEnumerable` wrapper still needs to forward it.
   Should the emitter always add `[EnumeratorCancellation]` on the interceptor parameter matching the token?

4. **T-005 — QSD004 ActivitySource registration check:** This is best-effort static analysis. Is it worth
   the complexity, or is it too likely to produce false positives (e.g., source registered via reflection or
   config)? Consider making it opt-in via `#pragma warning enable QSD004`.
