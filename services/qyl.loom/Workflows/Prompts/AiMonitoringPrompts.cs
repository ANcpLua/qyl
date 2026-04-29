// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Workflows.Prompts;

/// <summary>
///     MCP prompt for AI agent monitoring setup. Encodes the four load-bearing rules for
///     any <c>gen_ai.*</c> instrumentation: tracing-first, detection-first, sampling gate,
///     and PII opt-in for prompt/output capture.
/// </summary>
[McpServerPromptType]
internal sealed class AiMonitoringPrompts
{
    [McpServerPrompt(Name = "qyl.loom.setup_ai_monitoring",
        Title = "AI Monitoring (tracing-first, sampling-aware, PII opt-in)")]
    [Description("Sets up AI/LLM monitoring. Enforces tracing-first, sampling correctness, and PII-safe defaults.")]
    public static string Setup(
        [Description("JSON payload produced by loom_detect_dotnet. Required — used to confirm base SDK + tracing.")]
        string detectionJson,
        [Description(
            "Comma-separated list of AI SDKs in use (e.g. 'openai,anthropic,Microsoft.Extensions.AI'). Leave empty to auto-derive from detection.")]
        string? installedAiSdks = null,
        [Description("Current tracesSampleRate if known (e.g. '0.1'). Drives the sampling-gate directive.")]
        string? currentTracesSampleRate = null,
        [Description("true if the user has EXPLICITLY consented to prompt/output capture (PII). Defaults to false.")]
        bool piiCaptureConsent = false) =>
        $$"""
          You are configuring **AI Agent Monitoring** for the user's .NET project. This sits on top
          of the base SDK + tracing — it is NOT a standalone feature.

          ## The four hard rules (enforce all of them)

          ### 1. Tracing must already be on
          AI monitoring is a span-tree view. No tracing, no AI monitoring. Check the detection
          result: if `recommendations.tracing == false` OR the project does not set
          `TracesSampleRate > 0` (or a `TracesSampler`), stop and add base tracing first via
          `qyl.loom.setup_dotnet_tracing`.

          ### 2. Detect SDKs before configuring
          Do not add an integration for an SDK the project does not use. Installed AI SDKs:
          `{{installedAiSdks ?? "(derive from detection.ai_sdks)"}}`

          Detection payload:
          ```json
          {{detectionJson}}
          ```

          Map detected SDKs to actions:
          - **`OpenAI` NuGet** → Loom auto-instruments via OTel when the base `UseOpenTelemetry()`
            path is wired. If the project is JS/Node instead, use
            `Loom.openAIIntegration()` / `Loom.instrumentOpenAiClient(client)` (browser / Next.js).
          - **`Microsoft.Extensions.AI`** (IChatClient) → wrap with
            `chatClient.AsBuilder().UseOpenTelemetry("my.app.genai").Build()` so gen_ai spans emit.
            Loom.OpenTelemetry picks them up via `AddLoom()` on the TracerProvider.
          - **`Microsoft.Agents.AI`** (MAF) → wrap the agent with
            `agent.AsBuilder().UseOpenTelemetry("my.app.agent").Build()` — agent-level spans plus
            the chat-client spans from the line above.
          - **`Anthropic`**, **`Mscc.GenerativeAI.Microsoft`** (Google GenAI) → follow the same
            `IChatClient` pattern via Microsoft.Extensions.AI if the wrapper exists; otherwise
            manual instrumentation (see "Manual" below).
          - **No detected SDK** → fall back to manual instrumentation with `gen_ai.*` op names.

          ### 3. The sampling gate (load-bearing)
          Current `tracesSampleRate`: `{{currentTracesSampleRate ?? "(unknown — read from code)"}}`

          Agent runs are span trees. The root span's sampling decision cascades to every child;
          `gen_ai.*` children have no independent say. If the HTTP transaction that triggers the
          agent is sampled at <1.0 and no `tracesSampler` overrides the decision, you silently
          drop entire agent executions.

          If `tracesSampleRate < 1.0` AND no `tracesSampler` is configured, do **not** quietly set
          `tracesSampleRate = 1.0` — that inflates cost. Instead, add a `TracesSampler` that
          returns 1.0 for AI routes and falls back to the existing rate for everything else:

          ```csharp
          options.TracesSampler = ctx =>
          {
              var op   = ctx.TransactionContext.Operation;
              var name = ctx.TransactionContext.Name;

              // Standalone gen_ai root spans (cron, queue consumer, CLI)
              if (op.StartsWith("gen_ai.", StringComparison.Ordinal)) return 1.0;

              // HTTP routes that trigger AI calls
              if (op == "http.server" &&
                  (name.Contains("/api/chat") || name.Contains("/api/agent") ||
                   name.Contains("/api/ai")))
                  return 1.0;

              return 0.1;  // <- baseline, adjust to the project's current rate
          };
          ```

          If AI is the core product, skip the sampler and set `TracesSampleRate = 1.0` directly.
          State which you did and why.

          ### 4. PII / prompt capture is opt-in, explicit
          Prompt + output capture sends **user content** to Loom. That is PII by default. User consent
          flag passed in: `piiCaptureConsent={{piiCaptureConsent}}`.

          - `{{(piiCaptureConsent ? "CONSENT GIVEN" : "NO CONSENT")}}` — {{(piiCaptureConsent
              ? "enable `recordInputs` / `recordOutputs` (JS) or `include_prompts` / `send_default_pii` (Python) on the integration. Add a comment next to the flag pointing at the privacy policy."
              : "DO NOT set `recordInputs`, `recordOutputs`, `include_prompts`, or `send_default_pii = true`. Before enabling, the user must: (a) confirm their privacy policy permits capturing prompts / model responses, (b) confirm compliance with GDPR / CCPA / etc., (c) confirm data retention is appropriate for the sensitivity.")}}

          ## Fallback — metrics + logs at 100%
          If 100% tracing is not feasible, emit metrics and logs on every LLM call (these are
          independent of trace sampling):

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
          Requires `options.EnableMetrics = true` AND `options.EnableLogs = true` (both opt-in gates).

          ## Manual gen_ai span shape (when no integration exists)
          ```csharp
          var tx  = LoomSdk.StartTransaction("llm-invoke", "gen_ai.request");
          LoomSdk.ConfigureScope(s => s.Transaction = tx);

          var span = tx.StartChild("gen_ai.request", $"LLM request {model}");
          span.SetData("gen_ai.request.model", model);
          span.SetData("gen_ai.usage.input_tokens",  usage.InputTokens);
          span.SetData("gen_ai.usage.output_tokens", usage.OutputTokens);
          // opt-in, PII-sensitive:
          // span.SetData("gen_ai.request.messages",  JsonSerializer.Serialize(messages));
          span.Finish(SpanStatus.Ok);
          tx.Finish();
          ```

          Finish by verifying gen_ai.* spans appear in the Traces dashboard showing model, token
          counts, and latency.
          """;
}
