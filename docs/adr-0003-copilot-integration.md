# ADR-0003: GitHub Copilot Integration Architecture

## Status

Accepted

## Date

2026-01-29

## Context

qyl needs AI-assisted telemetry analysis capabilities. Users should be able to:
- Query telemetry data using natural language
- Execute automated workflows for common analysis tasks
- Get AI-powered insights without leaving the qyl ecosystem

Existing options like Aspire Dashboard's Copilot integration require IDE dependencies and lack observability into AI interactions themselves.

## Decision

Implement a standalone GitHub Copilot integration (`qyl.copilot`) with the following architecture:

### 1. Standalone Authentication (No IDE Dependency)

**Decision**: Implement cascading authentication that auto-detects available credentials.

```
Environment (GITHUB_TOKEN/GH_TOKEN)
    ↓ fallback
GitHub CLI (gh auth token)
    ↓ fallback
Personal Access Token (configured)
    ↓ fallback
OAuth Device Flow (interactive)
```

**Rationale**: Users should be able to use qyl.copilot from any environment - CI/CD, containers, terminals - without requiring VS Code or other IDEs.

### 2. Composition Over Inheritance for Instrumentation

**Decision**: Use composition pattern for instrumented agent wrapper instead of inheriting from `AIAgent`.

```csharp
// Composition (chosen) — wraps QylCopilotAdapter with OTel spans
// See src/qyl.copilot/Adapters/QylCopilotAdapter.cs
// Instrumentation: src/qyl.copilot/Instrumentation/CopilotInstrumentation.cs

// NOT inheritance (rejected)
public class InstrumentedAgent : AIAgent
{
    protected override Task<...> RunCoreAsync(...) { }
}
```

**Rationale**:
- `AIAgent` has complex abstract members (`RunCoreAsync`, `RunCoreStreamingAsync`, `GetNewThread`, `DeserializeThread`) that change between SDK versions
- Composition provides stable API regardless of SDK changes
- Easier to test and mock

### 3. OTel GenAI Semantic Conventions 1.39

**Decision**: All Copilot interactions emit OTel traces and metrics following GenAI semconv 1.39.

**Span Attributes**:
- `gen_ai.provider.name`: `github_copilot`
- `gen_ai.operation.name`: `chat`, `invoke_agent`, `execute_tool`, `workflow`
- `gen_ai.request.model`: `github-copilot`
- `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`
- `gen_ai.response.finish_reasons`

**Metrics**:
- `gen_ai.client.token.usage` (Histogram) - Token consumption
- `gen_ai.client.operation.duration` (Histogram) - Operation latency

**Custom qyl Attributes**:
- `qyl.workflow.name` - Workflow being executed
- `qyl.workflow.execution_id` - Unique execution identifier
- `qyl.copilot.session_id` - Conversation session

**Rationale**: Dogfooding - qyl observes AI observability, so our own AI integration must be fully observable.

### 4. File-Based Workflow Definitions

**Decision**: Workflows defined as Markdown files with YAML frontmatter in `.qyl/workflows/`.

```markdown
---
name: error-analysis
description: Analyze error patterns in telemetry
trigger: manual
tools:
  - query
  - analyze
---

# Error Analysis Workflow

Analyze the following error data and identify:
1. Root cause patterns
2. Affected services
3. Remediation suggestions

## Context
{{telemetry_data}}
```

**Rationale**:
- Version-controlled alongside code
- Human-readable and editable
- Supports template parameters (`{{variable}}`)
- Similar pattern to GitHub Actions workflows

### 5. Streaming-First API

**Decision**: All execution methods return `IAsyncEnumerable<StreamUpdate>`.

```csharp
public async IAsyncEnumerable<StreamUpdate> ChatAsync(string prompt, ...)
public async IAsyncEnumerable<StreamUpdate> ExecuteWorkflowAsync(CopilotWorkflow workflow, ...)
```

**StreamUpdate kinds**:
- `Content` - Text chunk from AI
- `ToolCall` - Tool being invoked
- `ToolResult` - Tool execution result
- `Progress` - Workflow progress (0-100%)
- `Completed` - Final result with metadata
- `Error` - Execution failure

**Rationale**:
- Better UX for long-running operations
- Enables real-time progress display in dashboard
- Allows cancellation mid-stream

## Consequences

### Positive

1. **No IDE lock-in**: Works in any environment with GitHub auth
2. **Full observability**: Every AI interaction is traced and metered
3. **Extensible workflows**: Users can define custom analysis workflows
4. **Streaming UX**: Real-time feedback for all operations
5. **SDK resilience**: Composition pattern survives SDK API changes

### Negative

1. **Preview SDK dependency**: `Microsoft.Agents.AI.GitHub.Copilot` is in preview
2. **GitHub account required**: No offline/local LLM support (yet)
3. **Workflow complexity**: YAML frontmatter parsing adds complexity

### Risks

1. **SDK breaking changes**: Preview SDK may change significantly
   - *Mitigation*: Composition pattern isolates us from internal API changes

2. **Token costs**: Heavy usage could incur significant Copilot costs
   - *Mitigation*: Token usage metrics enable cost monitoring

3. **Rate limiting**: GitHub may rate-limit API calls
   - *Mitigation*: Expose rate limit info via auth status

## Alternatives Considered

### 1. Direct OpenAI/Anthropic Integration

**Rejected**: Would require separate API keys, doesn't leverage existing GitHub Copilot subscriptions.

### 2. Aspire Dashboard Pattern (IDE Extension)

**Rejected**: Requires VS Code/Rider, not suitable for server/container deployments.

### 3. Inheritance-Based Instrumentation

**Rejected**: `AIAgent` abstract API is complex and unstable in preview.

## References

- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [GitHub Copilot SDK](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-types/github-copilot-agent)
- [OTel GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
