# ADR-003 Completion Design

## Goal

Close ADR-003 (.NET Premium SDK) by wiring the dead Agent interceptor pipeline, consolidating the dual entry-point APIs into one, and adding MSBuild property toggles so users can disable individual generator pipelines at compile time.

## Current State

The source generator has 6 working pipelines (Builder, GenAI, Db, OTelTags, Meter, Traced). A 7th pipeline (Agent — for Microsoft.Agents.AI interception) has complete analyzer + emitter + model code but was never registered in the generator's `Initialize()` method.

Two competing entry-point APIs exist:
- `QylServiceDefaults.TryUseQylConventions()` — old API, called by generator interceptor
- `QylServiceDefaultsExtensions.UseQyl()` — newer API with auto-discovery

No MSBuild property toggle infrastructure exists (no `CompilerVisibleProperty`, no `AnalyzerConfigOptionsProvider` reads).

## Design

### Step 1: Wire Agent Pipeline

**Generator** (`ServiceDefaultsSourceGenerator.cs`): Add 7th pipeline block following the exact pattern of the other 5 SDK pipelines:
- Syntactic pre-filter: `AgentCallSiteAnalyzer.CouldBeAgentInvocation`
- Semantic analysis: `AgentCallSiteAnalyzer.ExtractCallSite`
- Gate: `qylRuntimeAvailable`
- Emit: `AgentInterceptorEmitter.Emit` → `"AgentIntercepts.g.cs"`
- Add `AgentCallSitesDiscovered` pipeline stage constant
- Add `AgentIntercepts` generated file constant

**Runtime** (`ActivitySources.cs`): Add `Agent = "qyl.agent"` constant + lazy `AgentSource` + `AgentMeter` properties. Same pattern as GenAi/Db/Traced.

**OTel config** (`QylServiceDefaultsExtensions.cs`): Register `"qyl.agent"` in activity source arrays and meter arrays.

### Step 2: Consolidate Dual API

Merge unique features from old `QylServiceDefaults.cs` into `QylServiceDefaultsExtensions.cs`:

| Feature from old API | Merge target |
|---------------------|--------------|
| `ExceptionCaptureMiddleware` + `ExceptionHookRegistrar` | `UseQyl()` and `MapQylEndpoints()` |
| `ValidationStartupFilter` | `UseQyl()` |
| `DevLogs` endpoint | `MapQylEndpoints()` |
| `ForwardedHeaders` / `HSTS` / `StaticAssets` | `QylOptions` properties |
| `QylServiceDefaultsOptions` | Merge into `QylOptions` |

After merge, slim `QylServiceDefaults.cs` to thin wrappers:
- `TryUseQylConventions()` → idempotent call to `UseQyl()`
- `MapQylDefaultEndpoints()` → call to `MapQylEndpoints()`

Generator's emitted code continues calling `TryUseQylConventions()` / `MapQylDefaultEndpoints()` unchanged.

### Step 3: MSBuild Property Toggles

**csproj** (`qyl.servicedefaults.csproj`):
```xml
<ItemGroup>
  <CompilerVisibleProperty Include="QylGenAi" />
  <CompilerVisibleProperty Include="QylDatabase" />
  <CompilerVisibleProperty Include="QylAgent" />
  <CompilerVisibleProperty Include="QylTraced" />
  <CompilerVisibleProperty Include="QylMeter" />
</ItemGroup>
```

**Generator**: Read `context.AnalyzerConfigOptionsProvider.GlobalOptions` for `build_property.QylGenAi` etc. Default `true` when absent. When `false`, skip that pipeline — no analyzer, no interceptor, zero overhead.

## Files Changed

| File | Action |
|------|--------|
| `servicedefaults.generator/ServiceDefaultsSourceGenerator.cs` | Add Agent pipeline + toggle reads |
| `servicedefaults/Instrumentation/ActivitySources.cs` | Add Agent constant + source + meter |
| `servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs` | Register agent sources, merge old API features |
| `servicedefaults/QylServiceDefaults.cs` | Slim to thin wrappers |
| `servicedefaults/qyl.servicedefaults.csproj` | Add CompilerVisibleProperty items |

## Out of Scope

- Dashboard "dimmer" UI (frontend, separate PR)
- NuGet packaging/publishing (CI/CD)
- ADR-005 copilot end-to-end verification

## Verification

1. `dotnet build` — 0 errors, 0 warnings
2. `dotnet test` — all tests pass
3. Generator emits `AgentIntercepts.g.cs` when `Microsoft.Agents.AI` types present
4. `<QylGenAi>false</QylGenAi>` suppresses GenAI interceptor generation
