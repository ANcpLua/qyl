# Conversational Loom Design

> **Status:** Draft
> **Date:** 2026-03-12
> **Supersedes:** Parts of `docs/loom-design.md` (state machine, endpoints, session model)
> **Depends on:** `2026-03-11-aicontextprovider-integration-design.md` for shared issue-context plumbing only (`IIssueContextSource`, `IssueContextBuilder`, `ObservabilityContextProvider`, `QylAgentBuilder` provider wiring)
> **Supersedes in that doc:** Loom runtime decisions, session model, endpoint direction, and `QylCopilotAdapter` lifecycle

## Problem

Loom's current interactive pipeline (`LoomExplorerService`) is a one-shot streaming call over raw `IChatClient`. The user cannot intervene mid-investigation, cannot continue the conversation after results are delivered, and cannot pick up a background autofix session that already found something interesting. Three systems exist that should be one:

1. **Background autofix** (`TriagePipelineService` + `AutofixAgentService`) — autonomous 5-step pipeline, no user interaction
2. **Interactive Loom** (`LoomExplorerService`) — one-shot SSE stream, custom `StreamUpdateKind` protocol, no conversation
3. **AG-UI chat** (`CopilotAguiEndpoints`) — conversational `AIAgent` over standard AG-UI events, no issue awareness

Additionally, `qyl.loom` is a 57-file dead dependency — all its services are duplicated in `qyl.collector` and never registered in DI. Two true salvage candidates (`StatisticalMath`, `DistributionComparer`) plus four type-only files have no collector counterpart.

## Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Unified agent, dual trigger | Same `AIAgent` handles background autofix and user conversations — one set of tools, one instruction set, one session model |
| 2 | Tool calls as stage signals | Agent calls `root_cause(...)` / `solution(...)` / `code_it_up(...)` — AG-UI `tool_call_start`/`tool_call_end` events drive dashboard stage transitions |
| 3 | Salvage the best, delete the rest | Move 6 files from `qyl.loom` to `qyl.collector`, delete the project |
| 4 | 5-stage flow | Insight > Explore > Reason > Root Cause > Solution (matching the React prototype) |
| 5 | Hero moment | Background auto-investigates, dashboard shows badge, user clicks "Attach & Continue Chat" |
| 6 | Mid-stream intervention | POST interrupt + CancellationToken + conversation history append |
| 7 | "Code It Up" as agent tool | `code_it_up()` tool triggers `AutofixOrchestrator` > `PrCreationService` pipeline |

## Dependency boundary

This document owns Loom's runtime shape:

- Session lifecycle and DuckDB persistence
- AG-UI endpoints and SSE protocol
- Background handoff and attach flow
- Agent/tool orchestration
- Deletion of the old Copilot-specific Loom path

The March 11 AIContextProvider design still owns the shared context-plumbing details:

- `IIssueContextSource`
- `IssueContextBuilder`
- `ObservabilityContextProvider`
- `QylAgentBuilder` provider wiring

When the two documents overlap, use this one for Loom behavior and the March 11 document for shared context-construction mechanics.

## Architecture (Post-Migration Target State)

This section describes the target architecture after this spec is implemented. For the current state, see the [Problem](#problem) section and `docs/loom-design.md`.

```text
src/
├── qyl.contracts/
│   ├── Autofix/
│   │   ├── LoomStage.cs              # NEW — stage enum + extensions
│   │   └── LoomSessionTypes.cs       # NEW — session, message, tool result types
│   └── Copilot/
│       └── IIssueContextSource.cs    # NEW — context interface (BCL-only)
├── qyl.agents/
│   ├── Agents/
│   │   ├── QylAgentBuilder.cs        # MODIFIED — ChatClientAgentOptions + providers
│   │   ├── LoomAgent.cs              # NEW — agent factory + instructions
│   │   └── LoomTools.cs             # NEW — root_cause, solution, code_it_up
│   ├── Context/
│   │   └── ObservabilityContextProvider.cs  # NEW
│   └── Adapters/
│       └── InstrumentedChatClient.cs # KEPT — OTel wrapping
├── qyl.collector/
│   ├── Autofix/
│   │   ├── LoomEndpoints.cs          # MODIFIED — deprecated, delegates to LoomAguiEndpoints
│   │   ├── LoomExplorerService.cs    # MODIFIED — uses IssueContextBuilder
│   │   ├── LoomInsightService.cs     # MODIFIED — uses IssueContextBuilder
│   │   ├── LoomSessionStore.cs       # NEW — DuckDB session CRUD
│   │   ├── IssueContextBuilder.cs    # NEW — shared context builder
│   │   ├── AutofixOrchestrator.cs    # KEPT
│   │   ├── AutofixAgentService.cs    # KEPT
│   │   ├── TriagePipelineService.cs  # MODIFIED — headless LoomAgent execution
│   │   ├── AutofixConstants.cs       # SALVAGED from qyl.loom
│   │   └── AutofixArtifacts.cs       # SALVAGED from qyl.loom
│   ├── Analytics/
│   │   ├── StatisticalMath.cs        # SALVAGED from qyl.loom
│   │   ├── DistributionComparer.cs   # SALVAGED from qyl.loom
│   │   ├── AnomalyTypes.cs           # SALVAGED from qyl.loom
│   │   └── AnomalyService.cs         # KEPT
│   ├── Copilot/
│   │   └── LoomAguiEndpoints.cs      # NEW — replaces CopilotAguiEndpoints
│   └── Storage/Migrations/
│       └── loom_sessions.sql         # NEW — DDL
└── qyl.dashboard/                    # KEPT — frontend (separate spec)
```

**Deleted:** `src/qyl.loom/` (entire project), `QylCopilotAdapter.cs`, `CopilotSessionStore.cs`, `CopilotAuthProvider.cs`, `GitHubPkceFlow.cs`, `CopilotAguiEndpoints.cs`, `CopilotEndpoints.cs`.

### Dependency chain (post-migration)

```text
core/specs/*.tsp → qyl.contracts → qyl.collector → qyl.dashboard
                                  → qyl.mcp
                                  → qyl.agents → qyl.collector (LoomAgent created in Program.cs)
                                  → qyl.instrumentation
eng/build/ → orchestrates everything above
```

No `qyl.loom` in the chain — it is deleted.

### Component diagram

```text
                    ┌─────────────────────┐
                    │   qyl.dashboard     │
                    │   (React 19)        │
                    └──────┬──────────────┘
                           │ AG-UI SSE (CopilotKit)
                           │ POST /api/v1/loom/{issueId}/chat
                           │ POST /api/v1/loom/{sessionId}/interrupt
                           │ GET  /api/v1/loom/pending-handoffs
                           │ POST /api/v1/loom/{sessionId}/attach
                           v
                    ┌─────────────────────┐
                    │ LoomAguiEndpoints   │
                    └──────┬──────────────┘
                           │ creates/resumes LoomSession
                           v
         ┌────────────────────────────────────────┐
         │            LoomAgent (AIAgent)         │
         │  ┌──────────────────────────────────┐  │
         │  │ QylAgentBuilder.FromChatClient    │  │
         │  │ + InMemoryChatHistoryProvider     │  │
         │  │ + ObservabilityContextProvider    │  │
         │  │ + LoomTools (root_cause,          │  │
         │  │   solution, code_it_up)           │  │
         │  │ + ObservabilityTools (78 MCP)     │  │
         │  └──────────────────────────────────┘  │
         └────────────┬───────────────────────────┘
                      │
         ┌────────────┼────────────────────────┐
         │            │                        │
         v            v                        v
   ┌──────────┐ ┌──────────────┐ ┌───────────────────────┐
   │ DuckDB   │ │IssueContext  │ │ AutofixOrchestrator   │
   │ Sessions │ │Builder       │ │ + PrCreationService   │
   └──────────┘ └──────────────┘ └───────────────────────┘

   Background trigger:
   ┌──────────────────────┐    headless session
   │TriagePipelineService │───────────────────> LoomAgent
   │  (BackgroundService) │                     (no SSE, results to DuckDB)
   └──────────────────────┘
```

### What changes from today

| Component | Before | After |
|-----------|--------|-------|
| LLM calls | Raw `IChatClient` per-step | `AIAgent` with tool-calling loop |
| Session state | None (one-shot stream) | DuckDB `loom_sessions` table |
| Stage transitions | Custom `StreamUpdateKind` enum | AG-UI `tool_call_start`/`tool_call_end` events |
| Conversation | Not possible | Multi-turn via `InMemoryChatHistoryProvider` |
| Background results | `FixRunRecord` in DuckDB, no UI | `LoomSession` with attach endpoint |
| Context building | Duplicated `BuildContextBlock` in 2 services | `IssueContextBuilder` (shared) + `ObservabilityContextProvider` (AIAgent) |

## State Machine

The state machine uses two dimensions: **stage** (where in the pipeline) and **status** (what the session is doing at that stage). This separation means a session "at stage RootCause" can be distinguished as "actively running," "waiting for user input," or "crashed."

### Two-dimensional state

```text
Stage (where)               Status (what)
─────────────               ─────────────
Idle (0)                    Active    — agent is running
Insight (1)                 Paused    — agent stopped, waiting for user
Exploring (2)               Idle      — session exists, agent not running
Reasoning (3)               Completed — terminal: success
RootCause (4)               Failed    — terminal: error
Solution (5)                Cancelled — terminal: user/system cancel
CodeItUp (6)
```

### State diagram

```
                    status=Active
                         │
                    ┌────v────┐
                    │ Idle(0) │
                    └────┬────┘
                         │ session created
                         v
                    ┌──────────┐
                    │Insight(1)│ heuristic + optional LLM
                    └────┬─────┘
                         │
                         v
                    ┌───────────┐
                    │Exploring  │ agent monologue (text_delta)
                    │    (2)    │
                    └────┬──────┘
                         │
                         v
                    ┌───────────┐
                    │Reasoning  │ internal deliberation
                    │    (3)    │
                    └────┬──────┘
                         │
             ┌───────────┼───────────────────┐
             │           │                   │
             v           │                   v
    ┌──────────────┐     │         ┌──────────────────┐
    │status=Paused │     │         │  RootCause (4)   │
    │(stage stays  │     │         │  tool_call:      │
    │ at 2 or 3)   │     │         │  root_cause()    │
    └──────┬───────┘     │         └──────┬───────────┘
           │ user msg    │                │
           v             │                v
    status → Active      │         ┌─────────────┐
    stage → Exploring    │         │ Solution(5) │
                         │         │ tool_call:   │
                         │         │ solution()   │
                         │         └──────┬───────┘
                         │                │
                         │     ┌──────────┼──────────┐
                         │     │          │          │
                         │     v          v          v
                         │  ┌────────┐ ┌──────┐ ┌──────────┐
                         │  │CodeIt  │ │ Done │ │ Continue │
                         │  │Up (6)  │ │      │ │ chat ... │
                         │  │tool:   │ │      │ └──────────┘
                         │  │code_it │ │      │
                         │  │_up()   │ │      │
                         │  └───┬────┘ └──┬───┘
                         │      │         │
                         │      v         v
                         │  status=Completed
                         │
                         └──> status=Failed (on error at any stage)
                              status=Cancelled (on cancel at any stage)
```

### Stage enum

```csharp
// qyl.contracts/Autofix/LoomStage.cs

/// <summary>Where in the investigation pipeline the session is.</summary>
public enum LoomStage
{
    Idle = 0,
    Insight = 1,
    Exploring = 2,
    Reasoning = 3,
    RootCause = 4,
    Solution = 5,
    CodeItUp = 6
}

/// <summary>What the session is doing at its current stage.</summary>
public enum LoomStatus
{
    /// <summary>Agent is actively running at this stage.</summary>
    Active,
    /// <summary>Agent paused — waiting for user input to continue.</summary>
    Paused,
    /// <summary>Session exists but no agent is running (pre-start or between runs).</summary>
    Idle,
    /// <summary>Terminal: investigation completed successfully.</summary>
    Completed,
    /// <summary>Terminal: unrecoverable error.</summary>
    Failed,
    /// <summary>Terminal: user or system cancelled.</summary>
    Cancelled
}

public static class LoomStageExtensions
{
    public static bool IsTerminal(this LoomStatus status) =>
        status is LoomStatus.Completed or LoomStatus.Failed or LoomStatus.Cancelled;
}
```

### Why two dimensions?

| Scenario | Stage alone (broken) | Stage + Status (correct) |
|----------|---------------------|--------------------------|
| Agent running, analyzing root cause | `RootCause (4)` | stage=`RootCause`, status=`Active` |
| Agent paused at reasoning, needs user | `WaitingForUser (10)` — lost which stage | stage=`Reasoning`, status=`Paused` |
| Agent crashed mid-exploration | `Exploring (2)` — indistinguishable from running | stage=`Exploring`, status=`Failed` |
| Background session done, ready for handoff | `Completed (100)` — but what stage did it reach? | stage=`Solution`, status=`Completed` |
| User cancelled during root cause analysis | `Cancelled (102)` — lost progress info | stage=`RootCause`, status=`Cancelled` |

### Stage transitions

| From Stage | To Stage | Trigger | Status |
|------------|----------|---------|--------|
| Idle | Insight | Session created | Active |
| Insight | Exploring | Insight generated, agent starts monologue | Active |
| Exploring | Reasoning | Agent stops streaming text, starts deliberation | Active |
| Reasoning | RootCause | Agent calls `root_cause()` tool | Active |
| *(any)* | *(same)* | Agent needs user input | Paused |
| *(paused)* | Exploring | User sends message (interrupt or reply) | Active |
| RootCause | Solution | Agent calls `solution()` tool | Active |
| Solution | CodeItUp | Agent calls `code_it_up()` tool | Active |
| *(any)* | *(same)* | Agent finishes or run completes | Completed |
| *(any)* | *(same)* | Unrecoverable error | Failed |
| *(any)* | *(same)* | User or system cancels | Cancelled |

Note: terminal statuses (Completed, Failed, Cancelled) freeze the stage at its last value, preserving how far the investigation got.

## SSE Protocol — Tool Calls as Stage Signals

The dashboard renders stage transitions by observing standard AG-UI events. No custom event types.

### AG-UI event stream

```
event: RUN_STARTED
data: {"runId": "..."}

event: TEXT_MESSAGE_START
data: {"messageId": "..."}

-- Agent exploring (Stage 2) --
event: TEXT_MESSAGE_CONTENT
data: {"delta": "Looking at the stack trace, I can see..."}
...

-- Agent calls root_cause tool (Stage 3→4) --
event: TOOL_CALL_START
data: {"toolCallId": "tc_1", "toolName": "root_cause", "args": {}}

event: TOOL_CALL_ARGS
data: {"toolCallId": "tc_1", "delta": "{\"summary\":\"Race condition in..."}

event: TOOL_CALL_END
data: {"toolCallId": "tc_1"}

-- Agent calls solution tool (Stage 4→5) --
event: TOOL_CALL_START
data: {"toolCallId": "tc_2", "toolName": "solution", "args": {}}

event: TOOL_CALL_ARGS
data: {"toolCallId": "tc_2", "delta": "{\"summary\":\"Add lock..."}

event: TOOL_CALL_END
data: {"toolCallId": "tc_2"}

event: TEXT_MESSAGE_END
data: {"messageId": "..."}

event: RUN_FINISHED
data: {"runId": "..."}
```

### Dashboard stage mapping

```typescript
// React: useCopilotChat hook + custom stage reducer
function loomStageReducer(event: AGUIEvent): LoomStage {
  switch (event.type) {
    case 'TEXT_MESSAGE_CONTENT':
      return LoomStage.Exploring;
    case 'TOOL_CALL_START':
      switch (event.toolName) {
        case 'root_cause': return LoomStage.RootCause;
        case 'solution':   return LoomStage.Solution;
        case 'code_it_up': return LoomStage.CodeItUp;
      }
    case 'RUN_FINISHED':
      return LoomStage.Completed;
  }
}
```

### Tool argument schemas

The agent's tool calls carry structured data as JSON arguments. The dashboard parses `TOOL_CALL_ARGS` deltas to render structured UI:

```typescript
// root_cause tool args
interface LoomRootCause {
  summary: string;
  steps: { order: number; description: string; is_root_cause: boolean }[];
}

// solution tool args
interface LoomSolution {
  summary: string;
  steps: { title: string; description: string }[];
}

// code_it_up tool args → response
interface LoomCodeItUp {
  run_id: string;
  pr_url?: string;
  confidence: number;
}
```

## LoomAgent Definition

### Factory

```csharp
// qyl.agents/Agents/LoomAgent.cs
public static class LoomAgent
{
    public static AIAgent Create(
        IChatClient chatClient,
        IReadOnlyList<AITool> observabilityTools,
        IReadOnlyList<AIContextProvider> contextProviders,
        TimeProvider? timeProvider = null)
    {
        List<AITool> tools =
        [
            .. LoomTools.All,
            .. observabilityTools
        ];

        return QylAgentBuilder.FromChatClient(
            chatClient,
            agentName: "loom",
            description: "AI debugging assistant that investigates errors and proposes fixes",
            instructions: Instructions,
            tools: tools,
            contextProviders: contextProviders,
            timeProvider: timeProvider);
    }

    private const string Instructions = """
        You are Loom, an AI debugging assistant embedded in the qyl observability platform.
        You investigate production errors by analyzing telemetry data (traces, logs, metrics).

        ## Investigation Flow

        1. **Explore**: Read the error context injected into this conversation.
           Query telemetry using your observability tools to understand the full picture.
           Stream your analysis as you go — the user sees your reasoning in real-time.

        2. **Root Cause**: When you've identified the root cause, call the `root_cause` tool
           with a structured causal chain. Each step should be a link in the chain from
           the triggering event to the fundamental cause. Mark exactly one step as `is_root_cause`.

        3. **Solution**: After root cause, call the `solution` tool with implementation steps.
           Each step should be concrete and actionable. The user can approve, modify, or
           reject individual steps.

        4. **Code It Up**: If the user approves, call `code_it_up` to generate a fix.
           This creates a PR with the changes. Only do this when explicitly asked.

        ## Interaction Guidelines

        - Stream your thinking — don't silently process. The user watches your investigation live.
        - Ask clarifying questions if the error context is ambiguous.
        - Use observability tools to query spans, logs, and metrics. Don't guess — verify.
        - If you need more information from the user, explain what you need and why.
        - After delivering root cause + solution, remain available for follow-up questions.
        """;
}
```

### Tools

**Session ID resolution:** Tools that need the current session ID get it from `AgentSession.StateBag` via a `LoomSessionIdKey` constant, mirroring how `ObservabilityContextProvider` uses `IssueIdKey`. The session ID is set by `LoomAguiEndpoints` when creating/resuming a session (see [Endpoint wiring](#session-id-wiring) below).

```csharp
// qyl.agents/Agents/LoomTools.cs
public static class LoomTools
{
    /// <summary>Key in AgentSession.StateBag for the current Loom session ID.</summary>
    public const string SessionIdKey = "qyl.loomSessionId";

    public static IReadOnlyList<AITool> All { get; } =
    [
        AIFunctionFactory.Create(RootCause),
        AIFunctionFactory.Create(Solution),
        AIFunctionFactory.Create(CodeItUp)
    ];

    [Description("Report the root cause analysis as a structured causal chain. " +
        "Call this once you've identified the root cause.")]
    private static LoomRootCauseResult RootCause(
        [Description("One-sentence summary of the root cause")]
        string summary,
        [Description("Ordered causal chain from trigger to root cause")]
        LoomCausalStep[] steps)
    {
        // The tool "executes" by returning the structured data.
        // The AG-UI event stream carries the arguments to the dashboard.
        // The session store persists the result.
        return new LoomRootCauseResult(summary, steps);
    }

    [Description("Report the proposed solution as implementation steps. " +
        "Call this after root cause analysis.")]
    private static LoomSolutionResult Solution(
        [Description("One-sentence summary of the fix")]
        string summary,
        [Description("Ordered implementation steps")]
        LoomSolutionStep[] steps)
    {
        return new LoomSolutionResult(summary, steps);
    }

    [Description("Generate a code fix and open a pull request. " +
        "Only call when the user explicitly asks to code it up.")]
    private static async Task<LoomCodeItUpResult> CodeItUp(
        [Description("Repository full name (owner/repo)")]
        string repo,
        [Description("Base branch for the PR (default: main)")]
        string? baseBranch,
        // Injected by AIFunction infrastructure:
        AgentSession agentSession,
        AutofixOrchestrator orchestrator,
        PrCreationService prService,
        LoomSessionStore sessionStore,
        CancellationToken ct)
    {
        // Resolve session ID from AgentSession.StateBag (set by LoomAguiEndpoints)
        string? sessionId = agentSession.StateBag.GetValue<string>(SessionIdKey);
        if (sessionId is null)
            return new LoomCodeItUpResult(false, null, null, 0, "No session ID in StateBag");

        LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
        if (session is null)
            return new LoomCodeItUpResult(false, null, null, 0, "No active session");

        // Create fix run from session's root cause analysis
        FixRunRecord run = await orchestrator.CreateFixRunAsync(
            session.IssueId, session.Issue, FixPolicy.RequireReview, ct);

        string? prUrl = null;
        if (!string.IsNullOrEmpty(repo))
            prUrl = (await prService.CreatePrAsync(run.Id, repo, baseBranch ?? "main", ct)).PrUrl;

        return new LoomCodeItUpResult(true, run.Id, prUrl, run.Confidence, null);
    }
}

// Tool return types (in qyl.contracts/Autofix/LoomSessionTypes.cs)
public sealed record LoomRootCauseResult(string Summary, LoomCausalStep[] Steps);
public sealed record LoomSolutionResult(string Summary, LoomSolutionStep[] Steps);
public sealed record LoomCodeItUpResult(bool Success, string? RunId, string? PrUrl, double Confidence, string? Error);
```

## Mid-Stream Intervention

When the user sends a message while the agent is running (exploring, reasoning), the system interrupts the current run and appends the user's message to the conversation history.

### Endpoint

```
POST /api/v1/loom/{sessionId}/interrupt
Content-Type: application/json

{ "message": "Actually, check the database connection pool — we saw similar issues last week" }
```

### Flow

```text
1. Client POST /interrupt with message
2. Server:
   a. Validate session exists and is non-terminal
   b. Cancel the current CancellationTokenSource
   c. Append user message to session history
   d. Set stage → Exploring
   e. Return 200 OK
3. Agent's current RunStreamingAsync throws OperationCanceledException
4. LoomAguiEndpoints catches, starts new RunStreamingAsync on same session
5. InMemoryChatHistoryProvider preserves prior conversation
6. ObservabilityContextProvider re-injects issue context
7. Agent continues with full history + new user message
8. SSE stream continues with new events (RUN_STARTED for the new run)
```

### Implementation sketch

```csharp
// LoomAguiEndpoints.cs — interrupt handler
app.MapPost("/api/v1/loom/{sessionId}/interrupt", async (
    string sessionId,
    InterruptRequest request,
    LoomSessionStore sessionStore,
    CancellationToken ct) =>
{
    LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
    if (session is null) return Results.NotFound();
    if (session.Status.IsTerminal()) return Results.Conflict("Session is terminal");

    // Cancel the running agent
    session.CancellationTokenSource?.Cancel();

    // Append user message to history
    session.Messages.Add(new LoomMessage(LoomMessageRole.User, request.Message));
    session.Stage = LoomStage.Exploring;
    session.Status = LoomStatus.Active;
    session.PauseReason = null;
    await sessionStore.UpdateAsync(session, ct);

    return Results.Ok();
});
```

### Cancellation wiring

Each active session holds a `CancellationTokenSource`. The AG-UI streaming loop links it:

```csharp
using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(requestCt);
session.CancellationTokenSource = sessionCts;

try
{
    await foreach (var evt in agent.RunStreamingAsync(messages, session: agentSession, cancellationToken: sessionCts.Token))
    {
        await WriteAguiEvent(response, evt, ct);
    }
}
catch (OperationCanceledException) when (sessionCts.IsCancellationRequested)
{
    // Interrupt — the SSE connection stays open
    // Client reconnects or a new run starts on the same session
}
```

## Background > Conversation Handoff (Hero Moment)

### Flow

```text
1. TriagePipelineService scores issue >= threshold
2. Creates headless LoomSession (mode=Background, status=Active)
3. LoomAgent runs autonomously:
   a. ObservabilityContextProvider injects issue data
   b. Agent explores telemetry, calls root_cause(), calls solution()
   c. Results persisted to DuckDB via LoomSessionStore
   d. On completion: status=Completed, stage=last reached
4. Dashboard polls GET /api/v1/loom/pending-handoffs
   → Returns sessions with mode=Background, status=Completed, stage >= RootCause
5. Dashboard shows "Loom auto-investigated this issue" badge
6. User clicks "Attach & Continue Chat"
   → POST /api/v1/loom/{sessionId}/attach
7. Server:
   a. Validates session is background + status=Completed
   b. Sets mode=Interactive, status=Idle
   c. Returns LoomSession with root_cause_json, solution_json, message history
8. Dashboard opens SSE connection to POST /api/v1/loom/{sessionId}/chat
   → Receives REPLAY events (prior results), then LIVE events (new run)
```

### Replay Protocol (Handoff Hydration)

When a user attaches to a background session that already has results, the SSE stream must catch them up before entering live mode. This is the hardest part of the handoff.

**Design decisions:**

1. **Replay is instant, not real-time.** The dashboard receives all prior events in a burst. No artificial delays.

2. **Replay vs live is distinguished by a `replay` field.** Each replayed event carries `"replay": true`. The dashboard uses this to render prior results immediately (no animations) and switch to live rendering when `replay` disappears.

3. **Mid-investigation attach joins the live stream.** If the background agent is still running (`status=Active`) when the user attaches, the SSE connection joins the in-progress stream. No cancel/restart. The user sees live events from wherever the agent currently is. Prior events are replayed first.

**SSE stream for a completed background session (most common case):**

```
-- Phase 1: Replay (instant, all at once)
event: RUN_STARTED
data: {"runId": "bg-run-1", "replay": true}

event: TEXT_MESSAGE_START
data: {"messageId": "msg-1", "replay": true}

event: TEXT_MESSAGE_CONTENT
data: {"delta": "[full monologue text from background run]", "replay": true}

event: TOOL_CALL_START
data: {"toolCallId": "tc_1", "toolName": "root_cause", "replay": true}

event: TOOL_CALL_ARGS
data: {"toolCallId": "tc_1", "delta": "[full root cause JSON]", "replay": true}

event: TOOL_CALL_END
data: {"toolCallId": "tc_1", "replay": true}

event: TOOL_CALL_START
data: {"toolCallId": "tc_2", "toolName": "solution", "replay": true}

event: TOOL_CALL_ARGS
data: {"toolCallId": "tc_2", "delta": "[full solution JSON]", "replay": true}

event: TOOL_CALL_END
data: {"toolCallId": "tc_2", "replay": true}

event: RUN_FINISHED
data: {"runId": "bg-run-1", "replay": true}

-- Phase 2: Live (agent starts new run, waiting for user input)
event: RUN_STARTED
data: {"runId": "live-run-1"}

event: TEXT_MESSAGE_START
data: {"messageId": "msg-2"}

event: TEXT_MESSAGE_CONTENT
data: {"delta": "I investigated this error automatically. Here's what I found..."}
...
```

**SSE stream for an in-progress background session:**

```
-- Phase 1: Replay (events the agent already produced)
event: RUN_STARTED
data: {"runId": "bg-run-1", "replay": true}

event: TEXT_MESSAGE_CONTENT
data: {"delta": "[monologue so far]", "replay": true}

-- Phase 2: Live (joining the running stream mid-flight)
event: TEXT_MESSAGE_CONTENT
data: {"delta": "...and looking at the database query patterns..."}

event: TOOL_CALL_START
data: {"toolCallId": "tc_1", "toolName": "root_cause"}
...
```

**Dashboard rendering logic:**

```typescript
function handleEvent(event: AGUIEvent) {
  if (event.replay) {
    // Instant render — no animations, no typing effect
    appendToUI(event, { animated: false });
  } else {
    // Live render — streaming animations, typing indicator
    appendToUI(event, { animated: true });
  }
}
```

**Implementation in LoomAguiEndpoints:**

```csharp
// POST /api/v1/loom/{sessionId}/chat — handles both new and attach scenarios
async Task HandleChat(HttpResponse response, LoomSession session, AIAgent agent, ...)
{
    // --- Session ID wiring (anchored from LoomTools section) ---
    // Set both keys in StateBag so tools and context providers can resolve them.
    var agentSession = await agent.CreateSessionAsync(ct);
    agentSession.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, session.IssueId);
    agentSession.StateBag.SetValue(LoomTools.SessionIdKey, session.SessionId);

    // Phase 1: Replay prior messages (if any)
    IReadOnlyList<LoomMessage> history = await sessionStore.GetMessagesAsync(session.SessionId, ct);
    if (history.Count > 0)
    {
        await ReplayHistoryAsAguiEvents(response, history, session, ct);
    }

    // Phase 2: Start new live run (agent picks up from conversation history)
    session.Status = LoomStatus.Active;
    await sessionStore.UpdateAsync(session, ct);

    await foreach (var evt in agent.RunStreamingAsync(messages, session: agentSession, cancellationToken: ct))
    {
        await WriteAguiEvent(response, evt, ct);
    }
}

private static async Task ReplayHistoryAsAguiEvents(
    HttpResponse response,
    IReadOnlyList<LoomMessage> history,
    LoomSession session,
    CancellationToken ct)
{
    // Synthesize AG-UI events from stored messages, each with replay=true
    // Tool messages → TOOL_CALL_START/ARGS/END
    // Assistant messages → TEXT_MESSAGE_START/CONTENT/END
    // Flush all at once — no delays
}
```

### Endpoints

```
GET /api/v1/loom/pending-handoffs
→ 200 OK: LoomSessionSummary[] (sessions with mode=Background, status=Completed, stage >= RootCause)

POST /api/v1/loom/{sessionId}/attach
→ 200 OK: LoomSession (full session with root_cause_json, solution_json, message history)
→ 404: Session not found
→ 409: Session already interactive or terminal
```

### Background execution

```csharp
// TriagePipelineService modification
private async Task ExecuteBackgroundLoomAsync(string issueId, CancellationToken ct)
{
    // Create headless session (status=Active from creation)
    LoomSession session = await _sessionStore.CreateAsync(
        issueId, LoomSessionMode.Background, ct);

    // Build agent with issue + session context
    var agentSession = await _agent.CreateSessionAsync(ct);
    agentSession.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, issueId);
    agentSession.StateBag.SetValue(LoomTools.SessionIdKey, session.SessionId);

    // Per-session timeout (5 min default) to prevent background hangs
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

    try
    {
        // Run to completion — no SSE, results go to DuckDB
        await foreach (var evt in _agent.RunStreamingAsync(
            "Investigate this error. Identify the root cause and propose a solution.",
            session: agentSession,
            cancellationToken: timeoutCts.Token))
        {
            // Process tool calls to extract root cause / solution and update stage
            if (evt is ToolCallEvent { ToolName: "root_cause" } rc)
            {
                session.RootCause = ParseRootCause(rc);
                session.Stage = LoomStage.RootCause;
            }
            else if (evt is ToolCallEvent { ToolName: "solution" } sol)
            {
                session.Solution = ParseSolution(sol);
                session.Stage = LoomStage.Solution;
            }

            // Persist stage progress so pending-handoffs query can find partial results
            await _sessionStore.UpdateAsync(session, ct);
        }

        session.Status = LoomStatus.Completed;
    }
    catch (Exception ex)
    {
        session.Status = LoomStatus.Failed;
        session.Error = ex.Message;
    }

    await _sessionStore.UpdateAsync(session, ct);
}
```

## DuckDB Schema

### loom_sessions

```sql
CREATE TABLE IF NOT EXISTS loom_sessions (
    session_id       VARCHAR PRIMARY KEY,
    issue_id         VARCHAR NOT NULL,
    mode             VARCHAR NOT NULL DEFAULT 'interactive',  -- 'interactive' | 'background'
    stage            INTEGER NOT NULL DEFAULT 0,              -- LoomStage enum value (where)
    stage_name       VARCHAR NOT NULL DEFAULT 'idle',         -- human-readable stage
    status           VARCHAR NOT NULL DEFAULT 'idle',         -- LoomStatus: 'active' | 'paused' | 'idle' | 'completed' | 'failed' | 'cancelled'
    created_at       BIGINT  NOT NULL,                        -- unix nanos
    updated_at       BIGINT  NOT NULL,                        -- unix nanos
    root_cause_json  VARCHAR,                                 -- LoomRootCauseResult serialized
    solution_json    VARCHAR,                                 -- LoomSolutionResult serialized
    fix_run_id       VARCHAR,                                 -- links to fix_runs table
    pause_reason     VARCHAR,                                 -- why paused: 'waiting_for_user' | 'need_more_info' | null
    error            VARCHAR,
    metadata_json    VARCHAR                                  -- extensible bag
);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_issue
    ON loom_sessions (issue_id);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_mode_status
    ON loom_sessions (mode, status);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_status_stage
    ON loom_sessions (status, stage);
```

**Column semantics:**

| Column | Answers | Example queries |
|--------|---------|-----------------|
| `stage` | "How far did the investigation get?" | `WHERE stage >= 4` (has root cause) |
| `status` | "What is the session doing right now?" | `WHERE status = 'active'` (agent running) |
| `mode` | "Was this user-initiated or automatic?" | `WHERE mode = 'background'` |
| `pause_reason` | "Why is the agent paused?" | `WHERE status = 'paused' AND pause_reason = 'waiting_for_user'` |

**Key queries:**

```sql
-- Background sessions ready for handoff (hero moment badge)
SELECT * FROM loom_sessions
WHERE mode = 'background' AND status = 'completed' AND stage >= 4;

-- Abandoned sessions (agent crashed — status never reached terminal)
SELECT * FROM loom_sessions
WHERE status = 'active' AND updated_at < (now_nanos - 300_000_000_000);

-- Sessions waiting for user input
SELECT * FROM loom_sessions
WHERE status = 'paused';
```

### loom_messages

```sql
CREATE TABLE IF NOT EXISTS loom_messages (
    message_id   VARCHAR PRIMARY KEY,
    session_id   VARCHAR NOT NULL,
    role         VARCHAR NOT NULL,       -- 'user' | 'assistant' | 'system' | 'tool'
    content      VARCHAR NOT NULL,
    tool_name    VARCHAR,                -- for tool_call/tool_result messages
    tool_args    VARCHAR,                -- JSON args for tool calls
    created_at   BIGINT  NOT NULL,       -- unix nanos
    sequence     INTEGER NOT NULL        -- ordering within session
);

CREATE INDEX IF NOT EXISTS idx_loom_messages_session
    ON loom_messages (session_id, sequence);
```

### LoomSessionStore

```csharp
// qyl.collector/Autofix/LoomSessionStore.cs
public sealed class LoomSessionStore(DuckDbStore store, TimeProvider timeProvider)
{
    public async Task<LoomSession> CreateAsync(
        string issueId,
        LoomSessionMode mode = LoomSessionMode.Interactive,
        CancellationToken ct = default);

    public async Task<LoomSession?> GetAsync(string sessionId, CancellationToken ct = default);

    public async Task UpdateAsync(LoomSession session, CancellationToken ct = default);

    public async Task<IReadOnlyList<LoomSession>> GetByIssueAsync(
        string issueId, CancellationToken ct = default);

    public async Task<IReadOnlyList<LoomSessionSummary>> GetPendingHandoffsAsync(
        CancellationToken ct = default);

    public async Task AppendMessageAsync(
        string sessionId, LoomMessage message, CancellationToken ct = default);

    public async Task<IReadOnlyList<LoomMessage>> GetMessagesAsync(
        string sessionId, CancellationToken ct = default);
}
```

## Contract Types

```csharp
// qyl.contracts/Autofix/LoomStage.cs — enums shown in State Machine section above

// qyl.contracts/Autofix/LoomSessionTypes.cs
public enum LoomSessionMode { Interactive, Background }
public enum LoomMessageRole { User, Assistant, System, Tool }
public enum LoomPauseReason { WaitingForUser, NeedMoreInfo }

public sealed record LoomSession
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public required LoomSessionMode Mode { get; set; }
    public required LoomStage Stage { get; set; }
    public required LoomStatus Status { get; set; }
    public LoomPauseReason? PauseReason { get; set; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; set; }
    public LoomRootCauseResult? RootCause { get; set; }
    public LoomSolutionResult? Solution { get; set; }
    public string? FixRunId { get; set; }
    public string? Error { get; set; }

    // In-memory only (not persisted to DuckDB)
    [JsonIgnore] public List<LoomMessage> Messages { get; } = [];
    [JsonIgnore] public CancellationTokenSource? CancellationTokenSource { get; set; }
}

public sealed record LoomSessionSummary(
    string SessionId,
    string IssueId,
    LoomStage Stage,
    LoomStatus Status,
    LoomSessionMode Mode,
    long CreatedAt,
    bool HasRootCause,
    bool HasSolution);

public sealed record LoomMessage(
    LoomMessageRole Role,
    string Content,
    string? ToolName = null,
    string? ToolArgs = null);

// Wire format types — these define the SSE contract.
// JSON serialization uses snake_case: order, description, is_root_cause.
// This supersedes the id/text naming in docs/loom-design.md Section 8.
public sealed record LoomCausalStep(int Order, string Description, bool IsRootCause);
public sealed record LoomSolutionStep(string Title, string Description);
```

## IssueContextBuilder

This section summarizes the imported dependency and adds the Loom-specific decision that Explorer's richer format becomes canonical. For the shared contract and provider rationale, see `2026-03-11-aicontextprovider-integration-design.md`.

Extracts duplicated `BuildContextBlock` from `LoomExplorerService` and `LoomInsightService` per the [AIContextProvider design](2026-03-11-aicontextprovider-integration-design.md).

### Current duplication

Two services build context blocks with different truncation strategies:

| Service | Label | Stack truncation | Env field | UserContext |
|---------|-------|------------------|-----------|-------------|
| `LoomExplorerService.BuildContextBlock()` | `"Error type:"` | 800 chars | Yes | Yes |
| `LoomInsightService.BuildContextBlock()` | `"Type:"` | 500 chars | No | No |

**Resolution:** Adopt the Explorer format (richer) as the canonical format. Stack truncation is parameterized via `maxStackLength` — defaults to 800 chars, but callers can pass a smaller value if they need shorter context (e.g., for triage scoring where token budget is tight).

### Interface

```csharp
// qyl.contracts/Copilot/IIssueContextSource.cs
public interface IIssueContextSource
{
    Task<string> GetFormattedContextAsync(
        string issueId, string? userContext = null, CancellationToken ct = default);
}
```

BCL-only — no package dependencies. Lives in `qyl.contracts` so both `qyl.agents` (for `ObservabilityContextProvider`) and `qyl.collector` (for `IssueContextBuilder`) can reference it without circular dependencies.

### Implementation

```csharp
// qyl.collector/Autofix/IssueContextBuilder.cs
public sealed class IssueContextBuilder(DuckDbStore store, IssueService issueService)
    : IIssueContextSource
{
    /// <summary>Default stack trace truncation length (chars).</summary>
    public const int DefaultMaxStackLength = 800;

    public async Task<IssueContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = 5,
        int maxStackLength = DefaultMaxStackLength,
        CancellationToken ct = default)
    {
        IssueSummary? issue = await store.GetIssueByIdAsync(issueId, ct);
        if (issue is null) return IssueContext.Empty;

        IReadOnlyList<ErrorIssueEventRow> events =
            await issueService.GetEventsAsync(issueId, maxEvents, ct);

        string block = FormatBlock(issue, events, userContext, maxStackLength);
        return new IssueContext(issue, events, userContext, block);
    }

    async Task<string> IIssueContextSource.GetFormattedContextAsync(
        string issueId, string? userContext, CancellationToken ct)
    {
        IssueContext ctx = await BuildAsync(issueId, userContext, ct: ct);
        return ctx.FormattedBlock;
    }

    internal static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext,
        int maxStackLength = DefaultMaxStackLength)
    {
        // Canonical format (adopted from LoomExplorerService):
        //
        // === Error Context ===
        // Error type: {issue.ErrorType}
        // Message: {issue.Message}
        // First seen: {issue.FirstSeen}
        // Event count: {issue.EventCount}
        // Env: {issue.Environment ?? "unknown"}
        //
        // === Recent Events ===
        // Event 1: {event.Timestamp}
        //   Stack: {Truncate(event.StackTrace, maxStackLength)}
        //   Breadcrumbs: {event.Breadcrumbs}
        // ...
        //
        // === User Context ===     (only if userContext is non-null)
        // {userContext}
        //
        // `internal static` for unit testing without DuckDB.
    }
}

public sealed record IssueContext(
    IssueSummary? Issue,
    IReadOnlyList<ErrorIssueEventRow> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static IssueContext Empty { get; } = new(null, [], null, string.Empty);
    public bool IsEmpty => Issue is null;
}
```

### DI wiring

```csharp
// Program.cs
builder.Services.AddSingleton<IssueContextBuilder>();
builder.Services.AddSingleton<IIssueContextSource>(sp =>
    sp.GetRequiredService<IssueContextBuilder>());
```

### Consumer migration

```csharp
// LoomExplorerService — before:
string context = BuildContextBlock(issue, events);
// LoomExplorerService — after:
IssueContext ctx = await contextBuilder.BuildAsync(issueId, userContext, ct: ct);
string context = ctx.FormattedBlock;

// LoomInsightService — before:
string context = BuildContextBlock(issue, events);
// LoomInsightService — after:
IssueContext ctx = await contextBuilder.BuildAsync(issueId, ct: ct);
string context = ctx.FormattedBlock;
// (Uses default 800-char truncation — a minor behavior change from 500,
//  but Insight's LLM prompt has plenty of token budget for the extra context)
```

## Endpoint Summary

### New endpoints (LoomAguiEndpoints)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/loom/{issueId}/chat` | Start or continue conversational Loom session (AG-UI SSE) |
| POST | `/api/v1/loom/{sessionId}/interrupt` | Interrupt running agent, append user message |
| GET | `/api/v1/loom/pending-handoffs` | List background sessions ready for user attachment |
| POST | `/api/v1/loom/{sessionId}/attach` | Convert background session to interactive |
| GET | `/api/v1/loom/{sessionId}` | Get session state (stage, root cause, solution) |
| GET | `/api/v1/loom/{sessionId}/messages` | Get full message history |

### Existing endpoints (LoomEndpoints — kept for backward compatibility)

| Method | Path | Status |
|--------|------|--------|
| GET | `/api/v1/loom/{issueId}/insight` | **Kept** — fast heuristic, no agent needed |
| POST | `/api/v1/loom/{issueId}/explore` | **Deprecated** — replaced by `/chat` |
| POST | `/api/v1/loom/{issueId}/code-it-up` | **Deprecated** — now a tool call in the agent |

### Removed endpoints

| Method | Path | Reason |
|--------|------|--------|
| POST | `/api/v1/copilot/chat` | Replaced by `/api/v1/loom/{issueId}/chat` |

## Salvage Strategy

### True salvage (no collector counterpart)

| Source | Target | Change |
|--------|--------|--------|
| `qyl.loom/StatisticalMath.cs` | `qyl.collector/Analytics/StatisticalMath.cs` | Namespace: `Qyl.Loom` > `Qyl.Collector.Analytics` |
| `qyl.loom/DistributionComparer.cs` | `qyl.collector/Analytics/DistributionComparer.cs` | Namespace: `Qyl.Loom` > `Qyl.Collector.Analytics` |
| `qyl.loom/AnomalyTypes.cs` | `qyl.collector/Analytics/AnomalyTypes.cs` | Namespace: `Qyl.Loom` > `Qyl.Collector.Analytics` |
| `qyl.loom/AutofixConstants.cs` | `qyl.collector/Autofix/AutofixConstants.cs` | Namespace: `Qyl.Loom` > `Qyl.Collector.Autofix` |
| `qyl.loom/AutofixArtifacts.cs` | `qyl.collector/Autofix/AutofixArtifacts.cs` | Namespace: `Qyl.Loom` > `Qyl.Collector.Autofix` |
| `qyl.loom/LoomModels.cs` | `qyl.contracts/Autofix/LoomSessionTypes.cs` | Types merged into new contract file; `LoomJsonContext` moves to collector |

### Already migrated (delete — loom copy is dead code)

All 28 other service files in `qyl.loom` are byte-for-byte duplicates of their `qyl.collector` counterparts. They compile via ProjectReference but are never registered in DI. Safe to delete.

### Delete sequence

1. Move 6 salvage files (namespace-only changes)
2. Remove `qyl.loom` ProjectReference from `qyl.collector.csproj`
3. Remove `qyl.loom` from `qyl.slnx`
4. Delete `src/qyl.loom/` directory

## File Changes

### NEW (10 files)

| File | Purpose |
|------|---------|
| `qyl.contracts/Autofix/LoomStage.cs` | Stage enum + extensions |
| `qyl.contracts/Autofix/LoomSessionTypes.cs` | LoomSession, LoomMessage, LoomCausalStep, LoomSolutionStep, tool result types |
| `qyl.contracts/Copilot/IIssueContextSource.cs` | Context interface (BCL-only) |
| `qyl.agents/Agents/LoomAgent.cs` | Agent factory + system instructions |
| `qyl.agents/Agents/LoomTools.cs` | root_cause, solution, code_it_up tool definitions |
| `qyl.agents/Context/ObservabilityContextProvider.cs` | Auto-injects issue context into AIAgent sessions |
| `qyl.collector/Autofix/LoomSessionStore.cs` | DuckDB session CRUD |
| `qyl.collector/Autofix/IssueContextBuilder.cs` | Shared context builder (replaces duplicated BuildContextBlock) |
| `qyl.collector/Copilot/LoomAguiEndpoints.cs` | AG-UI SSE endpoints for Loom (chat, interrupt, handoff) |
| `qyl.collector/Storage/Migrations/loom_sessions.sql` | DDL for loom_sessions + loom_messages |

### SALVAGED (6 files — move + namespace change)

| Source | Target |
|--------|--------|
| `qyl.loom/StatisticalMath.cs` | `qyl.collector/Analytics/StatisticalMath.cs` |
| `qyl.loom/DistributionComparer.cs` | `qyl.collector/Analytics/DistributionComparer.cs` |
| `qyl.loom/AnomalyTypes.cs` | `qyl.collector/Analytics/AnomalyTypes.cs` |
| `qyl.loom/AutofixConstants.cs` | `qyl.collector/Autofix/AutofixConstants.cs` |
| `qyl.loom/AutofixArtifacts.cs` | `qyl.collector/Autofix/AutofixArtifacts.cs` |
| `qyl.loom/LoomModels.cs` | Types merged into `qyl.contracts/Autofix/LoomSessionTypes.cs` |

### MODIFIED (7 files)

| File | Change |
|------|--------|
| `qyl.collector/Autofix/LoomEndpoints.cs` | Deprecation markers, delegate new calls to LoomAguiEndpoints |
| `qyl.collector/Autofix/LoomExplorerService.cs` | Replace inline `BuildContextBlock` with `IssueContextBuilder` |
| `qyl.collector/Autofix/LoomInsightService.cs` | Replace inline `BuildContextBlock` with `IssueContextBuilder` |
| `qyl.collector/Autofix/TriagePipelineService.cs` | Add headless `LoomAgent` execution for background sessions |
| `qyl.collector/Program.cs` | DI: add LoomSessionStore, IssueContextBuilder, LoomAgent; remove Copilot adapter factory |
| `qyl.agents/Agents/QylAgentBuilder.cs` | `ChatClientAgentOptions` with `contextProviders` + `InMemoryChatHistoryProvider` |
| `qyl.agents/qyl.agents.csproj` | Remove `Microsoft.Agents.AI.GitHub.Copilot` package |

### DELETED

| File/Directory | Reason |
|----------------|--------|
| `qyl.agents/Adapters/QylCopilotAdapter.cs` | Replaced by LoomAgent; XML concat bug |
| `qyl.agents/Instrumentation/CopilotSessionStore.cs` | Replaced by InMemoryChatHistoryProvider |
| `qyl.agents/Auth/CopilotAuthProvider.cs` | No longer needed without Copilot SDK |
| `qyl.agents/Auth/GitHubPkceFlow.cs` | No longer needed without Copilot SDK |
| `qyl.collector/Copilot/CopilotAguiEndpoints.cs` | Replaced by LoomAguiEndpoints |
| `qyl.collector/Copilot/CopilotEndpoints.cs` | Copilot-specific endpoints removed |
| `src/qyl.loom/` | Entire project (57 files — 28 duplicates + 6 salvaged + 23 remaining dead code) |

## Build Order (Critical Path)

```text
Phase 1: Types (no runtime dependencies)
  1.1  qyl.contracts/Autofix/LoomStage.cs
  1.2  qyl.contracts/Autofix/LoomSessionTypes.cs
  1.3  qyl.contracts/Copilot/IIssueContextSource.cs

Phase 2: Storage + Context
  2.1  qyl.collector/Storage/Migrations/loom_sessions.sql
  2.2  qyl.collector/Autofix/LoomSessionStore.cs
  2.3  qyl.collector/Autofix/IssueContextBuilder.cs

Phase 3: Agent
  3.1  qyl.agents/Context/ObservabilityContextProvider.cs
  3.2  qyl.agents/Agents/QylAgentBuilder.cs (modify)
  3.3  qyl.agents/Agents/LoomAgent.cs
  3.4  qyl.agents/Agents/LoomTools.cs

Phase 4: Endpoints
  4.1  qyl.collector/Copilot/LoomAguiEndpoints.cs
  4.2  qyl.collector/Autofix/LoomEndpoints.cs (deprecation markers)
  4.3  qyl.collector/Autofix/LoomExplorerService.cs (delegate to IssueContextBuilder)
  4.4  qyl.collector/Autofix/LoomInsightService.cs (delegate to IssueContextBuilder)

Phase 5: Integration
  5.1  qyl.collector/Program.cs (DI wiring)
  5.2  qyl.collector/Autofix/TriagePipelineService.cs (background LoomAgent)

Phase 6: Salvage + Cleanup
  6.1  Move 6 salvage files (namespace change)
  6.2  Remove qyl.loom ProjectReference
  6.3  Delete Copilot adapter + SDK dependency
  6.4  Delete qyl.loom project
  6.5  Remove from qyl.slnx
```

### Parallelism opportunities

- Phase 1 tasks are independent of each other
- Phase 2.2 depends on 2.1; 2.3 depends on 1.3
- Phase 3.1 depends on 1.3; 3.2 is independent; 3.3 depends on 3.2 + 3.4
- Phase 6 tasks are sequential (delete order matters)

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| `ChatClientAgent` constructor instability (rc3) | Agent creation breaks on SDK update | Wrapped in `QylAgentBuilder` — single point of change |
| `AIFunctionFactory.Create` DI injection for `code_it_up` tool | Tool may not resolve `AutofixOrchestrator` from DI | Verify `AIFunctionFactory` supports DI-injected parameters; fallback: manual tool wrapping |
| `InMemoryChatHistoryProvider` memory growth | Long sessions accumulate unbounded history | Set max message count; persist to DuckDB `loom_messages` for replay |
| Background agent execution timeout | LLM calls hang indefinitely | Per-session timeout (default 5 min) via linked `CancellationTokenSource` |
| SSE reconnection after interrupt | Client may miss events between cancel and reconnect | Include `last_event_id` support in LoomAguiEndpoints |
| DuckDB write contention on sessions table | Multiple background agents writing simultaneously | Sessions are keyed by `session_id`; no cross-session contention |
| `qyl.loom` deletion breaks downstream | Unknown consumers of the ProjectReference | Verified: only `qyl.collector.csproj` references it, and all DI registrations use collector namespace |

## What this spec does NOT cover

- Dashboard React components for the 5-stage Loom UI (separate frontend spec)
- MCP tool definitions for Loom (existing 78 tools in `qyl.mcp` are unchanged)
- Workflow engine integration (`qyl.workflows` — deferred)
- GitHub Copilot SDK replacement strategy (orthogonal — removing the SDK is cleanup, not redesign)
- Authentication changes (GitHubService token bridge is unaffected)

## Verification gates

Before marking this spec as implemented:

1. **Build:** `./eng/build.sh Compile` succeeds, and Loom-related changes introduce no new warnings
2. **Schema:** `loom_sessions` and `loom_messages` tables created on startup
3. **Interactive flow:** `POST /api/v1/loom/{issueId}/chat` streams AG-UI events with stage transitions
4. **Tool signals:** `TOOL_CALL_START` with `toolName: "root_cause"` appears in SSE stream
5. **Interrupt:** `POST /interrupt` cancels running agent and appends user message
6. **Background:** `TriagePipelineService` creates headless sessions with root cause results
7. **Handoff:** `GET /pending-handoffs` returns background sessions; `POST /attach` converts to interactive
8. **Salvage:** `StatisticalMath` and `DistributionComparer` compile in `qyl.collector/Analytics/`
9. **Cleanup:** `qyl.loom` removed from solution; `QylCopilotAdapter` deleted; `Microsoft.Agents.AI.GitHub.Copilot` removed from `.csproj`
10. **Tests:** `LoomSessionStore` CRUD, `IssueContextBuilder` formatting, `ObservabilityContextProvider` message injection
