# Loom Design Specification

**qyl AI Debugging Agent**

| Field   | Value                                         |
|---------|-----------------------------------------------|
| Subject | qyl Loom — Conversational Debugging Agent     |
| Version | 2.0 (March 2026)                              |
| Status  | Architecture finalized, implementation active |

---

## Overview

Loom is qyl's AI debugging agent. One agent model, one tool surface, one state machine, dual trigger
(headless background + interactive SSE). The hero product moment is background-to-conversation handoff:
issue appears, background pipeline auto-investigates, dashboard shows root cause + proposed fix,
user clicks "Attach & Continue Chat", conversation opens with full prior context.

**Core differentiator:** Self-hosted Docker image with DuckDB. No cloud dependency, no GPU requirement.
The entire Loom surface runs on a single container.

---

## Table of Contents

1. [Architecture](#1-architecture)
2. [Five-Stage Flow](#2-five-stage-flow)
3. [Data Models](#3-data-models)
4. [API Surface](#4-api-surface)
5. [Services](#5-services)
6. [Handoff Flow](#6-handoff-flow)
7. [SSE Streaming Protocol](#7-sse-streaming-protocol)
8. [MCP Tools](#8-mcp-tools)
9. [DuckDB Storage](#9-duckdb-storage)
10. [Frontend](#10-frontend)
11. [Configuration](#11-configuration)
12. [Migration Plan](#12-migration-plan)
13. [Competitive Reference](#13-competitive-reference)

---

## 1. Architecture

One agent, two triggers, five stages.

```
┌─────────────────────────────────────────────────────────────┐
│                      LoomAgent (AIAgent)                     │
│  QylAgentBuilder.FromChatClient + ObservabilityContextProvider│
│  Tools: MCP tools (78) + native Loom tools                   │
│  State: DuckDB-backed LoomSession                            │
├──────────────┬──────────────────────────────────────────────┤
│  Background  │              Interactive                      │
│  Trigger     │              Trigger                          │
│              │                                               │
│  TriageService│  Dashboard POST /api/v1/loom/{issueId}/chat  │
│  (30s poll)  │  → AG-UI SSE streaming                        │
│  Creates     │  → 5-stage progressive UI                     │
│  headless    │  → mid-stream intervention via POST            │
│  session     │                                               │
├──────────────┴──────────────────────────────────────────────┤
│                     DuckDB (state store)                      │
│  loom_sessions: session state, stage, conversation history   │
│  loom_causal_steps: root cause chain per session             │
│  loom_solutions: solution steps per session                  │
│  autofix_runs: background pipeline runs                      │
└─────────────────────────────────────────────────────────────┘
```

### Dependency Chain

```
qyl.agents (QylAgentBuilder, LoomAgent, InstrumentedChatClient)
    ↓
qyl.loom (LoomEndpoints, LoomExplorerService, LoomInsightService)
    ↓
qyl.collector (TriagePipelineService, AutofixOrchestrator, PolicyGate, DuckDbStore)
    ↓
qyl.mcp (TriageTools, FixTools — HTTP bridge to collector)
    ↓
qyl.dashboard (LoomDashboardPage, use-Loom hooks, SSE consumer)
```

### What Stays in qyl.agents

- `QylAgentBuilder.FromChatClient` — unchanged, IS the path forward
- `InstrumentedChatClient` — OTel tracing for all LLM calls
- New: `LoomAgent.cs` — the conversational agent definition
- New: `LoomTools.cs` — native tools (root_cause, solution, code_it_up stage transitions)

### What Stays in qyl.collector

- `LoomEndpoints.cs` — rewritten for conversational (POST chat, GET session, SSE stream)
- `LoomExplorerService.cs` → becomes tool implementation consumed by LoomAgent
- `LoomInsightService.cs` → becomes tool implementation consumed by LoomAgent
- `TriagePipelineService.cs` — background trigger (30s poll)
- `AutofixOrchestrator.cs` — fix run lifecycle
- `PolicyGate.cs` — confidence gating
- New: `LoomSessionStore.cs` — DuckDB-backed session persistence

### qyl.loom is a standalone product

`src/qyl.loom/` is **not** dead code or a staging area. It is the C# transpile of the Sentry
reference implementation (reference material deleted; knowledge in `docs/reference/seer-knowledge-base.md`).
A separate standalone project also exists at `~/RiderProjects/qyl.loom/` with its own solution file.

`qyl.loom` references collector, agents, workflows, contracts, and instrumentation.
Do not decompose it into collector. Do not "salvage" files from it. It is the source of truth
for Loom product features.

---

## 2. Five-Stage Flow

Every Loom session progresses through five stages. Both background and interactive sessions
use the same pipeline — background runs autonomously, interactive streams via SSE.

```
Stage 1: Insight     → Pre-investigation context (fast, heuristic or LLM)
Stage 2: Explore     → Data ingestion + context assembly
Stage 3: Reason      → Streaming investigation monologue
Stage 4: Root Cause  → Structured causal chain extraction
Stage 5: Solution    → Editable fix plan → "Code It Up" → AutofixOrchestrator
```

### Stage 1: Insight

Fast pre-investigation sidebar context. No streaming.

**Endpoint:** `GET /api/v1/loom/{issueId}/insight`
**Service:** `LoomInsightService.GenerateInsightAsync()`

Two paths:

- **LLM path:** Build context → call LLM with `InsightGeneration` prompt → parse JSON
- **Heuristic fallback:** Error type pattern matching (NullReference, NetworkError, Timeout, etc.)

**Output:** `LoomInsight` — what happened, initial guess, trace context, external resources.

### Stage 2: Explore

Data ingestion phase. Loads issue + events from DuckDB, assembles context block.

**Progress:** 0–20%

### Stage 3: Reason

Streaming LLM investigation monologue. The agent "thinks out loud" — examining evidence,
correlating traces, identifying causal relationships.

**Endpoint:** `POST /api/v1/loom/{issueId}/explore` (SSE stream)
**Service:** `LoomExplorerService.ExploreAsync()`
**Progress:** 20–60%

The monologue streams character-by-character. Users can interrupt mid-stream via the
"Interrupt me..." input field.

### Stage 4: Root Cause

Structured causal chain extracted from the monologue. Each step in the chain is a
`LoomCausalStep` with an `Order`, `Description`, and `IsRootCause` boolean.

The root cause step is highlighted in red in the UI. Steps stream incrementally —
each one appears with a slide-in animation as the chain builds.

**Progress:** 60–80%
**Output:** `LoomRootCause` with `Summary` and `Steps[]`

### Stage 5: Solution

Editable solution checklist. Users can reorder, remove, or add steps before triggering
the autofix pipeline.

**Service:** `LoomExplorerService.RunSolutionPlanAsync()`
**Progress:** 80–100%
**Output:** `LoomSolution` with `Summary` and `Steps[]`

**"Code It Up" button** → `POST /api/v1/loom/{issueId}/code-it-up`
→ Creates `FixRunRecord` with `FixPolicy.AutoApply`
→ RCA → diff → confidence gate (0.85 threshold) → PR creation

---

## 3. Data Models

All models in `Qyl.Loom` namespace. Sealed records with snake_case JSON serialization
and source-generated JSON contexts for AOT.

### Core Models

```csharp
// Pre-investigation insight (Stage 1)
sealed record LoomInsight(
    string IssueId,
    string WhatHappened,
    string InitialGuess,
    string InTheTrace,
    LoomResource[] Resources);

sealed record LoomResource(string Title, string Url, string Description);

// Root cause chain (Stage 4)
sealed record LoomRootCause(string Summary, LoomCausalStep[] Steps);

sealed record LoomCausalStep(int Order, string Description, bool IsRootCause);

// Solution plan (Stage 5)
sealed record LoomSolution(string Summary, LoomSolutionStep[] Steps);

sealed record LoomSolutionStep(string Title, string Description);
```

### Request/Response Models

```csharp
// SSE exploration request
sealed record LoomExploreRequest(string? UserContext);

// "Code It Up" trigger
sealed record LoomCodeItUpRequest(string? Repo, string? BaseBranch);

// "Code It Up" response
sealed record LoomCodeItUpResponse(
    bool Success, string? RunId, string? PrUrl, string? Error);
```

### Autofix Pipeline Models

```csharp
// Fix policy determines automation behavior
enum FixPolicy { AutoApply, RequireReview, DryRun }

// Fix run record (DuckDB: fix_runs table)
sealed record FixRunRecord(
    string RunId, string IssueId, string ExecutionId,
    string Status,      // pending | running | review | applied | failed
    string Policy,      // FixPolicy as string
    string? FixDescription, double? ConfidenceScore, string? ChangesJson,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

// Coding agent providers
enum CodingAgentProvider { Loom, Cursor, GithubCopilot, ClaudeCode }

// Coding agent run (DuckDB: coding_agent_runs table)
sealed record CodingAgentRunRecord(
    string Id, string FixRunId, string Provider,
    string Status,      // pending | accepted | completed | failed | timed_out
    string? AgentUrl, string? PrUrl, string? RepoFullName,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
```

### Triage Models

```csharp
// Triage result (DuckDB: triage_results table)
sealed record TriageResult(
    string TriageId, string IssueId,
    double FixabilityScore,          // 0.0–1.0
    string AutomationLevel,          // auto | assisted | manual | skip
    string? AiSummary, string? RootCauseHypothesis,
    string? TriggeredBy, DateTimeOffset CreatedAt);

// Organization-level Loom settings (DuckDB: loom_settings table)
sealed record LoomSettingsRecord(
    string Id,
    string DefaultCodingAgent = "Loom",
    string? DefaultCodingAgentIntegrationId = null,
    string AutomationTuning = "medium",
    DateTimeOffset? UpdatedAt = null);
```

### Fixability Score Mapping

| Score     | Label        | Automation Action          |
|-----------|--------------|----------------------------|
| < 0.25    | `super_low`  | Skip                       |
| 0.25–0.40 | `low`        | Manual triage              |
| 0.40–0.66 | `medium`     | Assisted (root cause only) |
| 0.66–0.76 | `high`       | Code changes               |
| > 0.76    | `super_high` | Auto-fix + open PR         |

---

## 4. API Surface

### Loom Interactive Endpoints

| Method | Path                                | Purpose                     | Response                    |
|--------|-------------------------------------|-----------------------------|-----------------------------|
| `GET`  | `/api/v1/loom/{issueId}/insight`    | Pre-investigation insight   | `LoomInsight` JSON          |
| `POST` | `/api/v1/loom/{issueId}/explore`    | SSE streaming investigation | `text/event-stream`         |
| `POST` | `/api/v1/loom/{issueId}/code-it-up` | Trigger autofix pipeline    | `LoomCodeItUpResponse` JSON |

### Loom Settings Endpoints

| Method | Path                            | Purpose                  | Response                     |
|--------|---------------------------------|--------------------------|------------------------------|
| `GET`  | `/api/v1/loom/settings/{orgId}` | Get org Loom settings    | `LoomSettingsRecord` JSON    |
| `PUT`  | `/api/v1/loom/settings/{orgId}` | Update org Loom settings | Updated `LoomSettingsRecord` |

### Triage & Fix Endpoints

| Method  | Path                                        | Purpose               | Response         |
|---------|---------------------------------------------|-----------------------|------------------|
| `GET`   | `/api/v1/triage`                            | List triage results   | `TriageResult[]` |
| `GET`   | `/api/v1/issues/{issueId}/triage`           | Get triage for issue  | `TriageResult`   |
| `POST`  | `/api/v1/issues/{issueId}/triage`           | Trigger manual triage | `TriageResult`   |
| `POST`  | `/api/v1/issues/{issueId}/fix-runs`         | Create fix run        | `FixRunRecord`   |
| `PATCH` | `/api/v1/issues/{issueId}/fix-runs/{runId}` | Update fix run        | `FixRunRecord`   |

### Supporting Endpoints

| Method | Path                                          | Purpose                |
|--------|-----------------------------------------------|------------------------|
| `GET`  | `/api/v1/handoffs/pending`                    | Pending agent handoffs |
| `GET`  | `/api/v1/regressions`                         | Recent regressions     |
| `POST` | `/api/v1/regressions/check/{serviceName}`     | Check for regressions  |
| `GET`  | `/api/v1/github/events`                       | GitHub webhook events  |
| `POST` | `/api/v1/code-review/{repo}/pulls/{prNumber}` | Trigger code review    |

---

## 5. Services

### LoomInsightService

**Location:** `src/qyl.loom/LoomInsightService.cs`

Generates pre-investigation insight for Stage 1. Two paths:

**LLM path:**

1. Build context block from issue + recent events
2. Call LLM with `LoomPrompts.InsightGeneration` prompt
3. Parse JSON response → map to `LoomInsight`
4. Fallback to heuristic if LLM fails

**Heuristic path:**

- Base score: 0.3
- Error type pattern matching (NullReference → +0.2, NetworkError → specific guidance)
- Generates "What Happened" and "Initial Guess" from error metadata
- Builds resources list based on error category

### LoomExplorerService

**Location:** `src/qyl.loom/LoomExplorerService.cs`

Interactive streaming investigation for Stages 2–5.

```csharp
IAsyncEnumerable<StreamUpdate> ExploreAsync(
    string issueId, string? userContext, CancellationToken ct)
```

**Phase pipeline:**

| Phase | Progress | Method                   | Purpose                                  |
|-------|----------|--------------------------|------------------------------------------|
| 1     | 0–20%    | DuckDB lookup            | Load issue + events                      |
| 2     | 20–60%   | `StreamMonologueAsync()` | Stream LLM investigation                 |
| 3     | 60%      | `TryParseRootCause()`    | Extract JSON root cause from monologue   |
| 4     | 60–80%   | `RunSolutionPlanAsync()` | Generate solution plan (single LLM call) |
| 5     | 100%     | Emit COMPLETED           | Done                                     |

JSON extraction tolerates markdown code blocks — `TryParseRootCause()` and
`TryParseSolution()` strip fences before parsing.

### TriagePipelineService

**Location:** `src/qyl.collector/Autofix/TriagePipelineService.cs`

Background service scanning for untriaged error issues.

| Config         | Default | Env Var                       |
|----------------|---------|-------------------------------|
| Enabled        | true    | `QYL_TRIAGE_ENABLED`          |
| Poll interval  | 30s     | `QYL_TRIAGE_INTERVAL_SECONDS` |
| Auto-threshold | 0.8     | `QYL_TRIAGE_AUTO_THRESHOLD`   |
| Batch size     | 20      | Hardcoded                     |

**Scoring pipeline:**

```
Issue → ScoreWithLlmAsync() OR ScoreWithHeuristic()
  → TriageResult (fixabilityScore, automationLevel, aiSummary, rootCauseHypothesis)
  → DuckDbStore.InsertTriageResultAsync()
  → If score ≥ 0.8: AutofixOrchestrator.CreateFixRunAsync()
```

**Heuristic scoring:**

- Base: 0.3
- +0.15 if 10+ occurrences, +0.1 if 3+
- +0.2 if NullReference/ArgumentException/InvalidOperation
- +0.1 if <1h old, +0.05 if <24h old
- Automation level: ≥0.8 auto, ≥0.5 assisted, ≥0.2 manual, <0.2 skip

### AutofixOrchestrator

**Location:** `src/qyl.collector/Autofix/AutofixOrchestrator.cs`

Fix run lifecycle management.

```
CreateFixRunAsync(issueId, issue, policy)
  → Creates WorkflowExecutionRecord (name: "autofix", status: "pending")
  → Creates FixRunRecord linked to execution
  → Both stored in DuckDB

UpdateFixRunStatusAsync(runId, status, description, confidence, changesJson)
  → Updates fix run fields in DuckDB

LaunchCodingAgentAsync(fixRunId, provider, repoFullName)
  → Creates CodingAgentRunRecord
  → Supports: Loom, Cursor, GithubCopilot, ClaudeCode
```

### PolicyGate

**Location:** `src/qyl.collector/Autofix/PolicyGate.cs`

```
DefaultAutoApplyThreshold = 0.85

ShouldApply(AutoApply, 0.90, 0.85) → true   (apply)
ShouldApply(AutoApply, 0.70, 0.85) → false  (review)
ShouldApply(RequireReview, 0.99)   → false   (always review)
ShouldApply(DryRun, 0.99)         → false   (preview only)
```

### AutofixAgentService

**Location:** `src/qyl.collector/Autofix/AutofixAgentService.cs`

Background service processing pending fix runs every 15s.

**5-step pipeline:**

1. Gather context (issue details + recent events)
2. Generate root cause analysis via LLM
3. Generate solution plan
4. Generate diff/patch (changes_json)
5. Score confidence + apply policy gate

---

## 6. Handoff Flow

The hero product moment: background investigation → dashboard notification → live session
with full prior context.

```
1. TriagePipelineService scores issue → fixability ≥ 0.8
2. Creates headless LoomSession → agent runs Insight/Explore/RCA autonomously
3. Results stored in DuckDB (causal steps, solution, agent reasoning)
4. Dashboard shows "Loom auto-investigated" badge on issue
5. User clicks "Attach & Continue Chat" → hydrates SSE session from stored results
6. User can edit solution, ask follow-ups, redirect investigation
7. "Code It Up" → AutofixOrchestrator (RCA → diff → confidence gate → PR)
```

### Session Types

| Type           | Trigger                                              | Interaction                      | Storage                 |
|----------------|------------------------------------------------------|----------------------------------|-------------------------|
| Background     | TriagePipelineService (30s poll)                     | None — fully automatic           | Results in DuckDB       |
| Conversational | User clicks "Start Loom" or "Attach & Continue Chat" | SSE streaming + mid-stream input | Session state in DuckDB |

Background sessions store results in DuckDB. Conversational sessions stream via AG-UI SSE.
Handoff hydrates background results into a live session.

---

## 7. SSE Streaming Protocol

**Decision: Tool calls as stage signals** (not custom event types).

Stage transitions ARE tool calls. The agent deciding to "call the root_cause tool" IS the
stage transition — the agent saying "I found it, here's the chain." AG-UI already has
`tool_call_start`/`tool_call_delta`/`tool_call_end` as first-class events. The dashboard
uses a standard AG-UI consumer — no custom event parser. CopilotKit or any future AG-UI
client gets Loom rendering for free.

### AG-UI Event Stream

```
// Stage 0: Insight (pre-loaded, no SSE — fetched via GET /insight)

// Stage 1: Explore (agent monologue — text_delta events)
data: {"type":"text_delta", "content":"Looking at the trace..."}
data: {"type":"text_delta", "content":"the POST /checkout handler..."}

// Stage 2-3: Root Cause (tool call — streams LoomCausalStep[] incrementally)
data: {"type":"tool_call_start",
       "toolName":"root_cause",
       "args":{"issueId":"4521"}}
data: {"type":"tool_call_delta",
       "content":"{\"steps\":[{\"order\":1,\"description\":\"User initiates...\",\"is_root_cause\":false}]}"}
data: {"type":"tool_call_delta",
       "content":"{\"steps\":[{\"order\":4,\"description\":\"Backend throws...\",\"is_root_cause\":true}]}"}
data: {"type":"tool_call_end"}

// Stage 4: Solution (tool call — streams LoomSolution incrementally)
data: {"type":"tool_call_start",
       "toolName":"solution"}
data: {"type":"tool_call_delta",
       "content":"{\"steps\":[{\"title\":\"Define DTOs\",\"description\":\"Create strongly-typed...\"}]}"}
data: {"type":"tool_call_end"}
```

### Why Tool Calls, Not Custom Events

| Concern                 | Tool Calls                                            | Custom Events                 |
|-------------------------|-------------------------------------------------------|-------------------------------|
| AG-UI compatibility     | Native — `tool_call_start`/`delta`/`end` are standard | Custom parser required        |
| CopilotKit integration  | Works automatically                                   | Would need custom rendering   |
| Incremental streaming   | `tool_call_delta` carries partial JSON                | Would need own delta protocol |
| Frontend complexity     | Switch on `toolName`                                  | Parse custom event vocabulary |
| Mixed tool/stage stream | Unified — real tools + stage tools in same stream     | Parallel event systems        |

### Frontend Rendering by toolName

| `toolName`    | Component                                               | Stage  |
|---------------|---------------------------------------------------------|--------|
| `root_cause`  | `CausalChain` — vertical timeline, red root cause node  | 4      |
| `solution`    | `SolutionEditor` — editable checklist with "Code It Up" | 5      |
| `code_it_up`  | `CodeItUpConfirmation` — pipeline status                | Post-5 |
| *(any other)* | Generic tool call display                               | N/A    |

### StreamUpdate Model (Backend)

```csharp
sealed record StreamUpdate {
    StreamUpdateKind Kind;      // Content | ToolCall | ToolResult | Progress | Completed | Error | Metadata
    string? Content;            // Text being streamed
    string? ToolName;           // Tool name — "root_cause" | "solution" | MCP tool name
    string? ToolArguments;      // JSON args (for ToolCall)
    string? ToolResult;         // Result (for ToolResult)
    string? Error;              // Error message (for Error)
    int? Progress;              // 0–100 (for Progress)
    long? InputTokens;          // Running token count
    long? OutputTokens;
    DateTimeOffset Timestamp;
}
```

---

## 8. MCP Tools

### TriageTools

**Location:** `src/qyl.mcp/Tools/TriageTools.cs`

| Tool                 | Method | Purpose                                                      |
|----------------------|--------|--------------------------------------------------------------|
| `qyl.get_triage`     | GET    | Retrieve triage result for issue                             |
| `qyl.list_triage`    | GET    | List triage results (filter by automation level, limit ≤100) |
| `qyl.trigger_triage` | POST   | Manually trigger triage → may auto-route to autofix          |

### FixTools

**Location:** `src/qyl.mcp/Tools/FixTools.cs`

| Tool               | Method | Purpose                          |
|--------------------|--------|----------------------------------|
| `qyl.generate_fix` | POST   | Two-pass fix generation pipeline |

**`qyl.generate_fix` pipeline:**

```
Phase 1 — RCA Agent (≤8 tool calls)
  Tools: ErrorTools, SpanQueryTools, StructuredLogTools
  Prompt: FixGenPrompt.RcaSystem
  Output: Structured RCA report

Phase 2 — Fix Generation (single LLM call)
  Input: issueId, runId, RCA report, user context
  Prompt: FixGenPrompt.FixGenSystem
  Output: changes_json with confidence score
  Gate: PolicyGate.EvaluateNextStatus(policy, confidence, 0.85)
```

---

## 9. DuckDB Storage

### Loom-Specific Tables

| Table                 | Purpose             | Key Columns                                                                                                |
|-----------------------|---------------------|------------------------------------------------------------------------------------------------------------|
| `triage_results`      | Issue triage scores | triage_id, issue_id, fixability_score, automation_level, ai_summary, root_cause_hypothesis, scoring_method |
| `fix_runs`            | Fix run lifecycle   | run_id, issue_id, execution_id, status, policy, fix_description, confidence_score, changes_json            |
| `autofix_steps`       | Per-step tracking   | step_id, run_id, step_number, step_name, status, input_json, output_json, error_message                    |
| `coding_agent_runs`   | External agent runs | id, fix_run_id, provider, status, agent_url, pr_url, repo_full_name                                        |
| `loom_settings`       | Org-level config    | id, default_coding_agent, automation_tuning                                                                |
| `workflow_executions` | Workflow tracking   | execution_id, workflow_name, trigger, status, input_json, output_json                                      |

### Telemetry Tables (Shared)

| Table   | Purpose                 | Row Model                                    |
|---------|-------------------------|----------------------------------------------|
| `spans` | OTel spans (26 columns) | `SpanStorageRow` — promoted GenAI attributes |
| `logs`  | OTel logs (16 columns)  | `LogStorageRow` — structured log records     |

---

## 10. Frontend

### Dashboard

**Location:** `src/qyl.dashboard/src/pages/LoomDashboardPage.tsx`

Displays four stat cards and two tables:

**Stats:** Triage Results | Auto-Fix Eligible | Pending Handoffs | Regressions

**Tables:**

1. Recent Triage Results — issue ID, fixability score %, automation level, AI summary, triggered by, created time
2. Pipeline Activity — GitHub events (event type, repo, action, sender, time)

### React Query Hooks

**Location:** `src/qyl.dashboard/src/hooks/use-Loom.ts`

| Hook                             | Endpoint                                              | Stale Time |
|----------------------------------|-------------------------------------------------------|------------|
| `useTriageResult(issueId)`       | `GET /api/v1/issues/{issueId}/triage`                 | 30s        |
| `useTriageResults(limit)`        | `GET /api/v1/triage`                                  | 30s        |
| `useFixRunSteps(issueId, runId)` | `GET /api/v1/issues/{issueId}/fix-runs/{runId}/steps` | 30s        |
| `useRegressions(limit)`          | `GET /api/v1/regressions`                             | 30s        |
| `usePendingHandoffs()`           | `GET /api/v1/handoffs/pending`                        | 30s        |
| `useGitHubEvents(limit)`         | `GET /api/v1/github/events`                           | 30s        |
| `useTriggerCodeReview()`         | `POST /api/v1/code-review/{repo}/pulls/{prNumber}`    | mutation   |
| `useCheckRegressions()`          | `POST /api/v1/regressions/check/{serviceName}`        | mutation   |

### 5-Stage Conversational UI

The interactive Loom session renders a 5-stage progressive UI:

```
┌────────────────────────────────────────────────┐
│  Loom ◎                        Give Feedback   │
│  REACT-SYR › c44dcbff           Start Over     │
├────────────────────────────────────────────────┤
│  ▬▬▬▬▬▬ ▬▬▬▬▬▬ ▬▬▬▬▬▬ ▬▬▬▬▬▬ ░░░░░░         │
│  Insight Explore Reason RootCause Solution     │
├────────────────────────────────────────────────┤
│                                                │
│  [Stage-specific content renders here]         │
│                                                │
│  Stage 1: Insight panel with What Happened,    │
│           In the Trace, Initial Guess,         │
│           Resources → "Start Loom" button      │
│                                                │
│  Stage 3: Streaming monologue with             │
│           "Figuring out the root cause..."     │
│           progress bar, code highlighting,     │
│           "Interrupt me..." input field        │
│                                                │
│  Stage 4: Causal chain visualization —         │
│           vertical timeline with nodes,        │
│           root cause highlighted in red,       │
│           expandable step details              │
│                                                │
│  Stage 5: Editable solution checklist,         │
│           reorder/remove/add steps,            │
│           "Code It Up" button →                │
│           AutofixOrchestrator pipeline         │
│                                                │
│  Completion: "Autofix pipeline started"        │
│           View Fix PR | Start Fresh            │
│                                                │
├────────────────────────────────────────────────┤
│  qyl.loom — conversational debugger  stage N/5 │
└────────────────────────────────────────────────┘
```

**Design details:**

- Dark theme (`#0e0e1a` background, `#e0e0f0` text, `#c084fc` accent)
- DM Sans + JetBrains Mono fonts
- Causal chain: vertical line gradient, root cause node pulses red with glow
- Solution steps: numbered circles, highlighted key step, remove/reorder controls
- Stage progress bar: 5-segment with gradient transitions
- Streaming cursor: blinking `#c084fc` line during monologue

### Prototype Component

A standalone React prototype of the 5-stage flow exists at
`src/qyl.dashboard/src/components/Loom/LoomPrototype.tsx`.

This prototype demonstrates:

- `InsightPanel` — Stage 1 with expandable sections and "Start Loom" CTA
- `AgentMonologue` — Stage 3 with character-by-character streaming, markdown rendering
- `CausalChain` — Stage 4 with incremental step reveal, root cause highlighting
- `SolutionEditor` — Stage 5 with editable checklist and "Code It Up" button
- `CodeItUpConfirmation` — Completion state with pipeline status

---

## 11. Configuration

### Environment Variables

| Variable                      | Default      | Purpose                          |
|-------------------------------|--------------|----------------------------------|
| `QYL_TRIAGE_ENABLED`          | `true`       | Enable/disable background triage |
| `QYL_TRIAGE_INTERVAL_SECONDS` | `30`         | Triage poll interval             |
| `QYL_TRIAGE_AUTO_THRESHOLD`   | `0.8`        | Auto-route threshold             |
| `QYL_PORT`                    | `5100`       | Dashboard + REST API             |
| `QYL_GRPC_PORT`               | `4317`       | gRPC OTLP ingestion              |
| `QYL_OTLP_PORT`               | `4318`       | HTTP OTLP ingestion              |
| `QYL_DATA_PATH`               | `qyl.duckdb` | DuckDB file path                 |

### Automation Tuning Levels

| Level       | Behavior                            |
|-------------|-------------------------------------|
| `off`       | No automatic triage or autofix      |
| `super_low` | Triage only, no automation          |
| `low`       | Auto-triage, manual fix             |
| `medium`    | Auto-triage, assisted fix (default) |
| `high`      | Auto-triage, auto-fix with review   |
| `always`    | Full automation, auto-apply fixes   |

### Policy Gate Thresholds

| Policy          | Confidence ≥ 0.85   | Confidence < 0.85 |
|-----------------|---------------------|-------------------|
| `AutoApply`     | Apply automatically | Require review    |
| `RequireReview` | Always review       | Always review     |
| `DryRun`        | Preview only        | Preview only      |

---

## 12. Migration Plan

**Decision: qyl.loom stays as its own project.** It is the C# port of the Sentry reference
(reference deleted; see `docs/reference/seer-knowledge-base.md`). All files live in `src/qyl.loom/` and are the authoritative implementation.
Some files are also referenced from `qyl.collector/Autofix/` — those collector copies should
be treated as consumers, not replacements.
| `TriagePipelineService.cs` | `qyl.collector/Autofix/` | Background trigger stays, scoring logic refactored         |
| `AutofixArtifacts.cs`      | `qyl.contracts/Autofix/` | Shared type definitions                                    |
| `AutofixConstants.cs`      | `qyl.contracts/Autofix/` | Shared constants                                           |

**DELETE — superseded or incomplete:**

| Files                    | Reason                                 |
|--------------------------|----------------------------------------|
| `LoomExplorerService.cs` | Collector has better implementation    |
| `LoomInsightService.cs`  | Collector has better implementation    |
| All endpoint files       | Rewriting for conversational model     |
| `GlobalUsings.cs`        | Project being deleted                  |
| ~35 other files          | Scaffolding, duplicates, or incomplete |

### Phase 1: Consolidate Agent Model

Replace the Copilot adapter pattern with LoomAgent built on QylAgentBuilder:

```
Before:
  QylCopilotAdapter → Microsoft.Agents.AI.GitHub.Copilot → AG-UI endpoints

After:
  QylAgentBuilder.FromChatClient → LoomAgent (AIAgent) → AG-UI SSE endpoints

Delete:
  QylCopilotAdapter.cs (556 lines)
  CopilotSessionStore.cs
  CopilotAguiEndpoints.cs
  Microsoft.Agents.AI.GitHub.Copilot package reference
```

### Phase 2: DuckDB Session Persistence

Replace in-memory session store with DuckDB-backed persistence:

```sql
CREATE TABLE loom_sessions (
    session_id    VARCHAR PRIMARY KEY,
    issue_id      VARCHAR NOT NULL,
    session_type  VARCHAR NOT NULL,  -- 'background' | 'conversational'
    stage         INTEGER NOT NULL DEFAULT 0,
    stage_name    VARCHAR,
    conversation  JSON,              -- full message history
    root_cause    JSON,              -- LoomRootCause
    solution      JSON,              -- LoomSolution
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at  TIMESTAMP
);
```

### Phase 3: Tool Integration (AG-UI tool calls as stage signals)

Expose LoomExplorerService and LoomInsightService as tools within LoomAgent.
Stage transitions are tool calls — the agent "calls" root_cause/solution tools
to emit structured data that the frontend renders as stage-specific UI:

| Tool              | Maps To               | AG-UI Event                     | Purpose                                             |
|-------------------|-----------------------|---------------------------------|-----------------------------------------------------|
| `loom.insight`    | `LoomInsightService`  | N/A (pre-fetched via GET)       | Pre-investigation context                           |
| `loom.explore`    | `LoomExplorerService` | `text_delta`                    | Streaming monologue                                 |
| `loom.root_cause` | Stage transition      | `tool_call_start`/`delta`/`end` | Emit structured RCA → CausalChain component         |
| `loom.solution`   | Stage transition      | `tool_call_start`/`delta`/`end` | Emit structured fix plan → SolutionEditor component |
| `loom.code_it_up` | `AutofixOrchestrator` | `tool_call_start`/`end`         | Trigger fix pipeline                                |

### Phase 4: Handoff Hydration

Implement background → conversational session handoff:

1. Background session completes → results in DuckDB
2. Dashboard polls `/api/v1/handoffs/pending`
3. User clicks "Attach & Continue Chat"
4. `POST /api/v1/loom/{issueId}/chat` with `?hydrate=true`
5. Server loads background session, converts to conversational
6. Streams prior results as catch-up events (replayed tool calls), then enters live mode

---

## 13. Competitive Reference

This section provides condensed competitive context from Sentry's Loom/Seer system.
Full reverse-engineering details from the original spec are preserved in
`docs/archive/loom-sentry-reference.md`.

### Sentry Seer Architecture (Summary)

Sentry's Seer is a separate service cluster from the Django monolith:

- 7+ microservice pods (Autofix, Summarization, Anomaly Detection, Grouping, Breakpoint, Scoring GPU, Code Review)
- Python 3.11, gRPC + Gunicorn, Celery + RabbitMQ
- PostgreSQL + pgvector for embeddings
- HMAC-SHA256 authentication between Sentry and Seer
- Feature flags via Flagpole
- **Cloud-only** — NOT available for self-hosted Sentry

### Key Differentiators (qyl vs Sentry Seer)

| Dimension     | qyl Loom                             | Sentry Seer                   |
|---------------|--------------------------------------|-------------------------------|
| Deployment    | Self-hosted Docker, single container | Cloud-only, 7+ pods           |
| Storage       | DuckDB (columnar, zero-config)       | PostgreSQL + pgvector + GCS   |
| GPU           | Not required                         | Required for scoring          |
| Embeddings    | Not used (heuristic + LLM)           | jina-embeddings-v2 (768d)     |
| Agent model   | Single AIAgent (QylAgentBuilder)     | Multiple specialized services |
| Streaming     | AG-UI SSE                            | Custom SSE                    |
| Observability | OTel-native (eat our own dogfood)    | Langfuse                      |
| SCM           | GitHub (extensible)                  | GitHub Cloud only             |

### Sentry Capability Mapping

| Sentry Capability        | qyl Equivalent                                | Status      |
|--------------------------|-----------------------------------------------|-------------|
| Fixability Scoring (GPU) | `TriagePipelineService` (LLM/heuristic)       | Implemented |
| Autofix Pipeline         | `AutofixOrchestrator` + `AutofixAgentService` | Implemented |
| Explorer Agent           | `LoomExplorerService` (SSE streaming)         | Implemented |
| Issue Summarization      | `LoomInsightService`                          | Implemented |
| Code Review (Prevent)    | `CodeReviewService`                           | Implemented |
| Coding Agent Handoff     | `CodingAgentProvider` (4 providers)           | Implemented |
| Anomaly Detection        | `AnomalyService`                              | Implemented |
| Issue Grouping (ML)      | Not planned (heuristic fingerprinting)        | N/A         |
| Trace Summarization      | Not yet implemented                           | Planned     |
| Test Generation          | Not planned                                   | N/A         |

### sentry-mcp Reference

Sentry's MCP server uses a skills-based authorization model with 5 skill bundles
and HMAC-SHA256 signed OAuth state. Key patterns adopted by qyl:

- **Skills system:** qyl uses `QylSkillKind` (8 categories) with `QYL_SKILLS` env var
- **Embedded agent:** sentry-mcp wraps tools in a Vercel AI SDK agent; qyl uses QylAgentBuilder
- **Dual mode:** stdio for local, HTTP for remote; qyl supports stdio via `qyl.mcp`

---

## LLM Prompt Templates

### InsightGeneration

System prompt for Stage 1. Input: error type, message, occurrence count, timestamps, status.
Output: JSON with `what_happened`, `initial_guess`, `in_the_trace`, `resources[]`.

### ExplorerMonologue

System prompt for Stage 3. Instructs the LLM to "think out loud" through evidence examination,
correlation, and causal chain identification. Output: narrative text followed by a JSON block
with `summary` and `steps[]` (each with `order`, `description`, `is_root_cause`).

### SolutionPlanning

System prompt for Stage 5. Input: root cause analysis from monologue.
Output: JSON with `summary` and `steps[]` (each with `title`, `description`).
Rules: minimal steps, no testing/refactoring suggestions, focus on the fix.

---

## File Map

```
src/
├── loom/                               # Sentry reference implementation (TS + Python, read-only)
├── qyl.loom/                           # C# transpile of Sentry reference — Loom product features
│   ├── LoomModels.cs                   # Data models (Insight, RootCause, Solution)
│   ├── LoomEndpoints.cs                # REST + SSE endpoints
│   ├── LoomExplorerService.cs          # Streaming investigation (Stages 2–5)
│   ├── LoomInsightService.cs           # Pre-investigation insight (Stage 1)
│   ├── LoomPrompts.cs                  # LLM prompt templates
│   └── LoomSettingsEndpoints.cs        # Organization settings
│
├── qyl.collector/
│   ├── Autofix/
│   │   ├── TriagePipelineService.cs    # Background triage (30s poll)
│   │   ├── AutofixOrchestrator.cs      # Fix run lifecycle
│   │   ├── AutofixAgentService.cs      # Background fix processing (15s poll)
│   │   └── PolicyGate.cs               # Confidence gating (0.85 threshold)
│   ├── CodingAgent/
│   │   └── CodingAgentProvider.cs      # Loom | Cursor | GithubCopilot | ClaudeCode
│   └── Storage/
│       ├── DuckDbStore.Triage.cs       # Triage result CRUD
│       ├── DuckDbStore.AutofixSteps.cs # Autofix step tracking
│       └── DuckDbStore.CodingAgent.cs  # Coding agent + settings CRUD
│
├── qyl.mcp/Tools/
│   ├── TriageTools.cs                  # qyl.get_triage, qyl.list_triage, qyl.trigger_triage
│   └── FixTools.cs                     # qyl.generate_fix (two-pass pipeline)
│
├── qyl.agents/                         # Agent definitions
│   ├── QylAgentBuilder.cs              # Agent builder (FromChatClient)
│   └── InstrumentedChatClient.cs       # OTel-traced LLM calls
│
├── qyl.dashboard/src/
│   ├── pages/LoomDashboardPage.tsx     # Loom dashboard
│   ├── hooks/use-Loom.ts              # React Query hooks
│   └── components/Loom/               # UI components
│       └── LoomPrototype.tsx           # 5-stage conversational UI prototype
│
docs/
└── archive/
    └── loom-sentry-reference/          # REFERENCE ONLY — reverse-engineered Sentry Seer/Loom code
        ├── anomaly_detection/types.py  #   Not part of the build. Kept for competitive context.
        ├── autofix/                    #   Ported algorithms live in qyl.collector/Analytics/
        │   ├── prompts.py              #   and qyl.collector/Autofix/ respectively.
        │   ├── types.py
        │   ├── constants.py
        │   └── artifact_schemas.py
        ├── math.py
        ├── vendored.py
        └── workflows/compare.py
```
