# qyl.copilot - GitHub Copilot Integration

IDE surface of qyl. First contact after `docker compose up` — Copilot activates, asks for GitHub OAuth (one click), then guides the developer through setup from inside their editor.

## Role in Architecture

One of three shells (browser, terminal, IDE). Copilot is the onboarding guide and ongoing AI assistant:
- **Onboarding**: `docker compose up` → Copilot activates → GitHub OAuth (proves you're a dev) → guided setup
- **Runtime**: Query observability data, explain traces, suggest fixes — all from the IDE
- GitHub OAuth doubles as identity (no user table needed) and abuse protection for the remote instance

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |

## Features

- Wraps `Microsoft.Agents.AI.GitHub.Copilot`
- OTel 1.39 GenAI semconv instrumentation
- Declarative workflows (`.qyl/workflows/*.md` with YAML frontmatter)
- Cascading auth: GH_TOKEN -> gh CLI -> PAT -> OAuth
- Streaming via `IAsyncEnumerable`

## Structure

| Directory | Purpose |
|-----------|---------|
| `Adapters/` | Core GitHubCopilotAgent wrapper |
| `Auth/` | Cascading auth detection |
| `Workflows/` | Markdown workflow parser + engine |
| `Instrumentation/` | OTel spans + metrics |

## OTel

Spans: `gen_ai.chat`, `gen_ai.workflow`, `gen_ai.execute_tool`
Metrics: `gen_ai.client.token.usage`, `gen_ai.client.operation.duration`, `qyl.copilot.workflow.*`

## Rules

- TimeProvider.System for time, Lock for sync, SemaphoreSlim for async
- All public APIs async with CancellationToken
- Follow OTel 1.39 GenAI semantic conventions
