# TelemetryFabric — one fluent entry point for observed products

> **Status: design proposal (post-beta). Nothing here is implemented.** This document is
> the deliberate exception to the "no aspirational capabilities" rule that governs
> `docs/observability.md` — it exists precisely to hold the aspiration so product docs
> never have to. Origin: an ideation sketch in `services/qyl.collector/Program.cs`
> (preserved in `git stash`: "collector WIP ideation: TelemetryFabric sketch...") refined
> into this API design on 2026-07-07.

## Problem

The first sketch exposed telemetry subsystems as flat, top-level app calls:

```csharp
app.AddSampling();
app.AddBrowserTelemetry();
app.AddIosTelemetry();
app.AddAndroidTelemetry();
app.AddDashboard();
app.AddPrivacy();
```

That is bad API shape unless the caller is building the telemetry platform itself. For an
app/product author these are not separate mental tasks — they are implementation concerns
inside one vertical slice. Sampling without knowing the product surface is meaningless
(browser click events, iOS crashes, backend traces, and payment errors must not sample
the same way), and a dashboard is an output artifact, not a product feature.

## The governing rule: subsets stay abstracted

**No subsystem appears at the top level.** Every implementation concern (sampling,
pipelines, dashboards, export routing, privacy plumbing, contract tests) is a *derived
subset* of something the user actually declared — a product surface or a platform
contract. Subsets are reachable only through two narrow doors:

- `Override(...)` — scoped, explicit, rare; never the default path.
- `Generate(...)` — selects which artifacts to emit; never changes semantics.

A good telemetry platform API does not ask "do you want sampling / dashboards / privacy /
collector routing?". It asks:

```text
What products exist?
What semantics do they use?
Where should signals go?
What must never be collected?
What artifacts should be generated?
```

Then it wires the rest.

## The three layers

### 1. Product surfaces — things the user understands

```csharp
.ObserveProduct("browser-extension", ...)
.ObserveProduct("iphone", ...)
.ObserveProduct("android", ...)
.ObserveService("api", ...)
.ObserveWorker("ingestion", ...)
```

Each declared surface gets a complete default vertical slice. Declaring the surface *is*
the configuration:

```text
browser-extension telemetry slice
  ├─ event names
  ├─ trace/session correlation
  ├─ error capture
  ├─ privacy filter
  ├─ sampling defaults
  ├─ export route
  ├─ collector pipeline
  ├─ dashboard panels
  └─ generated tests
```

Default sampling inside a surface is semantic, not a single rate:

```text
normal click/event telemetry: sampled
errors: always kept
crashes: always kept
slow actions: kept
debug sessions: kept
sensitive fields: removed BEFORE sampling/export
```

### 2. Platform contracts — global rules

```csharp
.UseCollector("collector", port: 5100, project: "services/qyl.collector")
.UseSemantics("qyl.semconv.yaml")
.UsePrivacyDefaults()
```

The collector is declared once because it is an *infrastructure boundary* (one shared
ingestion point for every surface); users never configure per-surface pipelines — those
are generated:

```text
browser extension ─┐
iphone ────────────┤
android ───────────┤        browser-extension pipeline
api ───────────────┤        ios pipeline
worker ────────────┘        android pipeline
        ↓            ⇒      backend pipeline
   collector                error pipeline
        ↓                   metric pipeline
 storage/exporters          privacy pipeline
```

Privacy gets safe defaults but stays a *named, visible* concept — it is a hard boundary,
and telemetry without visible privacy rules is dangerous. You need to be able to see what
the platform will never collect:

```csharp
// fine to start
.UsePrivacyDefaults()

// the full contract, when you want it spelled out
.UsePrivacy(privacy => privacy
    .DefaultDenySensitive()
    .HashUserIdentifiers()
    .DropMessageBodies()
    .DropSecrets()
    .RequireConsentForBehavioralTelemetry())
```

### 3. Generated outputs — artifacts, not features

```csharp
.Generate(g => g
    .CollectorConfig()
    .ClientBindings()
    .Dashboards()
    .ContractTests())
```

Dashboards are derived from declared telemetry semantics — `ObserveProduct("browser-extension", ...)`
automatically yields panels for install/startup errors, active sessions, message
failures, export failures, top semantic events, and privacy drops. Avoid magical
catch-alls like `GenerateEverything()` / `ObserveProducts()` — the generator list is
short, explicit, and closed.

## The API

```csharp
var app = QylAppBuilder.Create(args);

app.AddTelemetryFabric("qyl", fabric => fabric
    .UseCollector("collector", port: 5100, project: "services/qyl.collector")
    .UseContracts(c => c
        .Semantics("qyl.semconv.yaml")
        .PrivacyDefaults())
    .Observe(o => o
        .ChromeExtension("browser-extension", "apps/qyl.extension")
        .iOS("iphone", "apps/qyl.ios")
        .Android("android", "apps/qyl.android")
        .AspNetCore("api", "services/qyl.api"))
    .Generate(g => g
        .CollectorConfig()
        .ClientBindings()
        .Dashboards()
        .ContractTests()));

await app.Build().RunAsync();
```

Four concepts, total: **collector, contracts, observed things, generated things.**

Overrides are explicit, scoped to a surface, and rare — default first, override second:

```csharp
.Override("browser-extension", x => x
    .Sampling(s => s.Events(0.10).Errors(1.00).DebugSessions(1.00))
    .Privacy(p => p.Drop("dom.text").Drop("input.value").Hash("user.id")))
```

## Grounding in today's qyl

The fabric is a composition layer over machinery that already exists or has precedent:

| Fabric concept | Existing qyl precedent |
|---|---|
| `UseCollector(...)` | `services/qyl.collector` (OTLP :4317/:4318, REST/dashboard :5100) |
| `UseContracts(.Semantics(...))` | `Qyl.OpenTelemetry.SemanticConventions` packages + `eng/config/collector-semantic-policy.json` → generated `CollectorSemanticAttributeCatalog.g.cs` |
| `UseContracts(.PrivacyDefaults())` | collector redaction (`AddQylRedactors`), attribute allow/deny lists in the semantic policy |
| `.Generate(...)` | eng/build generator targets (`GenerateCollectorSemanticAttributeCatalog`, `GenerateDependencyList`, `GenerateLibraryVersions`) + Verify gates keeping generated output honest |
| `.Observe(.iOS(...))` | qyl.mobile ([TelemetryDashboardViewModel.swift](https://github.com/ANcpLua/qyl.mobile/blob/main/ios/TelemetryObserver/ViewModels/TelemetryDashboardViewModel.swift)) |
| `QylAppBuilder` | `packages/Qyl.Run` (fluent AppHost-equivalent, subprocess orchestration) |

The existing generate→verify pattern (generator target + Verify gate that fails when
output is stale or hand-edited) is exactly the contract `.Generate(...)` should keep:
generated artifacts are committed, verified, and never hand-edited.

## Open questions

1. **Where does the fabric run?** `.Observe`/`.Generate` are build-time concerns
   (codegen, dashboards, tests) while `.UseCollector` is runtime wiring. Likely split:
   the fabric *declaration* lives in the app, a NUKE/eng target consumes it for
   generation — mirroring how the semantic catalog works today.
2. **Naming vs C# conventions** — `.iOS(...)` breaks PascalCase analyzers; probably
   `.Ios(...)` with an `iOS` display name, or suppress narrowly.
3. **Relationship to `Qyl.Run`** — does `QylAppBuilder.Create` wrap Qyl.Run's builder or
   replace it? The fabric should be a Qyl.Run extension, not a second host model.
4. **`ObserveWorker`/`ObserveService` defaults** — the backend slice differs from client
   surfaces (no crashes/offline queue; instead request/queue latency, error budgets).
   Needs its own default-slice table before implementation.
