# qyl.copilot - AI Agent (Microsoft Agent Framework)

IDE + chat surface of qyl. Adopts Microsoft Agent Framework (ADR-005) for provider-agnostic LLM access.

## Role in Architecture

One of three shells (browser, terminal, IDE). Copilot is the AI assistant that queries telemetry data:

- **Runtime**: Query observability data, explain traces, suggest fixes — from IDE or dashboard chat
- **Providers**: Ollama (local, free, default), OpenAI, Anthropic, any OpenAI-compatible endpoint
- **Without LLM**: all telemetry features work, agent features show "Configure LLM to enable"
- GitHub OAuth (ADR-002) provides identity — no separate user table needed

## Identity

| Property  | Value           |
|-----------|-----------------|
| SDK       | ANcpLua.NET.Sdk |
| Framework | net10.0         |

## ADR-005: Agent Framework Integration

```bash
# Provider config (any ONE — user's choice)
QYL_LLM_PROVIDER=ollama              # Local, free, private (default)
QYL_LLM_ENDPOINT=http://localhost:11434
QYL_LLM_MODEL=llama3

QYL_LLM_PROVIDER=openai              # Or OpenAI
QYL_LLM_API_KEY=sk-...
QYL_LLM_MODEL=gpt-4o-mini
```

## Features

- Microsoft Agent Framework (`Microsoft.Agents.AI`) for agent runtime
- Provider-agnostic: Ollama, OpenAI, Anthropic, any OpenAI-compatible
- 24 MCP tools from qyl.mcp (search_spans, get_trace, list_errors, etc.)
- OTel 1.39 GenAI semconv instrumentation
- Declarative workflows (`.qyl/workflows/*.md` with YAML frontmatter)
- Cascading auth: GH_TOKEN -> gh CLI -> PAT -> OAuth (ADR-002)
- Streaming via `IAsyncEnumerable`

## Structure

| Directory          | Purpose                           |
|--------------------|-----------------------------------|
| `Adapters/`        | Core GitHubCopilotAgent wrapper   |
| `Auth/`            | Cascading auth detection          |
| `Workflows/`       | Markdown workflow parser + engine |
| `Instrumentation/` | OTel spans + metrics              |

## OTel

Spans: `gen_ai.chat` (chat), `invoke_agent {name}` (workflow), `execute_tool {name}` (tool)
Metrics: `gen_ai.client.token.usage`, `gen_ai.client.operation.duration`, `qyl.copilot.workflow.*`

## Rules

- TimeProvider.System for time, Lock for sync, SemaphoreSlim for async
- All public APIs async with CancellationToken
- Follow OTel 1.39 GenAI semantic conventions
- Microsoft Agent Framework is MIT licensed, pin specific version (preview — handle breaking changes)
- No Azure requirement — Ollama (local, free) is default recommendation
