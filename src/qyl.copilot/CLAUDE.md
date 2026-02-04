# qyl.copilot - GitHub Copilot Integration

GitHub Copilot integration with qyl observability features.

## Identity

| Property  | Value               |
|-----------|---------------------|
| SDK       | ANcpLua.NET.Sdk     |
| Framework | net10.0             |
| Role      | copilot-integration |

## Purpose

Wraps `Microsoft.Agents.AI.GitHub.Copilot` with:

- Auto-instrumentation (OTel 1.39 GenAI semconv)
- Declarative workflows (`.qyl/workflows/*.md`)
- Cascading authentication
- Streaming execution (`IAsyncEnumerable`)

## Directory Structure

```
Adapters/
  QylCopilotAdapter.cs        # Core wrapper for GitHubCopilotAgent

Auth/
  CopilotAuthProvider.cs      # Cascading auth detection

Workflows/
  WorkflowParser.cs           # Parse .md files with YAML frontmatter
  WorkflowEngine.cs           # Orchestrate workflow execution

Instrumentation/
  CopilotInstrumentation.cs   # OTel spans, metrics, traces
```

## Authentication Cascade

Tries these sources in order:

```
1. GH_TOKEN / GITHUB_TOKEN env vars
2. gh auth token (GitHub CLI)
3. Explicit PAT from config
4. OAuth flow via dashboard
```

## Workflow Format

```markdown
<!-- .qyl/workflows/analyze-errors.md -->
---
name: Analyze Errors
description: Investigate error patterns
tools: ['telemetry', 'codebase']
trigger: manual
---

# Error Analysis

Analyze recent errors from qyl telemetry.

## Instructions
1. Query errors from {{timeRange}}
2. Group by error type
3. Suggest fixes
```

## Usage

```csharp
// Register services
services.AddQylCopilot(options =>
{
    options.AuthOptions.AutoDetect = true;
    options.WorkflowsDirectory = ".qyl/workflows";
});

// Add OTel instrumentation
builder.AddQylCopilotInstrumentation();
builder.AddQylCopilotMetrics();

// Use adapter
var adapter = await adapterFactory.GetAdapterAsync(ct);
await foreach (var update in adapter.ChatAsync("prompt", ct: ct))
{
    Console.Write(update.Content);
}

// Execute workflow
var engine = await engineFactory.GetEngineAsync(ct);
await foreach (var update in engine.ExecuteAsync("analyze-errors", parameters, ct: ct))
{
    // Handle streaming updates
}
```

## OTel Attributes

| Attribute               | Value                |
|-------------------------|----------------------|
| `gen_ai.system`         | `github_copilot`     |
| `gen_ai.operation.name` | `chat` or `workflow` |

### Spans

- `gen_ai.chat` - Chat completion
- `gen_ai.workflow` - Workflow execution
- `gen_ai.execute_tool` - Tool invocation

### Metrics

- `gen_ai.client.token.usage` - Token counts
- `gen_ai.client.operation.duration` - Latency
- `qyl.copilot.workflow.duration` - Workflow timing
- `qyl.copilot.workflow.executions` - Execution count

## Dependencies

### Packages

- `Microsoft.Agents.AI.GitHub.Copilot` - Copilot SDK
- `Microsoft.Agents.AI.Abstractions` - Agent abstractions
- `OpenTelemetry.Api` - OTel instrumentation

### Project References

- `qyl.protocol` - Shared types
- `qyl.servicedefaults` - Aspire defaults

## Rules

- Use `TimeProvider.System` for time operations
- Use `Lock` for sync, `SemaphoreSlim` for async
- All public APIs async with `CancellationToken`
- Stream everything with `IAsyncEnumerable`
- Follow OTel 1.39 GenAI semantic conventions
