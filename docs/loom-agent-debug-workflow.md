# Loom Agent Debug Workflow

Interactive agent debugging experience — from issue discovery to automated PR.

## Workflow Stages

### Stage 1: Pre-Investigation & Context Gathering

**UI:** Loom sidebar panel showing "Initial Guess", "What Happened", "In the Trace", "Resources".

**Endpoint:** `GET /api/v1/loom/{issueId}/insight` → `LoomInsightService`

| Capability | Domain | Implementation |
|---|---|---|
| Issue Summarization | Summarization | `LoomInsightService.GenerateInsightAsync()` (LLM + heuristic fallback) |
| Trace Summarization | Summarization | Events + stack traces from `IssueService.GetEventsAsync()` |
| Fetch Issues (Context) | Search | `DuckDbStore.GetIssueByIdAsync()` |

### Stage 2: Agent Kickoff & Tool Execution

**UI:** "Start Loom" button with optional user context input. Streaming status: "Ingesting qyl data..."

**Endpoint:** `POST /api/v1/loom/{issueId}/explore` (SSE) → `LoomExplorerService`

| Capability | Domain | Implementation |
|---|---|---|
| Explorer Agent | Explorer | `LoomExplorerService.ExploreAsync()` via `IChatClient` streaming |
| MCP Platform (60+ tools) | MCP | Available via `qyl.mcp` (future: tool injection into explorer) |

### Stage 3: Interactive Reasoning & Steering

**UI:** Streaming agent monologue, "Interrupt me..." input field.

| Capability | Domain | Implementation |
|---|---|---|
| AG-UI + Workflows | Protocol | `StreamUpdate` SSE events (`PROGRESS`, `CONTENT`, `COMPLETED`) |
| Agent Continuation | SDK | `CancellationToken` — SSE disconnect interrupts agent |

### Stage 4: Root Cause Synthesis

**UI:** Chronological breakdown of the failure path (expandable steps, root cause highlighted).

| Capability | Domain | Implementation |
|---|---|---|
| Root Cause Analysis | Autofix | `LoomExplorerService` → `LoomRootCause` with `LoomCausalStep[]` |

### Stage 5: Resolution Planning & Handoff

**UI:** Solution summary, implementation checklist (expandable/removable steps), "Code It Up" CTA.

**Endpoint:** `POST /api/v1/loom/{issueId}/code-it-up` → `AutofixOrchestrator` + `PrCreationService`

| Capability | Domain | Implementation |
|---|---|---|
| Autofix Orchestration | Autofix | `AutofixOrchestrator.CreateFixRunAsync()` |
| Autofix Pipeline | Autofix | `AutofixAgentService` (5-step background pipeline) → `PrCreationService` |

## API Surface

```
GET  /api/v1/loom/{issueId}/insight     → LoomInsight (JSON)
POST /api/v1/loom/{issueId}/explore     → SSE stream (StreamUpdate events)
POST /api/v1/loom/{issueId}/code-it-up  → LoomCodeItUpResponse (JSON)
```

## SSE Event Types

| Event | StreamUpdateKind | Content |
|-------|-----------------|---------|
| `PROGRESS` | Progress | Status message + percentage |
| `CONTENT` | Content | Agent monologue text (streaming) |
| `CONTENT` (toolName=root_cause) | Content | `LoomRootCause` JSON |
| `CONTENT` (toolName=solution) | Content | `LoomSolution` JSON |
| `COMPLETED` | Completed | Terminal event |
| `ERROR` | Error | Error message |

## Key Files

| File | Purpose |
|------|---------|
| `LoomModels.cs` | DTOs: LoomInsight, LoomRootCause, LoomSolution, LoomCausalStep, LoomSolutionStep |
| `LoomPrompts.cs` | LLM prompts for insight generation, explorer monologue, solution planning |
| `LoomInsightService.cs` | Pre-investigation insight (LLM + heuristic fallback) |
| `LoomExplorerService.cs` | Interactive streaming agent (SSE) |
| `LoomEndpoints.cs` | REST + SSE endpoint wiring |
| `AutofixPrompts.cs` | Background pipeline prompts (RCA, solution, diff, confidence, impact, triage) |
