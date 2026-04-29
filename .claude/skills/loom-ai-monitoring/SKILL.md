---
name: loom-ai-monitoring
description: Configure qyl AI agent monitoring for gen_ai.* traffic (LLM calls, agents, tools, handoffs) in the user's .NET project. Enforces the four hard rules — tracing-first, detect SDKs before configuring, sampling gate via tracesSampler for AI routes, and PII opt-in for prompt/output capture. Never defaults to capturing prompts.
---

# loom-ai-monitoring — qyl AI agent monitoring

Sits on top of `loom-sdk-onboarding`. AI monitoring is NOT a standalone feature — it requires base SDK + tracing already wired.

## Invoke this skill when
- The user asks to monitor LLM / OpenAI / Anthropic / Google GenAI / Vercel AI / LangChain / `Microsoft.Extensions.AI` / `Microsoft.Agents.AI` calls.
- The user mentions token usage, model latency, AI costs, or `gen_ai.*` spans.
- The user mentions `tracesSampler` / `traces_sampler` / sampling for AI traffic.
- `loom-workflow` routed to `SetupAiMonitoring`.

## The four hard rules (enforce ALL)

### 1. Tracing must already be on

AI monitoring is a span-tree view. No tracing, no AI monitoring. If `TracesSampleRate` is unset (or both it and `TracesSampler` are unset), stop and run `loom-sdk-onboarding` → `qyl.loom.setup_dotnet_tracing` first.

### 2. Detect SDKs before configuring

Do not add an integration for an SDK the project does not use. Use `loom_detect_dotnet` to enumerate `aiSdks`, then map to actions:

| SDK detected | Action |
|---|---|
| `OpenAI` NuGet | qyl auto-instruments via OTel when the base `UseOpenTelemetry()` is wired. |
| `Microsoft.Extensions.AI` | Wrap `chatClient.AsBuilder().UseOpenTelemetry("my.app.genai").Build()`. Loom.OpenTelemetry picks up gen_ai spans via `AddLoom()` on TracerProvider. |
| `Microsoft.Agents.AI` (MAF) | Wrap `agent.AsBuilder().UseOpenTelemetry("my.app.agent").Build()` — agent-layer spans on top of chat-client spans. |
| `Anthropic`, `Mscc.GenerativeAI.Microsoft` (Google GenAI) | Same `IChatClient` pattern via Microsoft.Extensions.AI when available. Otherwise manual instrumentation. |
| No detected SDK | Manual instrumentation with `gen_ai.*` op names. |

### 3. The sampling gate (load-bearing)

Agent runs are span trees. The **root** span's sampling decision cascades to every child — `gen_ai.*` children have no independent vote. If the HTTP transaction that triggers the agent is sampled at < 1.0 and no `TracesSampler` overrides, entire agent executions silently drop.

Do **not** quietly set `tracesSampleRate = 1.0` — it inflates cost. Use `TracesSampler` to hold AI routes at 1.0 while keeping the project-wide baseline:

```csharp
options.TracesSampler = ctx =>
{
    var op   = ctx.TransactionContext.Operation;
    var name = ctx.TransactionContext.Name;

    // Standalone gen_ai root spans (cron, queue consumer, CLI)
    if (op.StartsWith("gen_ai.", StringComparison.Ordinal)) return 1.0;

    // HTTP routes that trigger AI calls
    if (op == "http.server" &&
        (name.Contains("/api/chat") || name.Contains("/api/agent") || name.Contains("/api/ai")))
        return 1.0;

    return 0.1; // baseline — adjust to the project's rate
};
```

If AI is the core product, skip the sampler and use `TracesSampleRate = 1.0`. State which you did and why.

### 4. PII / prompt capture is opt-in, explicit

Prompt + output capture sends **user content** to qyl. That is PII by default. **Never default to on.** Before enabling:
1. Confirm the app's privacy policy permits capturing prompts / model responses.
2. Confirm compliance with applicable regulations (GDPR, CCPA, etc.).
3. Confirm qyl data retention is appropriate for the sensitivity.

Opt-in flags per SDK: `recordInputs` / `recordOutputs` (JS), `include_prompts` / `send_default_pii` (Python), `SendDefaultPii` + capture flag on the integration (.NET).

## How to run this skill

### Step 1 — Detect

```
loom_detect_dotnet(repoRoot)
```

Pull `aiSdks`, `recommendations.tracing`, and the current `LoomOptions` config (read `Program.cs` or `appsettings.json` for the existing `TracesSampleRate`).

### Step 2 — Confirm prerequisites

- `recommendations.tracing == true` OR tracing already configured → continue.
- Otherwise → route the user to `loom-sdk-onboarding` first.

### Step 3 — Fetch the AI monitoring prompt

```
qyl.loom.setup_ai_monitoring(
  detectionJson,
  installedAiSdks?,
  currentTracesSampleRate?,
  piiCaptureConsent=false  // default false, only true after explicit user confirmation
)
```

The prompt returns the full AI monitoring directive with the four hard rules, the sampler template, the PII-consent gate, and the fallback (metrics + logs at 100% when tracing cannot be at 100%).

### Step 4 — Verify

After wiring, make an LLM call. Confirm `gen_ai.*` spans appear in the qyl Traces dashboard with `gen_ai.request.model`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`.

## Fallback — metrics + logs at 100%

If 100% tracing is not feasible, emit metrics + logs on every LLM call (these are independent of trace sampling):

```csharp
LoomSdk.Metrics.EmitDistribution("gen_ai.token_usage", usage.TotalTokens,
    MeasurementUnit.None, new Dictionary<string, object> { ["model"] = model });
LoomSdk.Metrics.EmitCounter("gen_ai.calls", 1,
    new Dictionary<string, object> { ["model"] = model, ["status"] = status });

LoomSdk.Logger.LogInfo(log =>
{
    log.SetAttribute("gen_ai.model", model);
    log.SetAttribute("gen_ai.usage.input_tokens", usage.InputTokens);
    log.SetAttribute("gen_ai.usage.output_tokens", usage.OutputTokens);
    log.SetAttribute("gen_ai.latency_ms", latencyMs);
}, "LLM call");
```

Requires `options.EnableMetrics = true` AND `options.EnableLogs = true` (both opt-in gates from `loom-sdk-onboarding`).

## MCP surface this skill uses

| Tool | Purpose |
|---|---|
| `loom_detect_dotnet` | Surfaces `aiSdks` and base tracing status. |

| Prompt | Purpose |
|---|---|
| `qyl.loom.setup_ai_monitoring` | Full AI monitoring directive with the four hard rules, sampler template, PII gate, fallback. |
| `qyl.loom.setup_dotnet_tracing` | Prerequisite — base tracing setup. |

## Key attributes on manual `gen_ai.*` spans

| Attribute | Description |
|---|---|
| `gen_ai.request.model` | Model identifier |
| `gen_ai.usage.input_tokens` | Input token count |
| `gen_ai.usage.output_tokens` | Output token count |
| `gen_ai.agent.name` | Agent identifier (for MAF) |
| `gen_ai.tool.name` | Tool identifier |
| `gen_ai.request.messages` | **Opt-in only** — PII |
| `gen_ai.response.text` | **Opt-in only** — PII |

## Hard rules

- **No AI monitoring without tracing.** Non-negotiable.
- **Do not default PII on.** `recordInputs`, `recordOutputs`, `include_prompts`, `send_default_pii=true` are opt-in after explicit user consent with documented compliance.
- **TracesSampler over global TracesSampleRate = 1.0.** Unless AI is the core product.
- **Detect before integrate.** No `qyl.openAIIntegration()` / `anthropicAIIntegration()` on a project that does not import the respective SDK.

## Troubleshooting

| Issue | Fix |
|---|---|
| gen_ai spans missing despite sampler returning 1.0 | Parent HTTP transaction was sampled at a lower rate. Add the route to the sampler, or make the gen_ai route standalone. |
| `TracesSampler` not called for gen_ai spans | Expected. It fires on root spans only. Sample the parent HTTP route instead. |
| Token counts missing | Some providers omit usage for streaming responses. |
| Prompts missing from spans | Opt-in — `recordInputs` / `include_prompts`. Confirm PII consent before enabling. |
| Vercel AI spans empty | Requires `experimental_telemetry: { isEnabled: true }` on each `generateText` call. |
