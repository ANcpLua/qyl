# Loom Specification

> Owner: loom
> SSOT: YES (AI investigation pipeline, autofix, triage, code review, regression detection)
> Depends on: `telemetry-intelligence.md` (pattern engine), `telemetry-data-model.md` (schema), `issue-fingerprinting.md` (error grouping)
> Used by: `mcp.md` (analysis tool delegation)

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

`src/qyl.loom/` — standalone product. Communicates with collector over HTTP.

Seer API surface mapping: `src/qyl.loom/impl/seer/`

Agent construction uses MAF directly: `AIAgent`, `IChatClient`, `AddAIAgent()`. Shared types (`CodingAgentProvider`, `CodingAgentRunRecord`, `LoomSettingsRecord`) live in `qyl.contracts/Loom/`.

### 1.1 Collector vs Loom

| | Collector | Loom |
|---|---|---|
| **Role** | Data plane — ingest, store, serve, compute | Intelligence plane — autonomous AI agent |
| **Communication** | Serves REST API | Consumes collector REST API over HTTP |
| **LLM** | Optional `IChatClient?` for heuristic+LLM triage/autofix. Self-disables without LLM. | Required. Multi-step reasoning, provider SDKs, agent orchestration. |
| **Deployment** | Running (Railway) | Standalone process, separate deployment |

Loom enhances collector — it doesn't replace it. Collector's pipelines work with heuristic fallbacks. Loom adds autonomous reasoning on top. Seer API surface mapping at `src/qyl.loom/impl/seer/`.

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

Ownership boundaries: see `00-architecture.md` section 2.3.

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
