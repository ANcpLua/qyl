# Epic: Feature-Complete C# Tracing Annotations

**Status:** Complete
**Comparable:** Java `opentelemetry-instrumentation-annotations` + `@WithSpan` / `@SpanAttribute`
**Approach:** Roslyn source-gen interceptors (compile-time, zero runtime overhead)

Cross-check all 12 instrumentation attributes and their pipelines in
the [Instrumentation Toolkit Reference](instrumentation-toolkit.md).

Layer guard:
- Keep schema-generation changes in `eng/build/SchemaGenerator.cs` only.
- Keep generator semantic changes in `src/qyl.servicedefaults.generator/`.
- Keep runtime + collector wiring in `src/qyl.servicedefaults/` and `src/qyl.collector/`.

---

## 1. Executive Summary

The Java OTel agent provides `@WithSpan` and `@SpanAttribute` as a zero-boilerplate way to create spans
around methods. `qyl.servicedefaults` ships a C# equivalent (`[Traced]`, `[TracedTag]`, `[NoTrace]`,
`[TracedReturn]`) that is structurally superior in every dimension (class-level tracing, compile-time
interception, no JVM agent, property-level tags, return value capture, diagnostics).

All feature stories (T-001 through T-007) are implemented. T-008 (`[SpanEvent]`) was deferred as scope-creep.

### Current implementation summary

| Component                  | Location                                                                                  |
|----------------------------|-------------------------------------------------------------------------------------------|
| `[Traced]` attribute       | `qyl.servicedefaults/Instrumentation/TracedAttribute.cs`                                  |
| `[TracedTag]` attribute    | `qyl.servicedefaults/Instrumentation/TracedTagAttribute.cs`                               |
| `[TracedReturn]` attribute | `qyl.servicedefaults/Instrumentation/TracedReturnAttribute.cs`                            |
| `[NoTrace]` attribute      | `qyl.servicedefaults/Instrumentation/NoTraceAttribute.cs`                                 |
| Call site analyzer         | `qyl.servicedefaults.generator/Analyzers/TracedCallSiteAnalyzer.cs`                       |
| Interceptor emitter        | `qyl.servicedefaults.generator/Emitters/TracedInterceptorEmitter.cs`                      |
| Diagnostic analyzer        | `qyl.servicedefaults.generator/Analyzers/TracedDiagnosticAnalyzer.cs`                     |
| Data model                 | `qyl.servicedefaults.generator/Models/Models.cs` (`TracedCallSite`, `TracedTagParameter`, `TracedTagProperty`, `TracedReturnInfo`) |
| Generator pipeline         | `ServiceDefaultsSourceGenerator.cs` → `QSG005` → `TracedIntercepts.g.cs`                 |

---

## 2. C# vs. Java Feature Matrix

| Java Feature                                       | C# Status                      | Notes                                                                       |
|----------------------------------------------------|--------------------------------|-----------------------------------------------------------------------------|
| `@WithSpan` — method-level                         | ✅ `[Traced("source")]`         | Required constructor arg for ActivitySource name                            |
| `@WithSpan` — class-level                          | ✅ **Better**                   | All public methods traced; `[NoTrace]` opt-out                              |
| `@SpanAttribute` — parameters                      | ✅ `[TracedTag]`                | Includes `SkipIfNull` and `SkipIfDefault`                                   |
| `@SpanAttribute` — name derivation from param name | ✅                              | Falls back to parameter name                                                |
| `value` → custom span name                         | ✅ `SpanName = "..."`           | Named property on `[Traced]`                                                |
| `kind` → SpanKind                                  | ✅ `Kind = ActivityKind.Client` | Full `ActivityKind` enum                                                    |
| `inheritContext = false` (root span)               | ✅ `RootSpan = true`            | Emits `parentContext: default` (T-001)                                      |
| `CompletableFuture` / async return span lifetime   | ✅ `Task`/`ValueTask`           | Plus `IAsyncEnumerable<T>` streaming span lifetime (T-002)                  |
| Exception recording                                | ✅ OTel semconv                 | `exception.type/message/stacktrace/escaped` (T-003)                         |
| `@SpanAttribute` on properties                     | ✅ Instance + static            | `[TracedTag]` on properties captured at method entry (T-004)                |
| Config-based suppression                           | ❌ N/A                          | `[NoTrace]` is strictly better                                              |
| Config-based method include                        | ❌ N/A                          | Not applicable (compile-time)                                               |
| Misuse diagnostics                                 | ✅ QSD001–QSD005                | Build warnings/errors for attribute misuse (T-005)                          |

---

## 3. Feature Stories (All Complete)

### T-001 · `RootSpan` — orphaned / context-breaking spans ✅

`[Traced("MyApp.Background", RootSpan = true)]` emits `parentContext: default` to
`ActivitySource.StartActivity`, creating an isolated root span.

**Implementation:** `TracedInterceptorEmitter.BuildStartActivity` (line 237–241).
**Model:** `TracedCallSite.RootSpan`, `TracedAttribute.RootSpan`.

---

### T-002 · `IAsyncEnumerable<T>` streaming span lifetime ✅

Interceptor wraps the source `IAsyncEnumerable<T>` in an `async` iterator that keeps the Activity
alive until the stream is exhausted or an exception occurs.

**Implementation:** `TracedInterceptorEmitter.AppendAsyncEnumerableInterceptor` (line 203–233).
**Model:** `TracedCallSite.IsAsyncEnumerable`.
**Detection:** `ReturnTypeName.StartsWith("System.Collections.Generic.IAsyncEnumerable<")`.

---

### T-003 · Standard OTel exception attributes ✅

Exception events use correct OTel semconv attributes: `exception.type`, `exception.message`,
`exception.stacktrace`, `exception.escaped`.

**Implementation:** `TracedInterceptorEmitter.ExceptionBlock` (line 312–325).

---

### T-004 · `[TracedTag]` on properties ✅

Properties decorated with `[TracedTag]` on the containing type are captured at method entry via
`@this.PropertyName` (instance) or `global::Type.PropertyName` (static).

**Implementation:**
- Analyzer: `TracedCallSiteAnalyzer.ExtractTracedTagProperties` (line 260).
- Emitter: `TracedInterceptorEmitter.AppendPropertyTagSetters` (line 281–299).
- Model: `TracedTagProperty(PropertyName, TagName, SkipIfNull, IsNullable, IsStatic)`.

**Resolution:** Option A (instance property capture at method entry, all `[TracedTag]`-decorated
properties on the containing type) with visibility filter.

---

### T-005 · Analyzer diagnostics for attribute misuse ✅

| Diagnostic                                                     | ID       | Severity | Status |
|----------------------------------------------------------------|----------|----------|--------|
| `[TracedTag]` without `[Traced]` on method or containing class | `QSD001` | Warning  | ✅      |
| `[NoTrace]` without class-level `[Traced]`                     | `QSD002` | Info     | ✅      |
| `[Traced]` on non-interceptable method                         | `QSD003` | Warning  | ✅      |
| `[Traced]` `ActivitySourceName` not registered                 | `QSD004` | Warning  | Dropped |
| `[TracedTag]` on `out`/`ref` parameter                         | `QSD005` | Error    | ✅      |

**QSD004 dropped:** Best-effort static analysis for `AddActivitySource(name)` registration was too
likely to produce false positives (source registered via config, DI, or extension methods). Not worth
the complexity.

**Implementation:** `TracedDiagnosticAnalyzer : DiagnosticAnalyzerBase` in
`qyl.servicedefaults.generator/Analyzers/TracedDiagnosticAnalyzer.cs`.

---

### T-006 · `[TracedTag]` `SkipIfDefault` — value type null-equivalent ✅

When `SkipIfDefault = true` and the parameter is a value type, emits:
```csharp
if (!EqualityComparer<T>.Default.Equals(value, default))
    activity.SetTag("key", value);
```

**Implementation:** `TracedInterceptorEmitter.AppendParameterTagSetters` (line 262–268).
**Model:** `TracedTagParameter.SkipIfDefault`, `TracedTagParameter.TypeName` (fully-qualified, for
`EqualityComparer<T>`).

---

### T-007 · `[TracedReturn]` — capture return value as span attribute ✅

`[return: TracedReturn("tag", Property = "Path.To.Member")]` captures the return value as a span
attribute. Without `Property`, falls back to `ToString()`. With `Property`, uses null-safe dotted
member access (`result?.Path?.To?.Member`).

**Implementation:** `TracedInterceptorEmitter.BuildReturnCaptureExpr` (line 302–310).
**Model:** `TracedReturnInfo(TagName, PropertyPath)`, `TracedCallSite.ReturnCapture`.

---

### T-008 · `[SpanEvent]` — deferred

Deferred to a separate epic. Not implemented.

---

## 4. C#-Specific Advantages Over Java

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
| SkipIfDefault                | ❌                                     | ✅ T-006                                                   |
| Return value capture         | ❌                                     | ✅ T-007                                                   |
| Property-level tags          | ❌                                     | ✅ T-004                                                   |
| IAsyncEnumerable lifetime    | ❌                                     | ✅ T-002                                                   |
| Diagnostics / build warnings | ❌                                     | ✅ T-005 (QSD001–QSD005)                                   |

---

## 5. Files Touched

| Story | Attributes (`servicedefaults`) | Analyzer (`servicedefaults.generator`) | Emitter                       | Model       |
|-------|--------------------------------|----------------------------------------|-------------------------------|-------------|
| T-001 | `TracedAttribute.cs`           | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-002 | —                              | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-003 | —                              | —                                      | `TracedInterceptorEmitter.cs` | —           |
| T-004 | —                              | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-005 | —                              | `TracedDiagnosticAnalyzer.cs`          | —                             | —           |
| T-006 | `TracedTagAttribute.cs`        | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |
| T-007 | `TracedReturnAttribute.cs`     | `TracedCallSiteAnalyzer.cs`            | `TracedInterceptorEmitter.cs` | `Models.cs` |

---

## 6. Resolved Design Decisions

These were open questions during planning. All resolved by implementation.

1. **T-004 — Property tag capture scope:** Option A — every `[TracedTag]`-decorated property on the
   containing type is captured for every span on that instance. Visibility filter applied.

2. **T-007 — Property accessor syntax:** Dotted member access (`Property = "Usage.InputTokenCount"`)
   with null-safe chaining (`result?.Usage?.InputTokenCount`). No `nameof` requirement.

3. **T-002 — `EnumeratorCancellation` passthrough:** The emitter forwards the `CancellationToken`
   parameter through `await foreach` iteration.

4. **T-005 — QSD004 ActivitySource registration check:** Dropped. False positive rate too high for
   best-effort static analysis.
