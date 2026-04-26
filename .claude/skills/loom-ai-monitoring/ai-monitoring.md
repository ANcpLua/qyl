# AI Monitoring — qyl .NET reference

Reference companion to `./SKILL.md`. This file holds the long-form code samples, provider
matrix, and semantic-convention map. The SKILL is the workflow entry point; this is the
lookup table.

## Minimum versions

| Package / surface                           | Version in this repo                         | Binding                                   |
|---------------------------------------------|----------------------------------------------|-------------------------------------------|
| `Qyl.Client`                                | tracks repo (`TargetFramework=net10.0`)      | emits `gen_ai.*` spans when wired         |
| `Qyl.Telemetry` (`QylAttr.Genai.*`)         | generated from `eng/semconv/model/qyl/*.yaml`| Weaver-emitted constants                  |
| `Qyl.OpenTelemetry.SemanticConventions.Incubating` (`GenAiAttributes.*`) | schema `1.40.0`            | standard `gen_ai.*` attribute keys        |
| `Microsoft.Extensions.AI`                   | `10.5.0`                                     | `IChatClient` + `OpenTelemetryChatClient` |
| `Microsoft.Agents.AI` (MAF)                 | `1.3.0`                                      | `AIAgent` + `AIAgentBuilder`              |

`net10.0` is the only target. Older TFMs are not supported — the extension surface leans on
`required init`, `field` contextual keyword, and C# 14 primary constructors.

## The four hard rules

### 1. Tracing must already be on

AI monitoring is a span tree. No tracing pipeline, no AI monitoring. `builder.UseQyl()` (alias
`AddQylServiceDefaults()`) from `Qyl.Instrumentation` registers the tracer, meter, and the
`gen_ai.*` activity sources. If that call is missing, stop and run
`loom-sdk-onboarding` → `qyl.loom.setup_dotnet_tracing` first.

Verification — there must be an exporter endpoint (explicit or auto-discovered). The tracer
only registers sources when `OTEL_EXPORTER_OTLP_ENDPOINT` resolves to a reachable collector;
otherwise `HasListeners()` returns `false` and every `StartActivity()` returns `null` at zero
cost — by design, but no spans ship.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddQylServiceDefaults();   // registers gen_ai activity sources + meters
var app = builder.Build();
app.MapQylEndpoints();
```

The source list wired by `QylServiceDefaultsExtensions` already covers `OpenAI.*`,
`Azure.AI.OpenAI.*`, `Anthropic.*`, `Microsoft.Extensions.AI`, `Microsoft.Agents.AI`, and
`Experimental.Microsoft.Agents.AI` — nothing extra to add for these providers.

### 2. Detect SDKs before configuring

Do not wrap an SDK the project does not reference. Run `loom_detect_dotnet(repoRoot)` to
enumerate `aiSdks`, then map to the wrapping call.

| Detected SDK                   | Action                                                                                          |
|--------------------------------|-------------------------------------------------------------------------------------------------|
| `OpenAI` NuGet                 | Obtain `IChatClient` via `Microsoft.Extensions.AI.OpenAI`, then wrap with `UseQylTelemetry`.    |
| `Microsoft.Extensions.AI`      | Wrap the `IChatClient` with `chatClient.WithQylTelemetry(...)` or the builder form.             |
| `Microsoft.Agents.AI` (MAF)    | Wrap the `AIAgent` with `agent.AsBuilder().UseQylAgentTelemetry().Build()`.                     |
| `Anthropic` (official SDK)     | Same — route through `IChatClient`, then `WithQylTelemetry`. Direct SDK use is QYL0137.         |
| `Mscc.GenerativeAI.Microsoft`  | Google Gemini via `IChatClient`; wrap with `WithQylTelemetry`.                                  |
| `OllamaSharp`                  | Expose `IChatClient` via `ollama.AsChatClient()`, then `WithQylTelemetry`.                      |
| LM Studio / any OpenAI-compatible endpoint | Construct an `OpenAIClient` pointed at the LM Studio endpoint, then wrap as usual. |
| None detected                  | Manual instrumentation — emit `gen_ai.*` spans with `GenAiAttributes` (see below).              |

Direct SDK client instantiation outside a sanctioned `IXxxChatClientBuilder`
(`**/Clients/*ChatClientBuilder.cs` or `**/Factories/*ChatClientFactory.cs`) is flagged by
analyzer **QYL0137**. Tests and samples are exempt. Fix at source — do not suppress.

### 3. The sampling gate (load-bearing)

Agent runs are span trees. The **root** span's sampling decision cascades to every child;
`gen_ai.*` children have no independent vote. If the HTTP transaction that triggers the
agent is sampled at less than 1.0 and no sampler overrides, entire agent executions silently
drop.

`QylOptions.ObservabilityMode` picks the sampler:

| Mode         | Sampler                                             | Use                                                |
|--------------|-----------------------------------------------------|----------------------------------------------------|
| `AlwaysOn`   | `ParentBasedSampler(AlwaysOnSampler)` (default)     | Dev / staging / small prod — full visibility.      |
| `Warm`       | `ParentBasedSampler(AlwaysOffSampler)`              | Distributed trace continuity, selective export.    |
| `OnDemand`   | `AlwaysOffSampler` + activation via `POST /api/v1/observe` | Prod with subscription-gated observation.   |

For finer routing (sample AI routes at 1.0 while the baseline stays lower), hook
`ConfigureTracing` on `QylOptions` and install an OTel `Sampler` that inspects the root
activity name.

```csharp
builder.AddQylServiceDefaults(options =>
{
    options.ConfigureTracing = tracing =>
    {
        tracing.SetSampler(new AiRouteSampler(baselineRate: 0.1));
    };
});
```

If AI is the product, leave `ObservabilityMode = AlwaysOn`. State which choice you made and
why in the PR description.

### 4. PII opt-in (never default on)

Prompt and response capture sends **user content**. Capture is off by default. The gate is
`OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` — do not hard-code `true` in code.

```bash
# Dev only, after privacy review
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

Before flipping it on, confirm:
1. Privacy policy permits capturing prompts / model responses.
2. Compliance with GDPR / CCPA / sector regulations.
3. Collector retention is appropriate for the sensitivity.

`GenAiInstrumentation.WithQylTelemetry(enableSensitiveData: true)` also exists, but the
`CLAUDE.md` guidance is: pass `null` (or omit) and let the env var decide. The overload is
reserved for tests that explicitly opt in and assert on captured content.

## Wrapping a chat client

### Extension form — works without DI

```csharp
using Qyl.Instrumentation.Instrumentation.GenAi;
using OpenAI;
using Microsoft.Extensions.AI;

var raw = new OpenAIClient(apiKey).GetChatClient("gpt-5.4-mini").AsIChatClient();
IChatClient chat = raw.WithQylTelemetry(sourceName: "my.app.genai");
```

`WithQylTelemetry` is idempotent — if the inner client is already an `OpenTelemetryChatClient`
or a `ToolDecoratingChatClient`, it returns as-is. Safe to apply at multiple layers.

### Builder form — composition root with DI

```csharp
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

services.AddSingleton<IChatClient>(sp =>
{
    var raw = new OpenAIClient(apiKey).GetChatClient("gpt-5.4-mini").AsIChatClient();
    return new ChatClientBuilder(raw)
        .UseQylTelemetry(sourceName: "my.app.genai")
        .Build(sp);
});
```

`UseQylTelemetry` (builder form) composes three decorators in order:
1. `UseOpenTelemetry(sourceName)` — standard `gen_ai.*` spans via
   `Microsoft.Extensions.AI.OpenTelemetryChatClient`.
2. `UseLogging()` — `ILogger` breadcrumbs (falls back to `NullLoggerFactory` when DI is empty).
3. `ToolDecoratingChatClient(WrapTool)` — wraps `AIFunction`s in `TracedAIFunction` so tool
   calls emit `gen_ai.execute_tool` spans with the same source.

### Agent form — Microsoft.Agents.AI (MAF)

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

AIAgent agent = chat
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "LoomAutofix",
        Description = "Loom headless autofix runner",
        ChatOptions = new ChatOptions { Instructions = systemPrompt },
    })
    .AsBuilder()
    .UseQylAgentTelemetry()        // source = "qyl.agent" + UseLogging()
    .Build();
```

`UseQylAgentTelemetry` scopes `gen_ai.*` spans to the `AIAgent.RunAsync` boundary; the chat
client wrap handles per-completion spans underneath. Both are needed for a complete tree.
Analyzer **QYL0135** flags any `AIAgent` constructed without this wrap.

## Provider mapping

Every provider below terminates at an `IChatClient` — that is the only seam qyl instruments.
The right-hand column shows the construction; the wrap is always identical
(`.WithQylTelemetry(...)` or `new ChatClientBuilder(raw).UseQylTelemetry(...).Build(sp)`).

| Provider           | NuGet                                              | Construct the `IChatClient`                                                                 |
|--------------------|----------------------------------------------------|----------------------------------------------------------------------------------------------|
| OpenAI             | `OpenAI` + `Microsoft.Extensions.AI.OpenAI`        | `new OpenAIClient(key).GetChatClient(model).AsIChatClient()`                                |
| Azure OpenAI       | `Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI` | `new AzureOpenAIClient(endpoint, cred).GetChatClient(deployment).AsIChatClient()`         |
| Anthropic          | `Anthropic` (official SDK)                         | `new AnthropicClient(key).Messages.AsIChatClient(model)`                                    |
| Google Gemini      | `Mscc.GenerativeAI.Microsoft`                      | `new GeminiChatClient(apiKey, model)` (already `IChatClient`)                               |
| Ollama             | `OllamaSharp`                                      | `new OllamaApiClient(uri).AsChatClient(model)`                                              |
| LM Studio          | `OpenAI` (endpoint override)                       | `new OpenAIClient(key: "lm-studio", options).GetChatClient(model).AsIChatClient()` where `options.Endpoint` points at LM Studio |

Note: direct `OpenAIClient` / `AzureOpenAIClient` / `AnthropicClient` / `OllamaApiClient`
construction is **analyzer-flagged outside a ChatClientBuilder** (QYL0137). In production
code, put these inside `services/<yours>/Clients/XxxChatClientBuilder.cs` and inject the
`IXxxChatClientBuilder` abstraction.

## Composition-root wiring

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddQylServiceDefaults(options =>
{
    options.ObservabilityMode = ObservabilityMode.AlwaysOn;
});

// Provider-specific builder lives under services/my.app/Clients/
builder.Services.AddSingleton<IMyAppChatClientBuilder, MyAppChatClientBuilder>();
builder.Services.AddSingleton<IChatClient>(sp =>
    sp.GetRequiredService<IMyAppChatClientBuilder>().BuildChatClient("default"));

// MAF agents wrap at the agent layer
builder.Services.AddSingleton<AIAgent>(sp =>
    sp.GetRequiredService<IChatClient>()
        .AsAIAgent(new ChatClientAgentOptions { Name = "MyAgent" })
        .AsBuilder()
        .UseQylAgentTelemetry()
        .Build());

var app = builder.Build();
app.MapQylEndpoints();
app.Run();
```

The pattern is **three builders, one composition root** — see `eng/SKILL.md` for the Apex
contract (`IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow code).

## Manual `gen_ai.*` instrumentation

When a provider has no `IChatClient` adapter, emit spans by hand. Use the standard
attribute keys from `GenAiAttributes` and the qyl-scoped keys from `QylAttr.Genai`.

```csharp
using System.Diagnostics;
using Qyl.Instrumentation.Instrumentation;                                         // ActivitySources
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using QylAttr;                                                                      // Genai.*

using var activity = ActivitySources.GenAiSource.StartActivity(
    $"chat {model}", ActivityKind.Client);

activity?.SetTag(GenAiAttributes.OperationName, "chat");
activity?.SetTag(GenAiAttributes.ProviderName, "cohere");
activity?.SetTag(GenAiAttributes.RequestModel, model);
activity?.SetTag(Genai.WorkflowId, workflowId);      // qyl-scoped — links to autofix run

try
{
    var result = await provider.ChatAsync(messages, ct).ConfigureAwait(false);
    activity?.SetTag(GenAiAttributes.UsageInputTokens, result.InputTokens);
    activity?.SetTag(GenAiAttributes.UsageOutputTokens, result.OutputTokens);
    activity?.SetTag(Genai.InputTokens, result.InputTokens);
    activity?.SetTag(Genai.OutputTokens, result.OutputTokens);
    activity?.SetTag(Genai.Model, model);
    activity?.SetTag(Genai.Provider, "cohere");
    activity?.SetTag(Genai.CostUsd, pricing.Compute(model, result));   // omit when unknown
    return result;
}
catch (Exception ex)
{
    GenAiInstrumentation.RecordException(activity, ex);
    throw;
}
```

For streaming, use `GenAiInstrumentation.ExecuteStreamingAsync` — it handles the
`[EnumeratorCancellation]` boilerplate, records per-item token counts, and closes the
activity cleanly on exception paths.

For tool execution spans, use `GenAiInstrumentation.StartToolExecutionSpan(toolName, callId,
toolType)` followed by `RecordToolResult(activity, success, error)`.

### Attribute cheat sheet

| Attribute                                 | Source                                          | When                              |
|-------------------------------------------|-------------------------------------------------|-----------------------------------|
| `gen_ai.operation.name`                   | `GenAiAttributes.OperationName`                 | Always — `chat` / `embeddings` / `execute_tool` |
| `gen_ai.provider.name`                    | `GenAiAttributes.ProviderName`                  | Always                            |
| `gen_ai.request.model`                    | `GenAiAttributes.RequestModel`                  | Always                            |
| `gen_ai.usage.input_tokens`               | `GenAiAttributes.UsageInputTokens`              | When known                        |
| `gen_ai.usage.output_tokens`              | `GenAiAttributes.UsageOutputTokens`             | When known                        |
| `gen_ai.tool.name` / `.call.id` / `.type` | `GenAiAttributes.ToolName` / `ToolCallId` / `ToolType` | On `execute_tool` spans    |
| `qyl.genai.model`                         | `QylAttr.Genai.Model`                           | Always — qyl promoted column      |
| `qyl.genai.provider`                      | `QylAttr.Genai.Provider`                        | Always                            |
| `qyl.genai.input_tokens`                  | `QylAttr.Genai.InputTokens`                     | When known                        |
| `qyl.genai.output_tokens`                 | `QylAttr.Genai.OutputTokens`                    | When known                        |
| `qyl.genai.cost_usd`                      | `QylAttr.Genai.CostUsd`                         | Only when pricing is known — do NOT emit a zero placeholder |
| `qyl.genai.cache_hit`                     | `QylAttr.Genai.CacheHit`                        | When the provider reports cache behavior |
| `qyl.genai.workflow_id`                   | `QylAttr.Genai.WorkflowId`                      | Links span to `fixrun_*` / `run_*` |

The `qyl.genai.*` set is generated from `eng/semconv/model/qyl/genai.yaml` via
`./eng/semconv/run-weaver.sh` and `nuke GenerateSemconv`. If a new qyl-scoped attribute is
needed, add it to the YAML — never hard-code a `"qyl.genai.*"` string.

## Troubleshooting

| Symptom                                                  | Cause                                                                  | Fix                                                                                      |
|----------------------------------------------------------|------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| No `gen_ai.*` spans in the collector                     | No exporter endpoint resolved — sources never registered               | Set `OTEL_EXPORTER_OTLP_ENDPOINT` or let auto-discovery run; `app.Logs` prints the hit   |
| `gen_ai.*` spans missing despite provider wired          | Chat client not wrapped with `UseQylTelemetry`/`WithQylTelemetry`      | Wrap at the composition root; analyzer QYL0135 / QYL0137 points at the offending site    |
| Spans present, no prompt / response content              | PII gate off (expected default)                                        | Set `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` after privacy review       |
| Agent `RunAsync` shows as a plain HTTP span, no `invoke_agent` child | Missing `.AsBuilder().UseQylAgentTelemetry().Build()`          | Wrap the `AIAgent`; QYL0135 catches this                                                 |
| `gen_ai.usage.*_tokens` missing on streaming             | Provider did not emit usage chunk                                      | Use `ExecuteStreamingAsync` (counts yielded items) or read usage from final stream chunk |
| `qyl.genai.cost_usd` missing                             | Provider pricing table absent for this model                           | Extend pricing table OR omit the tag — never emit a zero placeholder                     |
| Root AI span sampled but children dropped                | Root sampler in the caller transaction said "drop"                     | Ship `ParentBased`; raise the parent route's rate, not just the gen_ai source            |
| Double-wrapped spans (duplicate `chat <model>` entries)  | Applied `UseQylTelemetry` at builder and again at `WithQylTelemetry`   | Pick one layer. `WithQylTelemetry` short-circuits on re-entry; still remove the double call |
| `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` set in prod | Forbidden — leaks PII by default                                | Remove from prod config; gate behind a dev-only profile                                  |

## Related files

- `/Users/ancplua/qyl/internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`
  — `WithQylTelemetry`, `UseQylTelemetry`, `UseQylAgentTelemetry`, manual `ExecuteAsync` /
  `ExecuteStreamingAsync`.
- `/Users/ancplua/qyl/internal/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs`
  — `builder.AddQylServiceDefaults()` / `builder.UseQyl()`; registered activity sources and
  meter list.
- `/Users/ancplua/qyl/packages/Qyl.Telemetry/Conventions/Qyl.g.cs` — `QylAttr.Genai.*`.
- `/Users/ancplua/qyl/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Attributes/GenAi/GenAiAttributes.g.cs`
  — standard `gen_ai.*` attribute keys.
- `/Users/ancplua/qyl/eng/semconv/model/qyl/genai.yaml` — source for the qyl-scoped attributes.
- `/Users/ancplua/qyl/internal/qyl.instrumentation.generators/Analyzers/ChatClientBuilderBypassAnalyzer.cs`
  — QYL0137 (provider-client escape detector).

For the workflow entry-point see `./SKILL.md`.
