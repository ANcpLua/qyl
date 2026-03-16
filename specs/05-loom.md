# Loom Specification

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

`src/qyl.loom/` — standalone product. Also exists as its own project at `~/RiderProjects/qyl.loom/`.

References collector, agents, workflows, contracts, and instrumentation via ProjectReference. This is by design. Do NOT decompose, delete, or merge into collector.

Reference architecture: `docs/reference/seer-knowledge-base.md` and `docs/loom-design.md`.

All agent calls use AIAgent via QylAgentBuilder. Never raw IChatClient.

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

### 2.2 Prompts

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

- Loom is standalone. Do NOT merge into collector.
- Collector MUST NOT depend on Loom (dependency flows loom → collector).
- All LLM calls via AIAgent + QylAgentBuilder. Never raw IChatClient.
- Each pipeline stage persisted independently for resumability.
- `LoomEndpoints`, `LoomExplorerService`, `LoomInsightService` are the primary query surfaces.
- `LoomSettingsEndpoints` manages Loom configuration.

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
