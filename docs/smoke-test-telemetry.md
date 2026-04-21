# Telemetry Smoke Test (post-collapse)

After PR #138 collapsed the `[AgentTraced]` attribute stack onto MAF's fluent middleware pipeline, two tests guard the emission path:

1. **Unit-level (already committed)** — `tests/qyl.collector.tests/Instrumentation/WithQylTelemetryEmissionTests.cs` exercises `WithQylTelemetry` end-to-end against a `FakeChatClient`, captures Activities via `ActivityCollector("qyl.genai")`, and asserts GenAI semconv 1.40 attributes (`gen_ai.operation.name` / `gen_ai.request.model` / provider identification) are present. Runs in CI on every PR.

2. **Live-provider (this document)** — the Aspire-dashboard smoke test described below. Run this once after any change to `AgentLlmFactory`, `WithQylTelemetry`, or MAF-middleware ordering.

## Prerequisites

- Docker running
- One provider configured in user-secrets or env (pick one):

```bash
# OpenAI
export QYL_AGENT_API_KEY=sk-...
export QYL_AGENT_MODEL=gpt-4o-mini

# Azure OpenAI
export QYL_AGENT_API_KEY=...
export QYL_AGENT_MODEL=gpt-4o
export QYL_AGENT_ENDPOINT=https://<resource>.openai.azure.com/
```

## 1. Start Aspire Dashboard

```bash
docker run --rm -it -d \
    -p 18888:18888 \
    -p 4317:18889 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

- Web UI: <http://localhost:18888>
- OTLP gRPC endpoint (where qyl will export): `http://localhost:4317`

## 2. Point qyl at the dashboard

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
export OTEL_SERVICE_NAME=qyl.mcp-smoke
export ENABLE_SENSITIVE_DATA=true   # dev-only — captures prompts/completions
```

## 3. Invoke a qyl.mcp tool that uses `AgentLlmFactory`

Fastest path: call `assisted_query` (it builds an `IChatClient` via `AgentLlmFactory.TryCreate`).

```bash
dotnet run --project services/qyl.mcp -- --tool assisted_query --args '{"query":"show error spans from the last hour"}'
```

(Or wire it through your MCP client of choice — Claude Code, Cursor, a raw `curl` to the Streamable HTTP endpoint.)

## 4. Verify spans in the dashboard

Open <http://localhost:18888> → **Traces**. Filter by `service.name=qyl.mcp-smoke`.

### Expected — `qyl.genai` source (chat-client layer)

- Operation name: `chat <model-id>` (e.g. `chat gpt-4o-mini`)
- Tags (GenAI semconv 1.40):
  - `gen_ai.operation.name=chat`
  - `gen_ai.provider.name=openai` (or legacy `gen_ai.system=openai`)
  - `gen_ai.request.model=gpt-4o-mini`
  - `gen_ai.response.model=gpt-4o-mini-2024-07-18` (or whatever the provider returned)
  - `gen_ai.usage.input_tokens` + `gen_ai.usage.output_tokens` (numeric)
- With `ENABLE_SENSITIVE_DATA=true`: span events carrying `gen_ai.prompt` / `gen_ai.completion` content.

### Expected — `qyl.agent` source (agent layer)

Only present if the code path went through an `AIAgent` wrapped with `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` (i.e. Loom agents). Raw `IChatClient` calls from `AgentLlmFactory` alone only emit `qyl.genai`.

- Operation name: `invoke_agent <agent-name>`
- Tags: `gen_ai.agent.name`, `gen_ai.agent.id`
- Child span: the `qyl.genai` chat span from the inner `IChatClient` call.

### Expected — `qyl.mcp` source (tool layer)

- Operation name: `execute_tool <tool-name>`
- Tags: `gen_ai.tool.name`, `gen_ai.tool.call.id`
- Parent of the `qyl.genai` span when the tool drove a chat call.

## 5. Failure signals (what to look for)

| Symptom | Likely cause |
|---|---|
| No `qyl.genai` span at all | `AgentLlmFactory.TryCreate` returned `null` (check `QYL_AGENT_API_KEY`), **or** the `.WithQylTelemetry()` call got dropped in a refactor |
| `qyl.genai` present but no provider tag | `FakeChatClient`-shaped test but real `Metadata` missing in provider — upstream `Microsoft.Extensions.AI` regression |
| `qyl.agent` span but no `qyl.genai` child | Agent wrapped with `UseOpenTelemetry("qyl.agent")` but inner `IChatClient` bypassed `WithQylTelemetry` — check composition root |
| Spans present but tool-execution span missing | `ToolDecoratingChatClient` layer stripped — `WithQylTelemetry` should always wrap it outside `OpenTelemetryChatClient` |

## 6. Teardown

```bash
docker stop aspire-dashboard
```

---

**Cadence:** run this smoke test once after any PR that touches `internal/qyl.instrumentation/Instrumentation/GenAi/`, `services/qyl.mcp/Agents/AgentLlmFactory.cs`, the MAF / `Microsoft.Extensions.AI` package versions, or the `ANcpLua.Analyzers` package (which ships `WithQylTelemetry`'s wrapping rules).
