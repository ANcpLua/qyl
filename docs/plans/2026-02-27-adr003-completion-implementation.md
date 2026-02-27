# ADR-003 Completion Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close ADR-003 by wiring the Agent interceptor pipeline, consolidating dual APIs, and adding MSBuild property toggles.

**Architecture:** The source generator (`ServiceDefaultsSourceGenerator`) discovers call sites via analyzers, emits interceptor code via emitters. Each pipeline is gated by a runtime availability check and (after this work) an MSBuild property toggle. The runtime `ActivitySources` class registers named sources that OTel tracing/metrics subscribe to.

**Tech Stack:** Roslyn IIncrementalGenerator (netstandard2.0), ASP.NET Core (net10.0), OpenTelemetry SDK

---

### Task 1: Wire Agent Pipeline in Generator

**Files:**
- Modify: `src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs:142-163` (add pipeline block after Traced)
- Modify: `src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs:353-376` (add pipeline stage + generated file constants)

**Step 1: Add Agent pipeline block**

Insert after the Traced pipeline (line 162) and before the closing `}` of `Initialize()` (line 163):

```csharp
        // =====================================================================
        // AGENT INTERCEPTION PIPELINE
        // Discovers Microsoft.Agents.AI calls and wraps them with agent telemetry.
        // =====================================================================

        context.SyntaxProvider
            .CreateSyntaxProvider(
                AgentCallSiteAnalyzer.CouldBeAgentInvocation,
                AgentCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.AgentCallSitesDiscovered)
            .CombineWithCollected(qylRuntimeAvailable)
            .SelectAndReportExceptions(static (input, _) =>
            {
                if (!input.Right || input.Left.IsEmpty) return FileWithName.Empty;
                var sourceCode = AgentInterceptorEmitter.Emit(input.Left.AsImmutableArray());
                return string.IsNullOrEmpty(sourceCode)
                    ? FileWithName.Empty
                    : new FileWithName(GeneratedFile.AgentInterceptors, sourceCode);
            }, context, "QSG006")
            .AddSource(context);
```

**Step 2: Add pipeline stage constant**

In the `PipelineStage` class, add after `TracedCallSitesDiscovered`:

```csharp
        // Agent interception pipeline
        public const string AgentCallSitesDiscovered = nameof(AgentCallSitesDiscovered);
```

**Step 3: Add generated file constant**

In the `GeneratedFile` class, add:

```csharp
        public const string AgentInterceptors = "AgentIntercepts.g.cs";
```

**Step 4: Build to verify**

Run: `dotnet build src/qyl.servicedefaults.generator/qyl.servicedefaults.generator.csproj`
Expected: 0 errors, 0 warnings

**Step 5: Commit**

```bash
git add src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs
git commit -m "feat(generator): wire Agent interceptor pipeline (QSG006)"
```

---

### Task 2: Add Agent ActivitySource to Runtime

**Files:**
- Modify: `src/qyl.servicedefaults/Instrumentation/ActivitySources.cs`

**Step 1: Add Agent constant, source, and meter**

After the `Traced` constant (line 25) add:

```csharp
    /// <summary>Agent operations source name.</summary>
    public const string Agent = "qyl.agent";
```

After the `s_db` field (line 36) add:

```csharp
    private static ActivitySource? s_agent;
```

After the `s_genAiMeter` field (line 39) add:

```csharp
    private static Meter? s_agentMeter;
```

After `DbSource` property (line 45) add:

```csharp
    /// <summary>ActivitySource for agent instrumentation.</summary>
    public static ActivitySource AgentSource => s_agent ??= new ActivitySource(Agent, Version);
```

After `GenAiMeter` property (line 48) add:

```csharp
    /// <summary>Meter for agent metrics.</summary>
    public static Meter AgentMeter => s_agentMeter ??= new Meter(Agent, Version);
```

**Step 2: Register agent source in OTel config**

In `src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs`, add `"qyl.agent"` to the activity sources array (after `"Microsoft.Agents.AI"` on line 53):

The array `SGenAiActivitySources` already includes `"Microsoft.Agents.AI"` — that's the SDK's own source. Add the qyl wrapper source separately. After line 260 (`tracing.AddSource(ActivitySources.Traced);`), add:

```csharp
                tracing.AddSource(ActivitySources.Agent);
```

And after line 234 (`metrics.AddMeter(ActivitySources.Db);`), add:

```csharp
                metrics.AddMeter(ActivitySources.Agent);
```

**Step 3: Build to verify**

Run: `dotnet build src/qyl.servicedefaults/qyl.servicedefaults.csproj`
Expected: 0 errors, 0 warnings

**Step 4: Commit**

```bash
git add src/qyl.servicedefaults/Instrumentation/ActivitySources.cs src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs
git commit -m "feat(servicedefaults): add qyl.agent ActivitySource and Meter"
```

---

### Task 3: Consolidate Dual API — Merge Features into UseQyl

**Files:**
- Modify: `src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs`
- Modify: `src/qyl.servicedefaults/QylServiceDefaults.cs`

**Step 1: Add ExceptionCapture and ValidationStartupFilter to UseQyl()**

In `QylServiceDefaultsExtensions.UseQyl()` (around line 120, after `builder.Services.AddProblemDetails();`), add:

```csharp
        // Exception capture (AppDomain + TaskScheduler hooks)
        builder.Services.AddHostedService<ExceptionHookRegistrar>();
```

Add the using at the top of the file:

```csharp
using Qyl.ServiceDefaults.ErrorCapture;
```

**Step 2: Add ExceptionCaptureMiddleware and DevLogs to MapQylEndpoints()**

In `QylServiceDefaultsExtensions.MapQylEndpoints()`, after the health check mappings (line 158), add:

```csharp
        // Exception capture middleware
        app.UseMiddleware<ExceptionCaptureMiddleware>();
```

**Step 3: Slim QylServiceDefaults.cs to thin wrappers**

Replace the body of `TryUseQylConventions()` with a delegation to `UseQyl()`:

```csharp
    public static TBuilder TryUseQylConventions<TBuilder>(this TBuilder builder,
        Action<QylServiceDefaultsOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        // Thin wrapper for generator compatibility — delegates to UseQyl()
        builder.UseQyl();
        return builder;
    }
```

Replace `MapQylDefaultEndpoints()` body:

```csharp
    public static void MapQylDefaultEndpoints(this WebApplication app)
    {
        // Thin wrapper for generator compatibility — delegates to MapQylEndpoints()
        app.MapQylEndpoints();
    }
```

Remove the duplicate `ExceptionHookRegistrar` registration (line 134 — it's registered twice currently), the `ConfigureOpenTelemetry` method, `AddDefaultHealthChecks`, `ConfigureJson`, `ConfigureHttpClientDefaults`, `ValidationStartupFilter` registration, and all other logic that is now in `UseQyl()`. Keep only the two thin wrapper methods, the `DevLogsScript` constant, the `MapDevLogsEndpoint` method, and the `DevLogEntry` record.

**Step 4: Build to verify**

Run: `dotnet build src/qyl.servicedefaults/qyl.servicedefaults.csproj`
Expected: 0 errors, 0 warnings

**Step 5: Full test**

Run: `dotnet test tests/qyl.collector.tests/qyl.collector.tests.csproj --no-build # VERIFY`
Expected: All tests pass

**Step 6: Commit**

```bash
git add src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs src/qyl.servicedefaults/QylServiceDefaults.cs
git commit -m "refactor(servicedefaults): consolidate dual API into UseQyl entry point"
```

---

### Task 4: Add MSBuild Property Toggles

**Files:**
- Modify: `src/qyl.servicedefaults/qyl.servicedefaults.csproj`
- Modify: `src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs`

**Step 1: Add CompilerVisibleProperty declarations**

In `qyl.servicedefaults.csproj`, add a new ItemGroup after the ProjectReference ItemGroup:

```xml
  <!-- MSBuild property toggles for source generator pipelines -->
  <ItemGroup>
    <CompilerVisibleProperty Include="QylGenAi" />
    <CompilerVisibleProperty Include="QylDatabase" />
    <CompilerVisibleProperty Include="QylAgent" />
    <CompilerVisibleProperty Include="QylTraced" />
    <CompilerVisibleProperty Include="QylMeter" />
  </ItemGroup>
```

**Step 2: Add toggle helper method in generator**

In `ServiceDefaultsSourceGenerator.cs`, add a helper method in the runtime checks section (after `IsGenAiRuntimeReferenced`, around line 182):

```csharp
    /// <summary>
    ///     Reads an MSBuild toggle property. Returns true if absent or "true".
    /// </summary>
    private static bool IsPipelineEnabled(
        AnalyzerConfigOptionsProvider options,
        string propertyName)
    {
        return !options.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value)
               || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }
```

**Step 3: Create toggle provider**

In `Initialize()`, after the `qylRuntimeAvailable` and `genAiRuntimeAvailable` providers (around line 57), add:

```csharp
        // MSBuild property toggles (default: true when absent)
        var toggles = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => new PipelineToggles(
                GenAi: IsPipelineEnabled(options, "QylGenAi"),
                Database: IsPipelineEnabled(options, "QylDatabase"),
                Agent: IsPipelineEnabled(options, "QylAgent"),
                Traced: IsPipelineEnabled(options, "QylTraced"),
                Meter: IsPipelineEnabled(options, "QylMeter")))
            .WithTrackingName(PipelineStage.ToggleCheck);
```

**Step 4: Add PipelineToggles record and pipeline stage**

Add to the constants section at the bottom of the file:

```csharp
    /// <summary>
    ///     MSBuild property toggles for each generator pipeline.
    /// </summary>
    private sealed record PipelineToggles(
        bool GenAi,
        bool Database,
        bool Agent,
        bool Traced,
        bool Meter);
```

In `PipelineStage`, add:

```csharp
        public const string ToggleCheck = nameof(ToggleCheck);
```

**Step 5: Gate each pipeline with toggles**

For each of the 5 SDK pipelines (GenAI, Db, OTelTags, Meter, Traced, Agent), combine with toggles and check the flag. The pattern for GenAI becomes:

```csharp
        context.SyntaxProvider
            .CreateSyntaxProvider(
                GenAiCallSiteAnalyzer.CouldBeGenAiInvocation,
                GenAiCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.GenAiCallSitesDiscovered)
            .CombineWithCollected(genAiRuntimeAvailable)
            .CombineWithCollected(toggles)
            .SelectAndReportExceptions(static (input, _) =>
            {
                var ((callSites, runtimeAvailable), pipelineToggles) = input;
                if (!runtimeAvailable || !pipelineToggles.GenAi || callSites.IsEmpty) return FileWithName.Empty;
                var sourceCode = GenAiInterceptorEmitter.Emit(callSites.AsImmutableArray());
                return string.IsNullOrEmpty(sourceCode)
                    ? FileWithName.Empty
                    : new FileWithName(GeneratedFile.GenAiInterceptors, sourceCode);
            }, context, "QSG001")
            .AddSource(context);
```

Apply the same pattern to Db (`.Database`), Traced (`.Traced`), Meter (`.Meter`), Agent (`.Agent`). OTelTags pipeline does not get a toggle (always on — it's core infrastructure).

**Step 6: Build to verify**

Run: `dotnet build src/qyl.servicedefaults.generator/qyl.servicedefaults.generator.csproj`
Expected: 0 errors, 0 warnings

Run: `dotnet build src/qyl.servicedefaults/qyl.servicedefaults.csproj`
Expected: 0 errors, 0 warnings

**Step 7: Commit**

```bash
git add src/qyl.servicedefaults/qyl.servicedefaults.csproj src/qyl.servicedefaults.generator/ServiceDefaultsSourceGenerator.cs
git commit -m "feat(generator): add MSBuild property toggles for pipeline control"
```

---

### Task 5: Full Verification and ADR Status Update

**Files:**
- Modify: `docs/adrs/ADR-003-nuget-first-instrumentation.md:1` (update status)

**Step 1: Full build**

Run: `dotnet build`
Expected: 0 errors, 0 warnings

**Step 2: Full test suite**

Run: `dotnet test tests/qyl.collector.tests/qyl.collector.tests.csproj # VERIFY`
Expected: All tests pass

**Step 3: Update ADR status**

Change line 3 of `docs/adrs/ADR-003-nuget-first-instrumentation.md` from:

```
Status: Accepted
```

to:

```
Status: Done
```

**Step 4: Final commit**

```bash
git add docs/adrs/ADR-003-nuget-first-instrumentation.md
git commit -m "docs: mark ADR-003 as Done"
```
