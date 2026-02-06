# qyl.copilot - GitHub Copilot Integration

GitHub Copilot wrapper with auto-instrumentation and declarative workflows.

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
