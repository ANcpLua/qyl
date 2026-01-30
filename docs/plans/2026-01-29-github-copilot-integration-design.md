# qyl GitHub Copilot Integration Design

**Date:** 2026-01-29
**Status:** Approved
**Author:** Brainstorming session

## Overview

Integrate GitHub Copilot into qyl as both an **observable AI backend** and an **async workflow executor**. Unlike Aspire Dashboard's Copilot integration (IDE-dependent, chat-only), qyl's version runs standalone, executes declarative workflows, and auto-instruments all Copilot interactions with OTel GenAI semantic conventions.

## Goals

1. **Standalone operation** - No IDE dependency. Works via PAT, OAuth, or environment detection
2. **Auto-instrumentation** - Every Copilot call becomes OTel spans (GenAI semconv 1.39)
3. **Declarative workflows** - File-based `.qyl/workflows/*.md` definitions (like GitHub Actions)
4. **Dashboard integration** - Chat UI, workflow status, telemetry visualization
5. **Dogfooding** - qyl observes its own Copilot usage

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         qyl.collector                                │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │ Copilot      │  │ Workflow     │  │ Copilot                  │  │
│  │ Adapter      │──│ Engine       │──│ Instrumentation          │  │
│  │              │  │              │  │ (auto OTel)              │  │
│  └──────────────┘  └──────────────┘  └──────────────────────────┘  │
│         │                │                      │                   │
│         ▼                ▼                      ▼                   │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    OTLP Pipeline                              │  │
│  │              (spans, metrics, logs → DuckDB)                  │  │
│  └──────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────┤
│                         qyl.dashboard                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │ Chat UI      │  │ Workflow     │  │ Copilot                  │  │
│  │              │  │ Browser      │  │ Telemetry View           │  │
│  └──────────────┘  └──────────────┘  └──────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

## Components

### 1. Copilot Adapter (`qyl.copilot`)

New project wrapping `Microsoft.Agents.AI.GitHub.Copilot`:

```csharp
// Core adapter - wraps GitHubCopilotAgent with qyl-specific features
public sealed class QylCopilotAdapter : IAsyncDisposable
{
    // Auth providers (priority order)
    public static async Task<QylCopilotAdapter> CreateAsync(
        CopilotAuthOptions? options = null,
        CancellationToken ct = default);

    // Execute a workflow
    public IAsyncEnumerable<WorkflowUpdate> ExecuteWorkflowAsync(
        CopilotWorkflow workflow,
        WorkflowContext context,
        CancellationToken ct = default);

    // Interactive chat
    public IAsyncEnumerable<ChatUpdate> ChatAsync(
        string prompt,
        ChatContext? context = null,
        CancellationToken ct = default);
}
```

### 2. Authentication (`CopilotAuthOptions`)

Priority-based auth detection:

```csharp
public sealed class CopilotAuthOptions
{
    // Auto-detect from environment (gh CLI, VS Code, etc.)
    public bool AutoDetect { get; init; } = true;

    // Explicit PAT (fallback)
    public string? PersonalAccessToken { get; init; }

    // OAuth callback URL for dashboard flow
    public string? OAuthCallbackUrl { get; init; }
}

// Detection order:
// 1. GH_TOKEN / GITHUB_TOKEN env vars
// 2. gh auth token (GitHub CLI)
// 3. VS Code GitHub auth
// 4. Explicit PAT from config
// 5. OAuth flow via dashboard
```

### 3. Workflow Engine

File-based workflow definitions in `.qyl/workflows/`:

```markdown
<!-- .qyl/workflows/analyze-errors.md -->
---
name: Analyze Errors
description: Investigate error patterns and suggest fixes
tools: ['telemetry', 'codebase', 'search']
trigger: manual
---

# Error Analysis Workflow

Analyze recent errors from qyl telemetry and provide root cause analysis.

## Context
- Time range: {{timeRange}}
- Service filter: {{serviceFilter}}

## Instructions
1. Query recent errors from the telemetry store
2. Group by error type and frequency
3. For top 5 error types, analyze stack traces
4. Suggest remediation steps
5. Generate a summary report
```

### 4. Auto-Instrumentation

Every Copilot call automatically creates OTel spans:

```csharp
// Automatic - no user code needed
[Activity("gen_ai.copilot.chat")]
internal static partial class CopilotInstrumentation
{
    // GenAI semconv 1.39 attributes
    // gen_ai.system = "github_copilot"
    // gen_ai.request.model
    // gen_ai.usage.input_tokens
    // gen_ai.usage.output_tokens
    // gen_ai.response.finish_reason
}
```

### 5. Protocol Types (`qyl.protocol`)

New types for Copilot integration:

```csharp
// Workflow definition (parsed from .md files)
public sealed record CopilotWorkflow
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tools { get; init; } = [];
    public WorkflowTrigger Trigger { get; init; } = WorkflowTrigger.Manual;
    public required string Instructions { get; init; }
}

// Workflow execution state
public sealed record WorkflowExecution
{
    public required string Id { get; init; }
    public required string WorkflowName { get; init; }
    public WorkflowStatus Status { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
}

public enum WorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

### 6. REST API Endpoints

```yaml
# Copilot endpoints in collector
POST /api/copilot/chat
  - Interactive chat with streaming response (SSE)

GET /api/copilot/workflows
  - List available workflows from .qyl/workflows/

POST /api/copilot/workflows/{name}/run
  - Execute a workflow, returns execution ID

GET /api/copilot/executions/{id}
  - Get execution status/result

GET /api/copilot/executions/{id}/stream
  - Stream execution updates (SSE)

GET /api/copilot/auth/status
  - Check auth status, return capabilities

POST /api/copilot/auth/oauth/start
  - Start OAuth flow, return redirect URL

POST /api/copilot/auth/oauth/callback
  - Handle OAuth callback
```

### 7. Dashboard Components

React components for Copilot integration:

```typescript
// Chat panel (like Aspire but better)
<CopilotChat
  context={telemetryContext}
  onMessage={handleMessage}
/>

// Workflow browser
<WorkflowBrowser
  workflows={workflows}
  onRun={handleRunWorkflow}
/>

// Execution monitor
<ExecutionMonitor
  executionId={executionId}
  onComplete={handleComplete}
/>

// Telemetry integration
<CopilotTelemetryView
  traceId={traceId}
/>
```

## Implementation Phases

### Phase 1: Core Adapter + Chat (This PR)
- [ ] Create `qyl.copilot` project
- [ ] Implement `QylCopilotAdapter` wrapping `GitHubCopilotAgent`
- [ ] Add PAT and environment auth detection
- [ ] Auto-instrumentation with OTel GenAI spans
- [ ] Basic `/api/copilot/chat` endpoint with SSE
- [ ] Protocol types in `qyl.protocol`

### Phase 2: Workflow Engine
- [ ] Workflow file parser (markdown with YAML frontmatter)
- [ ] Workflow discovery from `.qyl/workflows/`
- [ ] Workflow execution engine
- [ ] Execution state persistence in DuckDB
- [ ] REST endpoints for workflow management

### Phase 3: Dashboard UI
- [ ] Chat panel component
- [ ] Workflow browser component
- [ ] Execution monitor with streaming updates
- [ ] Copilot telemetry visualization
- [ ] "Explain this" context menu integration

### Phase 4: OAuth + Advanced Features
- [ ] OAuth flow for dashboard-based auth
- [ ] Scheduled workflows (cron)
- [ ] Event-triggered workflows
- [ ] Workflow chaining

## Dependencies

```xml
<!-- qyl.copilot.csproj -->
<PackageReference Include="Microsoft.Agents.AI.GitHub.Copilot" Version="1.0.0-preview.260128.1" />
<PackageReference Include="Microsoft.Agents.AI.Abstractions" Version="1.0.0-preview.260128.1" />
```

## OTel Attributes

Following GenAI semconv 1.39:

| Attribute | Description |
|-----------|-------------|
| `gen_ai.system` | `github_copilot` |
| `gen_ai.operation.name` | `chat`, `workflow` |
| `gen_ai.request.model` | Model identifier |
| `gen_ai.usage.input_tokens` | Input token count |
| `gen_ai.usage.output_tokens` | Output token count |
| `gen_ai.response.finish_reason` | `stop`, `length`, `error` |
| `qyl.workflow.name` | Workflow name (custom) |
| `qyl.workflow.execution_id` | Execution ID (custom) |

## Security Considerations

1. **Token storage** - Tokens stored encrypted, never logged
2. **Scope limitation** - Request minimal GitHub scopes
3. **Audit trail** - All Copilot interactions logged as telemetry
4. **Rate limiting** - Respect GitHub API limits

## Success Criteria

1. Can chat with Copilot from qyl dashboard without IDE
2. All Copilot interactions visible in telemetry view
3. Can define and execute workflows from `.qyl/workflows/`
4. Auto-instrumentation works zero-config
5. Token usage and costs tracked
