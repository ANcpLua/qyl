# qyl AOT Assessment

Scope: every project in the current tree, plus the real blockers that keep qyl off Native AOT today. Written from a read
of `Directory.Packages.props`, every `*.csproj`, `QylSerializerContext.cs`, and reflection-usage grep across `src/`.

**Date:** 2026-04-10
**TL;DR:** Zero projects set `PublishAot=true`. Zero projects set `IsAotCompatible=true`. The source code is remarkably
reflection-free (no `Activator.CreateInstance`, no `GetProperties()`/`GetMethods()`, no `Type.GetType`, no
`Assembly.Load` anywhere in `src/`). The blockers are almost entirely in **dependencies**, not in qyl code. `qyl.mcp` is
the single most realistic AOT target. `qyl.collector` is blocked on DuckDB.NET. `qyl.loom` is blocked on MAF's runtime
reflection for function calling — but you already built the generator pattern that would fix it (
`Qyl.Agents.Generator`).

---

## Why we're not AOT today

Three reasons, in descending order of difficulty:

1. **DuckDB.NET.Data.Full** uses reflection-based ADO.NET parameter binding. Hard blocker for `qyl.collector`. No
   in-process path around it today.
2. **Microsoft.Agents.AI.Hosting + AIFunctionFactory.Create** reflects tool methods at runtime to build `AIFunction`
   instances. Blocks `qyl.loom` and the autofix pipeline in `qyl.collector`.
3. **Nobody set `PublishAot=true`** on the shipping exes, and nobody set `IsAotCompatible=true` on leaf libraries — so
   even the projects that could be AOT today aren't getting the AOT analyzer warnings that would flag real problems.

---

## The four buckets you asked for

### Bucket A — 100% impossible (today)

| Project                                                                                                                                   | Why                                                                                                                                                                                                                                            |
|-------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `qyl.collector`                                                                                                                           | **DuckDB.NET.Data.Full** P/Invokes + reflection-based parameter binding. No source-gen alternative exists. Workarounds are out-of-process (DuckDB HTTP server / FlightSQL / CLI subprocess) which is a different architecture, not an AOT fix. |
| `Qyl.Agents.Abstractions`                                                                                                                 | Targets `netstandard2.0`. AOT requires net8.0+. Can't be AOT directly, only *consumed* by AOT apps. Would need multi-targeting.                                                                                                                |
| Any Roslyn generator (`qyl.instrumentation.generators`, `qyl.mcp.generators`, `qyl.collector.storage.generators`, `Qyl.Agents.Generator`) | Roslyn components run inside the compiler (`netstandard2.0` always). Correct as-is.                                                                                                                                                            |
| `eng/build` (NUKE)                                                                                                                        | Build tooling, not runtime. Correct as-is.                                                                                                                                                                                                     |
| All `tests/`                                                                                                                              | xUnit runner needs reflection. Tests never AOT.                                                                                                                                                                                                |

### Bucket B — Partial / blocked on dependencies

| Project                              | Blocker                                                                                                                                                                                        | What unblocks it                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `qyl.loom`                           | `Microsoft.Agents.AI` uses `AIFunctionFactory.Create(Delegate)` which reflects parameters at runtime. `ChatClient.GetResponseAsync` tool-calling path also reflects.                           | Two paths: **(a)** Wait for MAF to ship an AOT-friendly `AIFunctionFactoryOptions` with `JsonSerializerOptions` that uses a source-gen `JsonSerializerContext` (already partially there in 2025 builds — check `AIFunctionFactoryOptions.SerializerOptions`). **(b)** Build a `[LoomTool]` source generator that emits `AIFunction` subclasses with compile-time parameter metadata (Bucket D below — this is the same pattern as `Qyl.Agents.Generator`, extended). |
| `qyl.mcp`                            | `ModelContextProtocol` tool dispatch. If you use `.WithTools<T>()` scan or the attribute-based path, the SDK may still reflect on startup to build the tool catalog for 77 tools.              | Audit MCP SDK usage — newer versions (`ModelContextProtocol.Core` 0.2+) have source-gen for `[McpServerTool]`. You already have `qyl.mcp.generators` (interim) and `Qyl.Agents.Generator` (full). The latter is the correct destination. Replace SDK discovery with generator-produced registration at `WithTools()` call sites.                                                                                                                                     |
| `qyl.collector` *(non-DuckDB paths)* | gRPC+protobuf is AOT-OK in .NET 8+. ASP.NET minimal API is AOT-OK with `RequestDelegateGenerator`. `Microsoft.AspNetCore.Authentication.JwtBearer` is AOT-OK. The only hard blocker is DuckDB. | If you ever split collector into "ingest service (AOT)" + "storage service (JIT, owns DuckDB)", the ingest layer becomes Bucket C.                                                                                                                                                                                                                                                                                                                                   |

### Bucket C — Easy wins (today, no code changes or near-zero)

| Project                | Action                                                                                                                                                                                                           | Estimated effort                           |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------|
| `qyl.contracts`        | Add `<IsAotCompatible>true</IsAotCompatible>`. BCL-only leaf, records + enums + attributes. Zero code changes.                                                                                                   | 30 seconds.                                |
| `qyl.instrumentation`  | Add `<IsAotCompatible>true</IsAotCompatible>`. Audit `LoomGovernedAIFunction` and `LoomConcurrencyManager` for any `typeof(T).GetMethod` calls — if clean, done.                                                 | 1 hour audit + flag.                       |
| `Qyl.Agents` runtime   | Add `<IsAotCompatible>true</IsAotCompatible>`. Verify `JsonRpcMessage` uses a `JsonSerializerContext` (don't know yet — needs one-file check). `McpProtocolHandler` is the hot path.                             | 2 hours audit + flag + any context wiring. |
| `qyl.mcp` leaf helpers | Most `Tools/*.cs` files are simple DTOs calling `CollectorHelper` which is HttpClient. These are AOT-clean. The blockers are (1) DI registration of 77 tools and (2) MCP SDK dispatch. Source code itself: easy. | Bundled into qyl.mcp AOT push.             |

### Bucket D — Insanity, but you already started it

This is the interesting bucket. "Insanity" in the *good* sense: requires writing a source generator, but you have all
the prior art.

**D1 — `[LoomTool]` generator that replaces `AIFunctionFactory.Create(Delegate)`.**
You already have `Qyl.Agents.Generator` which converts `[Tool]`-annotated methods into MCP tool dispatch + schemas +
OTel. Extend it (or fork its `ToolExtractor` + `DispatchEmitter`) to emit `AIFunction` subclasses:

```csharp
// Hand-written
[LoomTool(Description = "Analyze a trace")]
public static Task<TraceAnalysis> AnalyzeTrace(string traceId, int depth = 3) { ... }

// Generated
internal sealed class AnalyzeTraceAiFunction : AIFunction
{
    public override string Name => "analyze_trace";
    public override string Description => "Analyze a trace";
    public override JsonElement JsonSchema => /* source-gen constant */;
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments args, CancellationToken ct)
    {
        var traceId = args.GetString("traceId");
        var depth = args.GetInt32OrDefault("depth", 3);
        return new(AnalyzeTrace(traceId, depth));
    }
}
```

This replaces the reflection-based `AIFunctionFactory.Create` path entirely. `qyl.loom` becomes AOT-publishable.

Effort: ~2 weeks for a focused engineer who has shipped Roslyn incremental generators. You already have
`ForAttributeWithMetadataName` patterns in `src/Qyl.Agents.Generator/McpServerGenerator.cs` and
`src/qyl.instrumentation.generators/Loom/Generation/`. `LoomToolOutputGenerator.cs` exists — it may already be 60% of
this.

**D2 — `[JsonSerializerContext]` expansion for Loom DTOs.**
Check what types flow through `Microsoft.Extensions.AI` tool-call JSON paths. Add them to a
`LoomSerializerContext : JsonSerializerContext`. This is "tedious but mechanical" — not insane.

**D3 — Interceptor for MCP tool registration.**
If `ModelContextProtocol.Core` still uses `services.AddMcpServer().WithTools<T>()` with reflection scan, write an
interceptor (C# 12+ `[InterceptsLocation]` attribute) that replaces that call with a direct list of tool registrations
emitted by `Qyl.Agents.Generator`. You already enable `InterceptorsNamespaces` in several csproj files (e.g.
`qyl.collector.csproj:8`, `qyl.loom.csproj`, `qyl.instrumentation.csproj`), so the infrastructure is in place. Medium
effort — ~3 days.

**D4 — Replace `Microsoft.Agents.AI` tool-calling loop with a generated state machine.**
Most ambitious. The MAF `ChatClient` response loop reflects on tool signatures every call. If you emit a per-tool-set
state machine (like how `async/await` is lowered), you get zero reflection at runtime. This is the nuclear option and
probably not worth it until MAF stabilizes. **Actual insanity — skip unless you're shipping this as a library.**

---

## Per-project matrix

| Project                   | Bucket            | Publishable as AOT exe    | Mark `IsAotCompatible=true` | Notes                                                               |
|---------------------------|-------------------|---------------------------|-----------------------------|---------------------------------------------------------------------|
| `qyl.collector`           | A (blocked)       | No — DuckDB               | No                          | Only path: out-of-process DuckDB.                                   |
| `qyl.mcp`                 | B → C after audit | **Yes, realistic target** | Yes                         | Single-file + AOT = excellent distribution story.                   |
| `qyl.loom`                | B → D             | Yes, after D1             | Yes, after D1               | `LoomGovernedAIFunction.cs` just landed — perfect place to hook in. |
| `qyl.contracts`           | C                 | n/a (library)             | Yes, trivially              | 30 seconds.                                                         |
| `qyl.instrumentation`     | C                 | n/a                       | Yes, after audit            | ActivitySource + semconv. Should be clean.                          |
| `Qyl.Agents` (runtime)    | C                 | n/a                       | Yes, after audit            | Verify `JsonRpcMessage` has a context.                              |
| `Qyl.Agents.Abstractions` | A                 | n/a                       | No (netstandard2.0)         | Multi-target net10.0 to mark it.                                    |
| Roslyn generators (×4)    | A                 | n/a                       | Already `false`             | Correct.                                                            |
| `eng/build`               | A                 | n/a                       | Already `false`             | Correct.                                                            |
| All tests                 | A                 | n/a                       | No                          | Correct.                                                            |

---

## Concrete next actions (ordered by ROI)

1. **Today, 5 minutes:** Add `<IsAotCompatible>true</IsAotCompatible>` to `qyl.contracts.csproj`. It's BCL-only records.
   Zero risk.
2. **This week, 1–2 hours:** Add `<IsAotCompatible>true</IsAotCompatible>` to `qyl.instrumentation.csproj` and fix any
   analyzer warnings that surface.
3. **This week, 2 hours:** Same for `Qyl.Agents.csproj` after verifying `JsonRpcMessage` has a serializer context.
4. **Next week, 1 day:** Turn on `<PublishAot>true</PublishAot>` in `qyl.mcp.csproj` *in a branch*. Let the AOT analyzer
   scream. Fix each `IL2xxx`/`IL3xxx` warning. Some will be in MCP SDK code (file issues upstream); some will be in your
   DI registration (fix with explicit registrations). This is the realistic first AOT-published exe.
5. **Q2 2026:** Extend `Qyl.Agents.Generator` with the `[LoomTool] → AIFunction subclass` emitter (Bucket D1). This is
   the unlock for `qyl.loom`.
6. **Parked:** `qyl.collector` AOT. Requires architectural change (split storage service) or upstream DuckDB.NET AOT
   work. Not a near-term target.

---

## How to find experts (search terms to grob)

Use these exact queries when hunting GitHub issues, blog posts, NuGet listings, or experts:

### General .NET AOT

- `"PublishAot" "IL2104"` — trim warnings from libraries you depend on
- `"dotnet publish" "-p:PublishAot=true" site:github.com` — reference publishing configs
- `"DynamicallyAccessedMembers" generator` — people who actually write AOT-safe reflection shims
- `"source generator" "AOT" "interceptor"` — the intersection where qyl lives

### DuckDB.NET AOT

- `DuckDB.NET AOT` — likely open issue
- `"DuckDB.NET" trimming` — upstream conversation
- `"DuckDB" FlightSQL .NET` — out-of-process escape hatch
- `"DuckDB" "HTTP server" C#` — alternative architecture

### Microsoft.Agents.AI / MEAI AOT

- `"Microsoft.Extensions.AI" AOT` — Microsoft's own AOT work
- `"AIFunctionFactory" "source generator"` — the exact thing you need generated
- `"ChatClient" "trim warning"` — known issues
- `"Microsoft.Agents.AI" "NativeAOT"` — MAF team statements

### ModelContextProtocol C# SDK AOT

- `"ModelContextProtocol" AOT` — SDK's own stance
- `"McpServerTool" "source generator"` — the attribute-driven path
- `"WithTools" reflection` — what the non-AOT path looks like
- `modelcontextprotocol/csharp-sdk trimming` — open issues

### gRPC + protobuf AOT

- `Grpc.AspNetCore AOT` — supported in .NET 8+
- `"Google.Protobuf" "NativeAOT"` — stable

### ASP.NET Core minimal API AOT

- `RequestDelegateGenerator AOT` — built-in .NET 8+ source generator
- `"Microsoft.AspNetCore" "RDG0001"` — AOT analyzer diagnostic prefix
- `"CreateSlimBuilder" vs "CreateBuilder"` — the AOT builder API

### People / accounts to follow

- `@davidfowl` on Twitter/GitHub — ASP.NET Core AOT
- `@stephentoub` on GitHub — runtime + Microsoft.Extensions.AI
- `@MichaelStefanFrank` — MCP C# SDK
- `@AndrewArnott` — source generators + interceptors
- `@SergioPedri` — Polly, source generator patterns

---

## Footguns

- **Don't mark a library `IsAotCompatible=true` without running a build first.** The flag turns on the AOT analyzer, and
  you will get warnings. Marking without fixing is worse than not marking (silently hides problems).
- **`PublishAot=true` transitively demands `IsTrimmable`.** Libraries you depend on that aren't AOT-ready will produce
  `IL2xxx` warnings treated as errors. Budget time for upstream issue filing.
- **`InvariantGlobalization=true`** is already set on `qyl.collector`, `qyl.mcp`, `qyl.loom`. Good — ICU is an AOT pain
  point.
- **`InterceptorsNamespaces`** is already enabled in key projects. This is infrastructure you'll want when (not if) you
  write interceptor-based DI registrations.
- **Single-file publish ≠ AOT.** `qyl.mcp` sets `PublishSingleFile=true` today; that still JITs at runtime. AOT is a
  second, separate flag.

---

## Where AL0131 fits

The previous QYL001 analyzer was ported to `ANcpLua.Analyzers` as `AL0131` (shipped in 1.24.0) and is auto-injected into
every qyl project via `ANcpLua.NET.Sdk`. It flags direct `ChatClient`/`OpenAI` usage that bypasses the
`Microsoft.Extensions.AI.IChatClient` instrumented wrapper. This matters for AOT because the instrumented wrapper is the
layer where `LoomGovernedAIFunction` lives, and `LoomGovernedAIFunction` is the thing a future `[LoomTool]` source
generator would plug into. Keeping that analyzer on keeps users out of the non-AOT path.

**Recommendation: cherry-pick `7ec362fa` onto main** before deleting the branch. Five seconds of safety.
