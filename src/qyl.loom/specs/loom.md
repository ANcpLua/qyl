# Loom Specification

> Owner: loom
> SSOT: YES (AI investigation pipeline, autofix, triage, code review, regression detection)
> Depends on: `specs/telemetry-intelligence.md` (pattern engine), `specs/telemetry-data-model.md` (schema), `specs/issue-fingerprinting.md` (error grouping)
> Used by: `specs/mcp.md` (analysis tool delegation)

AI-powered issue investigation and autofix. C# transpile of Sentry Seer. Standalone product.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Pipeline](#2-pipeline)
3. [Policy Gate](#3-policy-gate)
4. [Autofix](#4-autofix)
5. [Code Review](#5-code-review)
6. [Regression](#6-regression)
7. [Triage](#7-triage)
8. [Identity and GitHub](#8-identity)
9. [Constraints](#9-constraints)
10. [Definition of Done](#10-definition-of-done)

---

## 1. Overview

`src/qyl.loom/` — standalone product. Communicates with collector over HTTP via `CollectorClient`.

**Deployment:** Own Dockerfile at `src/qyl.loom/Dockerfile`. Runs as a Docker service alongside collector via root `docker-compose.yml`. No ProjectReference to collector, no DuckDB dependency.

Agent construction uses MAF directly: `AIAgent`, `IChatClient`, `AddAIAgent()`. Shared types (`CodingAgentProvider`, `CodingAgentRunRecord`, `LoomSettingsRecord`) live in `qyl.contracts/Loom/`.

### 1.1 If you ask "what is Loom?"

Loom is **qyl's Seer-equivalent product**.

It is not "the AI dashboard", not "the MCP server", and not "just Autofix".
Those are adjacent surfaces in the same overall product story, just like they are in Sentry:

- **qyl.collector + qyl.dashboard + qyl.instrumentation** are the observability substrate
- **qyl.mcp** is the agent-native access surface
- **qyl.loom** is the intelligence plane that consumes that context to investigate, explain, review, and fix

If you need the shortest possible definition:

> **Sentry Seer** is the AI debugger on top of Sentry context.  
> **qyl.loom** is the AI debugger on top of qyl context.

### 1.2 Sentry 1:1 product mapping

| Sentry surface | qyl surface | Mechanical owner | Notes |
|---|---|---|---|
| **Seer** | `src/qyl.loom/` | Loom | Standalone AI debugger / intelligence plane |
| **AI and LLM Observability** | `qyl.collector` + `qyl.dashboard` + `qyl.instrumentation` | Collector / dashboard / instrumentation | Runs, tokens, model costs, tool usage, traces, logs |
| **AI Agents Dashboard** | `AgentInsightsEndpoints` + dashboard agent views | Loom over collector data | Overview, Models, Tools, trace list, trace detail |
| **MCP Dashboard** | `qyl.mcp` + collector-side MCP telemetry | MCP + collector | MCP traffic, client mix, tool/resource/prompt visibility |
| **Autofix** | Loom 5-stage pipeline | Loom | Root cause → solution → diff → confidence |
| **PR Creation** | `PrCreationService` + `POST /api/v1/loom/{issueId}/code-it-up` | Loom | Draft/open PR from generated fix |
| **Coding Agents** | `CodingAgentProvider` (`Loom`, `Cursor`, `GithubCopilot`, `ClaudeCode`) | Loom | Delegate code generation or handoff externally |
| **Code Review / Bug Prediction** | `CodeReviewService`, `CodeReviewEndpoints`, `qyl.trigger_code_review` | Loom + MCP | Pre-merge AI review surface |
| **Interactive root-cause UI** | `GET /api/v1/loom/{issueId}/insight` + `POST /api/v1/loom/{issueId}/explore` (SSE) | Loom | Insight → exploration → code-it-up workflow |
| **Issue fix state** | `fix_runs`, `autofix_steps`, `artifacts`, `agent_runs`, `tool_calls` | Collector storage + Loom runtime | Durable state and replay surface |

### 1.3 What Loom is not

Do not collapse Loom into one of these narrower readings:

- **Not just Autofix** — Seer is not only Autofix, and Loom is not only fix generation
- **Not the dashboard** — dashboards show context; Loom consumes context to reason
- **Not the MCP server** — MCP is access plumbing, not the investigation product
- **Not collector-owned** — the collector stores and serves facts; Loom owns AI runtime behavior

The product cut should be read exactly like Sentry's:

```text
Telemetry substrate + debugging surfaces + AI debugger
```

For qyl that means:

```text
qyl.collector/qyl.dashboard/qyl.instrumentation + qyl.mcp + qyl.loom
```

### 1.4 Collector vs Loom

| | Collector | Loom |
|---|---|---|
| **Role** | Data plane — ingest, store, serve, compute | Intelligence plane — autonomous AI agent |
| **Communication** | Serves REST API | Consumes collector REST API over HTTP |
| **LLM** | Optional `IChatClient?` for heuristic+LLM triage/autofix. Self-disables without LLM. | Required. Multi-step reasoning, provider SDKs, agent orchestration. |
| **Deployment** | Running (Railway) | Standalone process, separate deployment |

Loom enhances collector — it doesn't replace it. Collector's pipelines work with heuristic fallbacks. Loom adds autonomous reasoning on top.

### 1.5 Official Seer evidence that drives this reading

The official Sentry materials consistently describe one product stack, not one isolated feature:

- **Seer docs** define Seer as the AI debugging agent and list **Autofix**, **PR Creation**, **Coding Agents**, and **Code Review** as first-class capabilities.
- **Autofix docs** define a three-step flow: **Root Cause Analysis**, **Solution Identification**, and **Code Generation**, with optional PR creation and coding-agent delegation.
- **AI Agents Dashboard docs** describe three tabs: **Overview**, **Models**, and **Tools**, plus abbreviated and full trace views with agent invocations, model interactions, tool calls, timing, handoffs, and errors.
- **MCP Dashboard docs** describe MCP traffic, clients, transport distribution, and tool/resource/prompt usage and failure views as part of the same AI observability story.
- **.NET AI instrumentation docs** show Sentry instrumenting `Microsoft.Extensions.AI` via `IChatClient.AddSentry()` and `AddSentryToolInstrumentation()`, which is the closest product analogue to qyl's AI/agent telemetry path.
- The Seer GA post and user stories show the intended product behavior: root-causing issues from Sentry context, debugging across multiple repositories, drafting PRs, and delegating to external coding agents.

The workshop transcripts and screenshots follow the same flow: overview traffic and duration, model/token views, tool-call inspection, full trace drill-down, database-call correlation, then alerting from the same context.

I did not find a clearly indexed public archive of the February/March Seer workshop recordings from official search. Treat the official docs and GA blog as the canonical sources of truth.

## 2. Pipeline

Five-stage autofix pipeline:

```text
1. Context Gathering    → collect error, stack trace, code, related telemetry
2. Root Cause Analysis  → LLM analyzes root cause from context
3. Solution Planning    → LLM plans fix approach
4. Diff Generation      → LLM generates code diff
5. Confidence Scoring   → LLM scores confidence in the fix
```

Each stage persisted as `AutofixStepRecord` in DuckDB.

### 2.1 Context Builder

`IssueContextBuilder` assembles context for the LLM:

- Error details and stack trace
- Relevant source code
- Related telemetry (traces, logs around the error)
- Historical fixes for similar issues

### 2.2 Investigation Input Contract

Every investigation run receives a normalized input bundle. `IssueContextBuilder` assembles this. No agent builds its own context.

| Field | Source | Required |
|-------|--------|----------|
| **Issue summary** | `error_issues` row (type, category, occurrence_count, first/last seen, priority, status) | Yes |
| **Grouped occurrences** | `error_issue_events` (all trace_ids, span_ids, stack_traces, environments, release_versions) | Yes |
| **Trace graph** | `spans` joined by `trace_id` → full span tree with parent/child relationships | Yes |
| **Code context** | Spans' `code.filepath`, `code.function`, `code.lineno` → source file content | Yes — guaranteed for `[Traced]` methods (compile-time emission) |
| **Deployment context** | `deployments` table: `service_version`, `git_commit`, `git_branch`, `previous_version` | Yes (if deployment exists) |
| **Deploy diff** | `git_commit` current vs `previous_version` → changed files list | Optional |
| **Recent commits** | Git log since last known-good deploy | Optional |
| **Historical fixes** | `fix_runs` WHERE `issue_id = ?` with outcomes (resolved/failed/regressed) | Yes |
| **Approval state** | `PolicyGate` config: AutoApply / RequireReview / DryRun | Yes |
| **Handoff state** | `agent_handoffs` row if resuming from a previous investigation | If resuming |
| **Artifacts** | `artifacts` table: related code snippets, logs, previous analysis outputs | Optional |
| **Breadcrumbs** | `error_breadcrumbs`: contextual events preceding the error | Optional |

Data flow:

```text
issue_id
  → IssueContextBuilder.BuildAsync()
    → queries error_issues, error_issue_events, spans, deployments, fix_runs, artifacts
    → assembles InvestigationContext record
      → passed to each pipeline stage as immutable input
```

### 2.3 Prompts

`AutofixPrompts` and `LoomPrompts` define system and user prompts for each pipeline stage.

## 3. Policy Gate

`PolicyGate` enforces approval policy per autofix run:

- **AutoApply** — fix applied automatically if confidence > threshold
- **RequireReview** — fix queued for human review
- **DryRun** — fix generated but not applied

Endpoints: `/approve` and `/reject` for human review workflow.

## 4. Autofix

`AutofixAgentService` — orchestrates the 5-stage pipeline.
`AutofixEndpoints` — REST API for triggering and monitoring autofix runs.
`AutofixOrchestrator` — coordinates agent handoffs between pipeline stages (in collector).

`PrCreationService` — creates GitHub PRs from generated diffs.

## 5. Code Review

`CodeReviewService` + `CodeReviewPrompt` — AI-powered code review.
`CodeReviewEndpoints` — REST API for triggering reviews.

Reviews incoming PRs and produces structured feedback with severity, location, and suggested fixes.

## 6. Regression

`RegressionDetectionService` — detects performance and error regressions.
`RegressionEndpoints` — REST API for regression queries.

Compares distributions across time windows using statistical methods.

### 6.1 Analytics

`AnomalyService` + `AnomalyEndpoints` — anomaly detection.
`DistributionComparer` — compares metric distributions.
`StatisticalMath` — statistical primitives (z-scores, percentiles, etc.).

## 7. Triage

`TriagePipelineService` — automated issue triage.

Classifies incoming issues by severity, assigns to teams, and determines if autofix is appropriate.

## 8. Identity

`GitHubService` + `GitHubEndpoints` — GitHub integration.
`GitHubWebhookEndpoints` — webhook receiver for PR/issue events.
`WorkspaceService` — workspace/project management.
`IssueAnalyticsEndpoints` — issue analytics queries.

## 9. Constraints

Ownership boundaries: see `specs/00-architecture.md` section 2.3.

- Agent construction uses MAF directly (`AIAgent`, `IChatClient`, `AddAIAgent()`).
- Each pipeline stage persisted independently for resumability.
- `LoomEndpoints`, `LoomExplorerService`, `LoomInsightService` are the primary query surfaces.
- `LoomSettingsEndpoints` manages Loom configuration.
- Shared types in `qyl.contracts/Loom/` — NOT in collector or Loom-local namespaces.

## 10. Definition of Done

- [ ] 5-stage pipeline executes end-to-end with AIAgent
- [ ] Each stage persisted as AutofixStepRecord in DuckDB
- [ ] PolicyGate enforces AutoApply/RequireReview/DryRun correctly
- [ ] PR creation works via GitHub API
- [ ] Code review produces structured feedback with location and severity
- [ ] Regression detection compares distributions with statistical significance
- [ ] Triage pipeline classifies and routes issues automatically
- [ ] No raw IChatClient usage anywhere in Loom
- [ ] Standalone project compiles and runs independently
