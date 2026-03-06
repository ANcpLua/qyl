# Loom Design Specification

**Reverse-Engineered from Public Sources**

| Field | Value |
|-------|-------|
| Subject | Sentry Loom AI Debugging Agent |
| Version | 1.0 (March 2026) |
| Method | Requirements engineering on open-source breadcrumbs |
| Sources | `getsentry/sentry` PRs, issues, code; `docs.sentry.io`; Sentry blog; API surface |

---

## Confidence Classification

Every claim in this document carries a confidence tag. The taxonomy mirrors requirement maturity levels used in [Requirements-Engineering-NASA-cFE](https://github.com/ANcpLua/Requirements-Engineering-NASA-cFE):

| Tag | Meaning | Equivalent |
|-----|---------|------------|
| **CONFIRMED** | Documented in public docs + visible in open-source code | SSRx verbatim from NASA SRS |
| **INFERRED** | Evidence in merged PRs/issues, high confidence but not officially documented | Manual attempt — derived from primary material |
| **EXPERIMENTAL** | Behind feature flags, open PRs, or explicitly marked preview/beta | Baseline AI output — plausible but unverified |
| **CLOSED-SOURCE** | Known to exist in `getsentry` (closed repo), cannot inspect implementation | SSR6 incomplete — acknowledged gap |
| **DEPRECATED** | Feature flag removed, code deleted, or superseded | Removed requirement |

---

## qyl Scope Labels

For qyl planning and implementation tracking, use this scope taxonomy in addition
to the confidence tags above:

| Label | Meaning |
|---|---|
| `IMPLEMENTED-IN-QYL` | Capability exists in this repository and is testable locally. |
| `CONTEXT-ONLY` | Comparative/reference information; useful context, not a local ship gate. |
| `EXTERNAL-CLOSED` | Known unknowns in external closed-source systems. |
| `NOT-PLANNED` | Explicitly excluded from qyl architecture and roadmap. |

Closed-source Sentry internals remain non-inspectable, but qyl implements its own
open Loom-like backend surface (triage, autofix, code review, handoff, regression,
webhook ingestion, dashboard, and MCP tooling).

### Implementation Evidence in qyl

| Capability | Status | Evidence |
|---|---|---|
| Endpoint families for Loom-like workflows | `IMPLEMENTED-IN-QYL` | `Program.cs` maps `MapAutofixEndpoints`, `MapRegressionEndpoints`, `MapAgentHandoffEndpoints`, `MapCodeReviewEndpoints`, `MapGitHubWebhookEndpoints`, `MapLoomSettingsEndpoints`, `MapTriageEndpoints`. Endpoint registration exists, but registration alone is not treated as proof of runtime correctness. |
| Autofix pipeline | `IMPLEMENTED-IN-QYL` | `AutofixAgentService.cs`, `AutofixOrchestrator.cs`, `AutofixEndpoints.cs`, `DuckDbStore.Autofix*.cs`. Current document status: implementation present, end-to-end verification still required. |
| Fixability scoring and triage | `IMPLEMENTED-IN-QYL` | `TriagePipelineService.cs`, `TriagePrompts.cs`, `TriageEndpoints.cs`, `DuckDbSchema.Triage.cs`. Current document status: implementation present, real-issue scoring verification still required. |
| Code review endpoints and service | `IMPLEMENTED-IN-QYL` | `CodeReviewEndpoints.cs`, `CodeReviewService.cs`, `CodeReviewPrompt.cs`. Current document status: implementation present, webhook-to-comment flow verification still required. |
| GitHub webhook ingestion + signature validation | `IMPLEMENTED-IN-QYL` | `GitHubWebhookEndpoints.cs` (`X-Hub-Signature-256`, `HMACSHA256`). Current document status: implementation present, signed payload verification still required. |
| Agent handoff lifecycle | `IMPLEMENTED-IN-QYL` | `AgentHandoffEndpoints.cs`, `AgentHandoffService.cs`, `DuckDbStore.Handoff.cs`. Current document status: implementation present, lifecycle transition verification still required. |
| Regression detection and querying | `IMPLEMENTED-IN-QYL` | `RegressionDetectionService.cs`, `RegressionEndpoints.cs`, `DuckDbStore.Regressions.cs`. Current document status: implementation present, detection/query verification still required. |
| Dashboard/UI for Loom flows | `IMPLEMENTED-IN-QYL` | `LoomDashboardPage.tsx`, `IssueTriagePage.tsx`, `IssueFixRunsPage.tsx`, `CodeReviewPage.tsx`. Current document status: implementation present, Playwright validation still required. |
| MCP tooling for Loom flows | `IMPLEMENTED-IN-QYL` | `AutofixMcpTools.cs`, `TriageTools.cs`, `RegressionTools.cs`, `GitHubMcpTools.cs`, `AgentHandoffTools.cs`, `AssistedQueryTools.cs`, `TestGenerationTools.cs`. Current document status: tool registration exists, protocol-level invocation verification still required. |

### qyl Verification Matrix

This table is the current ship bar for qyl. A capability is not considered verified until the full flow runs end-to-end with real inputs and real outputs.

| Capability | qyl Status | Verification Still Required |
|---|---|---|
| Autofix pipeline | `IMPLEMENTED-IN-QYL` | Run full issue -> root cause -> solution -> code change flow; confirm outputs are real and persisted correctly. |
| Triage/fixability scoring | `IMPLEMENTED-IN-QYL` | Execute scoring against real issues and validate thresholds, persistence, and API shape. |
| Code review | `IMPLEMENTED-IN-QYL` | Exercise GitHub webhook ingestion through PR analysis and confirm comment/status output. |
| GitHub webhook ingestion | `IMPLEMENTED-IN-QYL` | Validate HMAC-SHA256 signature handling and event routing with signed payloads. |
| Agent handoff | `IMPLEMENTED-IN-QYL` | Verify lifecycle transitions, state storage, and recovery behavior. |
| Regression detection | `IMPLEMENTED-IN-QYL` | Verify detection, storage, and query layer with real time-series inputs. |
| Dashboard UI | `IMPLEMENTED-IN-QYL` | Cover with Playwright after Phase 1 frontend stabilization. |
| MCP tooling | `IMPLEMENTED-IN-QYL` | Call tools through the MCP protocol and confirm real responses, not just registration. |

### 2026 Web Source Synthesis

The six extracted 2026 web markdown files collapse into two useful source groups: one primary product announcement cluster and one secondary explainer. The partner translation and press/news reposts do not add net-new product behavior beyond the primary Sentry post, but they do reinforce positioning, pricing, and operating assumptions.

#### Primary product signal: Seer expanded across the full development loop

Scope label for qyl: `CONTEXT-ONLY`

Deduplicated across:
- Sentry blog primary announcement
- Ichizoku Japanese translation mirror
- TechIntelPro secondary coverage
- AI-Tech Park / Business Wire syndication
- Sentry changelog page, which was only partially extractable in this corpus

Consolidated product claims:
- Seer is explicitly positioned as a shift-left system spanning local development, code review, and production rather than a production-only debugger.
- Runtime telemetry is the differentiator. The consistent message across sources is that traces, logs, metrics, errors, and related context are required to catch failures that static code inspection misses.
- Local development workflow depends on the Sentry MCP server as the bridge between a coding agent and runtime telemetry during reproduction of a bug.
- Code review focuses on production-risking defects, not style or low-signal lint-like feedback.
- Production flow remains root-cause-first, with optional code generation or delegation to an external coding agent when confidence is high.
- Open-ended investigation over telemetry is real but still preview-stage, so it should not be treated as a hard ship requirement for qyl.
- Pricing converged in January 2026 to `$40` per active contributor per month with unlimited usage, where an active contributor is defined by `2+` pull requests in a connected repository during the billing month.

qyl implication:
- The local product target is not "AI everywhere" in the abstract. The target is a telemetry-grounded debugging loop that proves value in three concrete surfaces: local triage/autofix, pre-merge review, and post-merge or production investigation.

#### Secondary product signal: Seer as debugging copilot, with privacy constraints

Scope label for qyl: `CONTEXT-ONLY`

Deduplicated across:
- Oreate AI third-party explainer

Consolidated product claims:
- Seer is framed as a copilot that operates on rich debugging context rather than as a generic assistant.
- Multi-repository and distributed-system reasoning are part of the public story, even if implementation details are not fully public.
- Privacy messaging matters: customer data and source code are not used for model training without explicit consent, and generated output is scoped to authorized users.

qyl implication:
- qyl should prioritize strong operator trust boundaries: explicit data movement, auditable tool actions, and clear separation between analysis context and code mutation.

---

## Table of Contents

1. [Glossary](#1-glossary)
2. [System Overview](#2-system-overview)
3. [Service Topology](#3-service-topology)
4. [Capabilities Catalog](#4-capabilities-catalog)
   - 4.1 [Issue Grouping & Similarity (ML)](#41-issue-grouping--similarity-ml)
   - 4.2 [Issue Summarization](#42-issue-summarization)
   - 4.3 [Fixability Scoring](#43-fixability-scoring)
   - 4.4 [Root Cause Analysis](#44-root-cause-analysis)
   - 4.5 [Autofix Pipeline](#45-autofix-pipeline)
   - 4.6 [AI Code Review (Loom Prevent)](#46-ai-code-review-Loom-prevent)
   - 4.7 [Explorer (Interactive Agent)](#47-explorer-interactive-agent)
   - 4.8 [Anomaly Detection](#48-anomaly-detection)
   - 4.9 [Trace Summarization](#49-trace-summarization)
   - 4.10 [Assisted Query / Search Agent](#410-assisted-query--search-agent)
   - 4.11 [Test Generation](#411-test-generation)
5. [Coding Agent Providers](#5-coding-agent-providers)
6. [API Surface](#6-api-surface)
   - 6.1 [Public REST API](#61-public-rest-api)
   - 6.2 [Loom Service Endpoints (Sentry -> Loom)](#62-Loom-service-endpoints-sentry---Loom)
   - 6.3 [RPC Bridge (Loom -> Sentry)](#63-rpc-bridge-Loom---sentry)
   - 6.4 [MCP Server](#64-mcp-server)
7. [Data Layer](#7-data-layer)
8. [Configuration & Feature Flags](#8-configuration--feature-flags)
9. [Integration Points](#9-integration-points)
10. [Data Privacy & Security](#10-data-privacy--security)
11. [Pricing](#11-pricing)
12. [Limitations & Known Gaps](#12-limitations--known-gaps)
13. [Experimental & Preview Features](#13-experimental--preview-features)
14. [Source Traceability](#14-source-traceability)

---

## 1. Glossary

| Term | Definition |
|------|-----------|
| **Loom** | Sentry's AI debugging agent. Umbrella name for all ML/AI features in the platform. |
| **Loom Prevent** | Internal codename for the AI Code Review subsystem. |
| **Autofix** | The automated issue-fixing pipeline (root cause -> solution -> code changes -> PR). |
| **Explorer** | The interactive agentic debugging interface (formerly the `Cmd+/` chat). |
| **Fixability Score** | A 0.0-1.0 score predicting whether an issue can be automatically fixed. GPU-computed. |
| **Supergroup** | Cross-issue semantic grouping triggered from Explorer RCA results. |
| **GroupHashMetadata** | Sentry model storing per-hash Loom state (embedding date, model version, match distance). |
| **LoomOperator** | Internal orchestrator connecting entrypoints (Slack, web) to Loom lifecycle events. |
| **Triage Signals** | The automation pipeline that decides whether to scan, summarize, and autofix an issue. |
| **Coding Agent** | External service that receives Loom's analysis and generates code changes (Loom built-in, Cursor, GitHub Copilot, Claude Code). |
| **HMAC-SHA256** | Authentication scheme used for all Sentry <-> Loom HTTP communication. |
| **pgvector** | PostgreSQL extension used for embedding storage and HNSW nearest-neighbor search. |
| **HNSW** | Hierarchical Navigable Small World — approximate nearest neighbor index algorithm. |
| **Flagpole** | Sentry's feature flag evaluation system. |

---

## 2. System Overview

**CONFIRMED**

Loom is a **separate service** from the Sentry monolith. It runs as multiple specialized microservices (pods), each handling a different ML/AI workload. The Sentry Django application acts as the frontend, data provider, and orchestrator, while Loom services perform the actual inference.

```
┌─────────────────────────────────────────────────────────┐
│                    User Interfaces                       │
│  Web UI  │  Slack (@mention)  │  MCP Server  │  API     │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│              Sentry Monolith (Django)                     │
│                                                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │ Endpoints │  │  Celery   │  │  Webhook  │  │   RPC   │ │
│  │  (REST)   │  │  Tasks    │  │ Handlers  │  │ Bridge  │ │
│  └─────┬─────┘  └─────┬────┘  └─────┬─────┘  └────▲────┘ │
│        │              │              │              │      │
│        └──────────────┴──────────────┘              │      │
│                       │ HMAC-signed HTTP             │      │
└───────────────────────┼──────────────────────────────┼──────┘
                        │                              │
                        ▼                              │
┌───────────────────────────────────────────────────────┐
│                 Loom Service Cluster                   │
│                                                        │
│  ┌──────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Autofix  │  │Summarization │  │Anomaly Detection│  │
│  │   Pod    │  │     Pod      │  │      Pod        │  │
│  └──────────┘  └──────────────┘  └─────────────────┘  │
│  ┌──────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Grouping │  │  Breakpoint  │  │  Scoring (GPU)  │  │
│  │   Pod    │  │     Pod      │  │      Pod        │  │
│  └──────────┘  └──────────────┘  └─────────────────┘  │
│  ┌──────────┐                                          │
│  │  Code    │  Runtime: Python 3.11, gRPC + Gunicorn   │
│  │  Review  │  Queue: Celery + RabbitMQ                │
│  │   Pod    │  Storage: PostgreSQL + pgvector           │
│  └──────────┘  Models: GCS gs://sentry-ml/Loom/models  │
│                Observability: Langfuse                  │
└───────────────────────────────────────────────────────┘
```

### Communication Pattern

**CONFIRMED** — All Sentry-to-Loom calls use `make_signed_Loom_api_request()` with:
- HMAC-SHA256 signing via `SEER_API_SHARED_SECRET`
- `Authorization: Rpcsignature rpc0:{signature}` header
- Optional `X-Viewer-Context` + `X-Viewer-Context-Signature` for audit trails
- urllib3 connection pooling (migrated from `requests.post` in Feb-Mar 2026)

**CONFIRMED** — Loom-to-Sentry callbacks use `OrganizationLoomRpcEndpoint` at `/api/0/internal/Loom-rpc/` with `SEER_RPC_SHARED_SECRET`.

---

## 3. Service Topology

**INFERRED** from connection pool configuration and URL settings.

| Service | Setting | Connection Pool | Purpose |
|---------|---------|----------------|---------|
| Autofix/Explorer | `SEER_AUTOFIX_URL` | `Loom_autofix_default_connection_pool` | Autofix, explorer, code review routing, project preferences, models, assisted query |
| Summarization | `SEER_SUMMARIZATION_URL` | `Loom_summarization_default_connection_pool` | Issue + trace summaries |
| Anomaly Detection | `SEER_ANOMALY_DETECTION_URL` | `Loom_anomaly_detection_default_connection_pool` | Time-series anomaly detection |
| Grouping | `SEER_GROUPING_URL` | `Loom_grouping_connection_pool` | Embedding-based similar issue detection |
| Breakpoint Detection | `SEER_BREAKPOINT_DETECTION_URL` | `Loom_breakpoint_connection_pool` | Performance regression detection |
| Scoring (GPU) | `SEER_SCORING_URL` | `fixability_connection_pool_gpu` | Fixability scoring |
| Code Review | `SEER_PREVENT_AI_URL` | `Loom_code_review_connection_pool` | AI code review (Loom Prevent) |

**INFERRED** — At minimum 7 distinct URL configurations, suggesting either 7 separate deployments or route-based load balancing across fewer physical services.

---

## 4. Capabilities Catalog

### 4.1 Issue Grouping & Similarity (ML)

**CONFIRMED**

When a new event arrives that does not match existing groups by hash, Sentry calls Loom to check if the stacktrace is semantically similar to existing groups.

| Property | Value | Confidence |
|----------|-------|------------|
| Embedding model | `jinaai/jina-embeddings-v2-base-en` | CONFIRMED (PR #99873) |
| Vector dimensions | 768 | CONFIRMED (blog) |
| Storage | PostgreSQL + pgvector (HNSW index) | CONFIRMED (blog) |
| Latency | Sub-100ms end-to-end | CONFIRMED (blog) |
| Impact | 40% reduction in new issues | CONFIRMED (blog) |
| False positive rate | Near-zero (conservative thresholds) | CONFIRMED (blog) |
| Quantization | Full float32 precision (float16/int8/binary evaluated, rejected) | CONFIRMED (blog) |
| Query strategy | Two-stage: permissive HNSW distance -> strict final distance | CONFIRMED (blog) |
| Model versioning | `SEER_SIMILARITY_MODEL_VERSION`, `GroupingVersion` enum | CONFIRMED (code) |
| V2 model | Rolling out behind `projects:similarity-grouping-v2-model` Flagpole flag | EXPERIMENTAL (PR #102263) |
| Training mode | `training_mode=True` sends embeddings without grouping decisions | INFERRED (PR #109539) |

**GroupHashMetadata fields** (CONFIRMED — code):

| Field | Purpose |
|-------|---------|
| `Loom_date_sent` | When the hash was sent to Loom |
| `Loom_event_sent` | Which event's stacktrace was sent |
| `Loom_model` | Model version used |
| `Loom_matched_grouphash` | FK to the matched group's hash |
| `Loom_match_distance` | Similarity distance score |
| `Loom_latest_training_model` | Tracks latest training-mode model version |

**Filtering criteria** (INFERRED):
- Event must have a stacktrace and a usable title
- Token count filtering replaced frame count filtering (PR #103997)
- Killswitch options: global Loom killswitch + similarity-specific killswitch

---

### 4.2 Issue Summarization

**CONFIRMED**

AI-generated summaries provide headlines, root cause hypotheses, and trace insights.

| Property | Value | Confidence |
|----------|-------|------------|
| Response schema | `headline`, `whats_wrong`, `trace`, `possible_cause`, `scores` | CONFIRMED (code) |
| Cache key | `ai-group-summary-v2:{group_id}` | CONFIRMED (code) |
| Cache TTL | 7 days | CONFIRMED (code) |
| Locking | Distributed lock during generation | CONFIRMED (code) |
| Automation trigger | Summary generation can trigger autofix based on fixability | CONFIRMED (code) |
| Service | Routed to dedicated summarization pod | INFERRED (PR #97926) |

---

### 4.3 Fixability Scoring

**CONFIRMED**

A GPU-backed service scores each issue on a 0.0-1.0 scale to determine automation behavior.

| Score Range | Label | Automation Action | Confidence |
|-------------|-------|-------------------|------------|
| < 0.25 | `SUPER_LOW` | No automation | CONFIRMED (code) |
| 0.25-0.40 | `LOW` | Depends on tuning setting | CONFIRMED (code) |
| 0.40-0.66 | `MEDIUM` | Root cause only | CONFIRMED (code) |
| 0.66-0.76 | `HIGH` | Code changes | CONFIRMED (code) |
| >= 0.78 | `SUPER_HIGH` + buffer | Open PR | CONFIRMED (code) |

---

### 4.4 Root Cause Analysis

**CONFIRMED**

The first stage of the autofix pipeline. Loom analyzes the issue, telemetry, and stacktraces to determine the root cause.

| Property | Value | Confidence |
|----------|-------|------------|
| Input data | Error messages, stack traces, distributed traces, structured logs, performance profiles, source code | CONFIRMED (docs) |
| Multi-repo | Can analyze across multiple linked GitHub repos | CONFIRMED (docs) |
| Streaming | Reasoning is streamed to user in real-time | CONFIRMED (docs) |
| User feedback | Thumbs up/down on RCA card, tracked via `Loom.autofix.feedback_submitted` | INFERRED (PR #104569) |
| Webhook event | `Loom.root_cause_started`, `Loom.root_cause_completed` | CONFIRMED (code) |

---

### 4.5 Autofix Pipeline

**CONFIRMED**

Multi-step automated issue fixing, from analysis through PR creation.

```
┌─────────────┐     ┌──────────┐     ┌──────────────┐     ┌───────────┐
│ Root Cause   │────>│ Solution │────>│ Code Changes │────>│ PR Create │
│  Analysis    │     │ Planning │     │  Generation  │     │  (GitHub) │
└─────────────┘     └──────────┘     └──────────────┘     └───────────┘
       │                  │                  │                    │
       ▼                  ▼                  ▼                    ▼
  Loom.root_cause_*  Loom.solution_*  Loom.coding_*       Loom.pr_created
```

**Automation tuning levels** (CONFIRMED — code):

| Level | Behavior |
|-------|----------|
| `off` | Disabled |
| `super_low` | Minimal automation |
| `low` | Conservative |
| `medium` | Default for seat-based pricing |
| `high` | Aggressive |
| `always` | Maximum automation |

**Stopping points** (CONFIRMED — code):

| Value | Behavior |
|-------|----------|
| `code_changes` | Stop after generating code (default) |
| `open_pr` | Automatically open a GitHub PR |

**Rate limits** (INFERRED):
- Scanner: `Loom.max_num_scanner_autotriggered_per_ten_seconds` (default 15)
- Autofix: `Loom.max_num_autofix_autotriggered_per_hour` (default 20, multiplied by tuning level)
- Issues older than 2 weeks are skipped for automation to prevent flood on enablement

**Billing** (CONFIRMED):
- `DataCategory.SEER_AUTOFIX` — billed per run
- `DataCategory.SEER_SCANNER` — billed per scan

**Dual-mode architecture** (INFERRED — code):
1. **Legacy mode** (default): Direct POST to Loom service
2. **Explorer mode** (`?mode=explorer`): Multi-step agentic pipeline via `LoomExplorerClient`

---

### 4.6 AI Code Review (Loom Prevent)

**CONFIRMED**

Pre-merge analysis triggered by GitHub webhooks.

| Property | Value | Confidence |
|----------|-------|------------|
| Trigger: PR opened | Yes (skips drafts) | CONFIRMED (docs) |
| Trigger: Draft -> ready | Yes | CONFIRMED (docs) |
| Trigger: New commit on ready PR | Yes | CONFIRMED (docs) |
| Trigger: `@sentry review` comment | Yes | CONFIRMED (docs) |
| Trigger: Check run re-request | Yes | INFERRED (code) |
| Data sent to AI | File names, code diffs, PR description only | CONFIRMED (docs) |
| Output | Inline PR comments + GitHub status check | CONFIRMED (docs) |
| Status check states | Success (green), Neutral (yellow), Error (red), Cancelled | CONFIRMED (docs) |
| Internal codename | Loom Prevent | INFERRED (code: `SEER_PREVENT_AI_URL`) |
| DB tracking | `CodeReviewRun` model: `task_enqueued` -> `Loom_request_sent` -> `succeeded/failed` | INFERRED (PR #108445) |
| Retention | Cleanup task purges rows older than 90 days | INFERRED (code) |
| A/B testing | `organizations:code-review-experiments-enabled` flag + per-PR hash-based assignment | EXPERIMENTAL (code) |
| SCM support | GitHub Cloud only | CONFIRMED (docs) |

**Metrics** (INFERRED — PR #105984):
- `sentry.Loom.code_review.webhook.received`
- `sentry.Loom.code_review.webhook.filtered` (with `reason` tag)
- `sentry.Loom.code_review.webhook.enqueued`
- `sentry.Loom.code_review.webhook.error` (with `error_type` tag)
- `sentry.Loom.code_review.task.e2e_latency`

---

### 4.7 Explorer (Interactive Agent)

**CONFIRMED** (feature-flagged)

The agentic investigation interface where users ask questions about errors, traces, and code.

| Property | Value | Confidence |
|----------|-------|------------|
| UI entry | `Cmd+/` in Sentry | CONFIRMED (docs) |
| Feature flag | `Loom-explorer` | CONFIRMED (code) |
| Can trigger autofix | Yes, from within Explorer context | INFERRED (PR #108389) |
| Screenshot support | Frontend can pass screenshots for visual context | INFERRED (PR #99744) |
| Replay DOM inspection | "Inspect Element" in replay viewer sends DOM to Explorer | EXPERIMENTAL (PR #108527) |
| Supergroup embeddings | After Explorer generates RCA, triggers supergroup embedding | EXPERIMENTAL (PR #107819) |
| Slack integration | `@mention` in Slack triggers Explorer runs | EXPERIMENTAL |

**Slack Integration Detail** (EXPERIMENTAL — multiple open PRs):
- `SlackMentionHandler` parses mentions, extracts prompts, builds thread context
- `SlackEntrypoint` and `LoomOperator` pattern for workflow orchestration
- Renders: Issue Alert, Root Cause, Proposed Solution, Code Changes, Pull Request
- Thinking face reaction for immediate acknowledgment
- Feature flag: `Loom-slack-explorer`

---

### 4.8 Anomaly Detection

**CONFIRMED**

Time-series anomaly detection for alert monitoring.

| Property | Value | Confidence |
|----------|-------|------------|
| Endpoint | `/v1/anomaly-detection/store` | CONFIRMED (code) |
| Pool | `Loom_anomaly_detection_default_connection_pool` | CONFIRMED (code) |
| Aggregate types | Counting vs. other alert types distinguished | INFERRED (PR #107649) |
| Retry logic | Retries on transient 503s | INFERRED (PRs #105542, #105854) |
| Timeout | Configurable via `SEER_ANOMALY_DETECTION_TIMEOUT` | CONFIRMED (code) |

---

### 4.9 Trace Summarization

**CONFIRMED**

AI-generated trace analysis.

| Property | Value | Confidence |
|----------|-------|------------|
| Endpoint | `/v1/automation/summarize/trace` | CONFIRMED (code) |
| Response | `trace_id`, `summary`, `key_observations`, `performance_characteristics`, `suggested_investigations` | CONFIRMED (code) |
| Cache key | `ai-trace-summary:{trace_slug}` | CONFIRMED (code) |
| Cache TTL | 7 days | CONFIRMED (code) |
| Feature flag | `organizations:single-trace-summary` | CONFIRMED (code) |
| Service | Routed to summarization pod | CONFIRMED (code) |

---

### 4.10 Assisted Query / Search Agent

**CONFIRMED**

Natural language to Sentry query translation.

| Property | Value | Confidence |
|----------|-------|------------|
| Translate endpoint | `/v1/assisted-query/translate` | CONFIRMED (code) |
| Agentic translate | `/v1/assisted-query/translate-agentic` | CONFIRMED (code) |
| Start agent | `/v1/assisted-query/start` | CONFIRMED (code) |
| Get state | `/v1/assisted-query/state` | CONFIRMED (code) |
| Cache | `/v1/assisted-query/create-cache` | CONFIRMED (code) |
| Tool types | Discover tools, Issues tools, Traces tools | CONFIRMED (code) |

---

### 4.11 Test Generation

**EXPERIMENTAL**

Unit test generation service.

| Property | Value | Confidence |
|----------|-------|------------|
| Endpoint | `/v1/automation/codegen/unit-tests` | CONFIRMED (code) |
| Module | `src/sentry/Loom/services/test_generation/` | CONFIRMED (code) |
| Status | Present in code, no public documentation | EXPERIMENTAL |

---

## 5. Coding Agent Providers

**CONFIRMED** — Loom supports pluggable coding agent backends via a base class hierarchy.

| Provider | Status | Feature Flag | Integration Method | Confidence |
|----------|--------|--------------|-------------------|------------|
| **Loom (built-in)** | Production | None (default) | Direct via autofix pipeline | CONFIRMED |
| **Cursor** | Production | Graduated (`Loom-coding-agent-integrations`) | Cursor Background Agent API | CONFIRMED (docs) |
| **GitHub Copilot** | Production | Graduated | Copilot Coding Agent Tasks API | INFERRED (PR #108565) |
| **Claude Code** | In Development | `organizations:integrations-claude-code` | Anthropic Claude Code agent API | EXPERIMENTAL (PRs #109526, #109738, #109750) |

**Base class hierarchy** (INFERRED — PR #109730):
```
CodingAgentIntegrationProvider   # Common provider config, setup dialog
  └─ CodingAgentPipelineView     # Pipeline view logic
  └─ CodingAgentIntegration      # Metadata persistence
```

**Organization settings** (CONFIRMED — code):
- `LoomOrganizationSettings.default_coding_agent` — `"Loom"`, `"cursor"`, or `null`
- `LoomOrganizationSettings.default_coding_agent_integration_id` — FK to Integration

---

## 6. API Surface

### 6.1 Public REST API

**CONFIRMED** — These endpoints are documented and user-facing.

| Endpoint | Method | Purpose | Rate Limit |
|----------|--------|---------|------------|
| `/api/0/Loom/models/` | GET | List active LLM model names | — |
| `/api/0/issues/{issue_id}/autofix/` | POST | Start autofix run | 25/min user, 100/hr org |
| `/api/0/issues/{issue_id}/autofix/` | GET | Get autofix state | 1024/min user |
| `/api/0/issues/{issue_id}/autofix/setup/` | GET | Check autofix prerequisites | — |
| `/api/0/issues/{issue_id}/autofix/update/` | POST | Send update to running autofix | — |
| `/api/0/issues/{issue_id}/ai-summary/` | POST | Generate AI issue summary | — |
| `/api/0/organizations/{org}/Loom/setup-check/` | GET | Check Loom quota/billing | — |
| `/api/0/organizations/{org}/Loom/onboarding-check/` | GET | Onboarding status | — |
| `/api/0/organizations/{org}/autofix-automation-settings/` | GET/PUT | Org automation settings | — |
| `/api/0/organizations/{org}/trace-summary/` | POST | AI trace summarization | — |
| `/api/0/organizations/{org}/Loom-explorer/chat/` | POST | Explorer chat | — |
| `/api/0/organizations/{org}/Loom-explorer/runs/` | GET | List Explorer runs | — |
| `/api/0/organizations/{org}/Loom-explorer/update/` | POST | Update Explorer run | — |
| `/api/0/projects/{org}/{project}/Loom-preferences/` | GET/PUT | Project Loom preferences | — |
| `/api/0/organizations/{org}/search-agent/start/` | POST | Start assisted search | — |
| `/api/0/organizations/{org}/search-agent/state/` | GET | Get search agent state | — |

### 6.2 Loom Service Endpoints (Sentry -> Loom)

**CONFIRMED** — These are the HTTP paths Sentry calls on the Loom service, extracted from code.

| Path | Pool | Purpose |
|------|------|---------|
| `/v1/automation/autofix/start` | Autofix | Start an autofix run |
| `/v1/automation/autofix/update` | Autofix | Update (select root cause, solution, create PR) |
| `/v1/automation/autofix/state` | Autofix | Get autofix state by group_id or run_id |
| `/v1/automation/autofix/state/pr` | Autofix | Get autofix state by PR ID |
| `/v1/automation/autofix/prompt` | Autofix | Get autofix prompt for coding agent handoff |
| `/v1/automation/autofix/coding-agent/state/update` | Autofix | Update coding agent state |
| `/v1/automation/autofix/coding-agent/state/set` | Autofix | Store coding agent states |
| `/v1/automation/summarize/trace` | Summarization | Summarize a trace |
| `/v1/automation/summarize/issue` | Summarization | Summarize an issue |
| `/v1/automation/summarize/fixability` | Scoring (GPU) | Generate fixability score |
| `/v1/automation/explorer/index` | Autofix | Index explorer data |
| `/v1/automation/explorer/index/org-project-knowledge` | Autofix | Index org project knowledge |
| `/v1/automation/codegen/unit-tests` | Autofix | Generate unit tests |
| `/v1/automation/codegen/pr-review/rerun` | Code Review | Rerun PR review |
| `/v1/automation/overwatch-request` | Code Review | Code review request |
| `/v1/project-preference` | Autofix | Get project preferences |
| `/v1/project-preference/set` | Autofix | Set project preferences |
| `/v1/project-preference/bulk` | Autofix | Bulk get preferences |
| `/v1/project-preference/bulk-set` | Autofix | Bulk set preferences |
| `/v1/project-preference/remove-repository` | Autofix | Remove repository |
| `/v1/models` | Autofix | List available models |
| `/v1/llm/generate` | Autofix | Direct LLM generation |
| `/v1/assisted-query/translate` | Autofix | Translate NL to query |
| `/v1/assisted-query/start` | Autofix | Start search agent |
| `/v1/assisted-query/state` | Autofix | Get search agent state |
| `/v1/assisted-query/translate-agentic` | Autofix | Agentic query translation |
| `/v1/assisted-query/create-cache` | Autofix | Create query cache |
| `/v1/explorer/service-map/update` | Autofix | Update service map |
| `/v0/issues/supergroups` | Autofix | Supergroup embeddings |
| `/v1/workflows/compare/cohort` | Anomaly Detection | Compare distributions |

### 6.3 RPC Bridge (Loom -> Sentry)

**CONFIRMED** — `OrganizationLoomRpcEndpoint` at `/api/0/internal/Loom-rpc/` is HMAC-authenticated.

The RPC bridge exposes dozens of functions for the Loom service to call back into Sentry, including:

| RPC Function Category | Confidence |
|----------------------|------------|
| Event/span/trace data access (via Snuba protos) | CONFIRMED |
| Organization and project metadata lookup | CONFIRMED |
| Issue occurrence creation (LLM-detected issues) | INFERRED |
| Repository and code mapping information | CONFIRMED |
| Attribute distribution queries | INFERRED |
| Log and metric queries | INFERRED |
| Profile data retrieval | INFERRED |
| Webhook broadcasting to Sentry Apps | CONFIRMED |
| Feature flag checks | CONFIRMED |

### 6.4 MCP Server

**CONFIRMED** — Separately maintained at `github.com/getsentry/sentry-mcp`.

| Property | Value |
|----------|-------|
| URL | `https://mcp.sentry.dev/mcp` (hosted, no install required) |
| Auth | OAuth 2.0 browser-based flow |
| Self-hosted | `npx @sentry/mcp-server@latest --access-token=TOKEN` (STDIO) |
| Tool count | 16+ tools across Core, Analysis, Advanced categories |
| Loom integration | Can trigger Loom analysis, get fix recommendations, monitor fix status |
| Disable Loom | `MCP_DISABLE_SKILLS=Loom` |
| Status | "Released for production, however MCP is a developing technology" |

**Note**: No MCP integration exists *inside* the `getsentry/sentry` codebase. The MCP server is a separate project that calls Sentry's public API.

---

## 7. Data Layer

### Django Models

**CONFIRMED** — Code-visible models.

**`LoomOrganizationSettings`** (`src/sentry/Loom/models/organization_settings.py`):

| Field | Type | Purpose |
|-------|------|---------|
| `organization` | FK (unique, indexed) | Owning organization |
| `default_coding_agent` | CharField | `"Loom"`, `"cursor"`, or null |
| `default_coding_agent_integration_id` | HybridCloudFK | FK to Integration |

**`GroupHashMetadata`** (extended by Loom — see Section 4.1 for fields)

**`CodeReviewRun`** (INFERRED — PR #108445):
- Tracks lifecycle: `task_enqueued` -> `Loom_request_sent` -> `Loom_request_succeeded/failed`

**`CodeReviewEvent`** (INFERRED — PR #108533):
- Rows for the internal PR Review Dashboard

### Migrations

| Migration | Purpose |
|-----------|---------|
| `0001_add_Loomorganizationsettings.py` | Creates the settings table |
| `0002_add_default_coding_agent.py` | Adds coding agent fields |

### Caching Strategy

**CONFIRMED** — Code-visible cache keys.

| Cache Key Pattern | TTL | Purpose |
|-------------------|-----|---------|
| `ai-group-summary-v2:{group_id}` | 7 days | Issue summaries |
| `ai-trace-summary:{trace_slug}` | 7 days | Trace summaries |
| `Loom-project-has-repos:{org_id}:{project_id}` | 15 min | Repo status |
| `Loom:seat-based-tier:{org_id}` | 4 hours | Pricing tier |
| `autofix_access_check:{group_id}` | 1 min | Access check |
| `LoomOperatorAutofixCache` (by group_id/run_id) | — | Operator state |

### External Storage

**INFERRED** — From the Loom service repository mirror:

| Storage | Purpose |
|---------|---------|
| PostgreSQL + pgvector | Embedding storage, HNSW index for similarity search |
| Google Cloud Storage (`gs://sentry-ml/Loom/models`) | ML model artifacts |
| Langfuse | AI operation tracing/observability |

---

## 8. Configuration & Feature Flags

### Django Settings

**CONFIRMED** — Code-visible settings.

| Setting | Purpose |
|---------|---------|
| `SEER_AUTOFIX_URL` | Base URL: autofix, explorer, code review routing, preferences, models |
| `SEER_SUMMARIZATION_URL` | Base URL: issue + trace summaries |
| `SEER_ANOMALY_DETECTION_URL` | Base URL: anomaly detection |
| `SEER_GROUPING_URL` | Base URL: similarity grouping |
| `SEER_BREAKPOINT_DETECTION_URL` | Base URL: performance regression |
| `SEER_SCORING_URL` | Base URL: GPU fixability scoring |
| `SEER_PREVENT_AI_URL` | Base URL: code review (Loom Prevent) |
| `SEER_API_SHARED_SECRET` | HMAC secret for Sentry -> Loom |
| `SEER_RPC_SHARED_SECRET` | HMAC secret for Loom -> Sentry |
| `SEER_AUTOFIX_GITHUB_APP_USER_ID` | Loom's GitHub App user ID |
| `SEER_MAX_GROUPING_DISTANCE` | Max distance threshold for similarity |
| `SEER_ANOMALY_DETECTION_TIMEOUT` | Timeout for anomaly detection |
| `SEER_BREAKPOINT_DETECTION_TIMEOUT` | Timeout for breakpoint detection |
| `SEER_FIXABILITY_TIMEOUT` | Timeout for fixability scoring |
| `CLAUDE_CODE_CLIENT_CLASS` | Dynamic client loading for Claude Code agent |

### Feature Flags

**Tags**: `A` = Active, `E` = Experimental, `G` = Graduated (100%), `D` = Deprecated/Dead

| Flag | Scope | Purpose | Tag |
|------|-------|---------|-----|
| `organizations:gen-ai-features` | Org | **Master gate** for all Loom AI features | A |
| `organizations:Loom-explorer` | Org | Explorer agent UI | A |
| `organizations:autofix-on-explorer` | Org | Route autofix through Explorer pipeline | A |
| `organizations:seat-based-Loom-enabled` | Org | Seat-based billing tier | A |
| `organizations:single-trace-summary` | Org | Trace summarization | A |
| `organizations:code-review-experiments-enabled` | Org | A/B testing for code review | A |
| `organizations:integrations-claude-code` | Org | Claude Code coding agent | E |
| `organizations:Loom-slack-workflows-explorer` | Org | Slack workflows on Explorer | E |
| `projects:similarity-grouping-v2-model` | Project | V2 grouping model rollout | E |
| `projects:supergroup-embeddings-explorer` | Project | Supergroup embeddings via Explorer | E |
| `Loom-explorer` | System | Explorer feature | A |
| `Loom-slack-explorer` | System | Slack @mention Explorer | E |
| `pr-review-dashboard` | System | Internal PR review dashboard | E |
| `triage-signals-v0-org` | System | New automation flow | A |
| `Loom-agent-pr-consolidation` | System | Consolidated PR toggle UI | A |
| `organizations:Loom-webhooks` | Org | Webhook subscriptions | G |
| `organizations:autofix-Loom-preferences` | Org | Project preferences | G |
| `organizations:Loom-coding-agent-integrations` | Org | External coding agents | G |
| `organizations:unlimited-auto-triggered-autofix-runs` | Org | Bypass rate limiting | D |

### Organization Options

| Option | Type | Purpose | Confidence |
|--------|------|---------|------------|
| `sentry:hide_ai_features` | bool | Kill switch for all AI features | CONFIRMED |
| `sentry:enable_Loom_coding` | bool | Enable/disable code changes step | CONFIRMED |
| `sentry:default_autofix_automation_tuning` | str | Default automation tuning for org | INFERRED |
| `sentry:auto_open_prs` | bool | Default PR creation for new projects | INFERRED |
| `sentry:default_automation_handoff` | str | Default coding agent for new projects | INFERRED |

### Project Options

| Option | Type | Purpose | Confidence |
|--------|------|---------|------------|
| `sentry:Loom_scanner_automation` | bool | Enable Loom scanner for project | CONFIRMED |
| `sentry:autofix_automation_tuning` | str | Tuning: off/super_low/low/medium/high/always | CONFIRMED |

### Rate Limit Options

| Option | Default | Confidence |
|--------|---------|------------|
| `Loom.max_num_scanner_autotriggered_per_ten_seconds` | 15 | INFERRED |
| `Loom.max_num_autofix_autotriggered_per_hour` | 20 (x tuning multiplier) | INFERRED |
| `Loom.similarity.circuit-breaker-config` | — | CONFIRMED |
| `Loom.similarity.grouping-ingest-retries` | — | CONFIRMED |
| `Loom.similarity.grouping-ingest-timeout` | — | CONFIRMED |

---

## 9. Integration Points

### GitHub

**CONFIRMED**

| Integration | Method | Details |
|-------------|--------|---------|
| Source code access | Contents (Read & Write) | Fetch files, commits, blame data |
| PR creation | Loom GitHub App (`Loom-by-sentry`) | Separate from main Sentry GitHub App |
| Code review | Webhooks (pull_request, issue_comment, check_run) | Posts inline comments + status checks |
| PR tracking | Webhook handler (`autofix/webhooks.py`) | Tracks opened/closed/merged analytics |
| Copilot handoff | Copilot Coding Agent Tasks API | Polls task status |
| SCM limitation | **GitHub Cloud only** | No GitLab, Bitbucket, Azure DevOps |
| SCM abstraction | **In progress** (issue #107469) | Unified SCM platform, GitLab explored |

**Required GitHub Permissions** (CONFIRMED — docs):

| Permission | Level | Purpose |
|-----------|-------|---------|
| Administration | Read-only | Branch protection, default branches |
| Checks | Read & Write | Status checks, code review results |
| Commit Statuses | Read & Write | Status integration |
| Contents | Read & Write | Source files, commits, blame |
| Issues | Read & Write | Linked issue operations |
| Members | Read-only (Org) | User mapping |
| Pull Requests | Read & Write | PR comments, code review |
| Webhooks | Read & Write | Event subscriptions |

### Slack

**CONFIRMED** (expanding to EXPERIMENTAL)

| Capability | Status | Confidence |
|-----------|--------|------------|
| Autofix notifications | Production | CONFIRMED |
| Explorer @mention | In development | EXPERIMENTAL |
| Diff rendering for autofix results | In development | EXPERIMENTAL |
| Thinking face reaction on mention | In development | INFERRED |

### Celery Tasks

**CONFIRMED**

| Task | Queue | Purpose | Retries |
|------|-------|---------|---------|
| `check_autofix_status` | `issues_tasks` | Detect stalled runs (15 min timeout) | 1 |
| `generate_summary_and_run_automation` | `ingest_errors_tasks` | Summary + automation (post-process) | 1 |
| `generate_issue_summary_only` | `ingest_errors_tasks` | Summary without automation | 3 (3s delay) |
| `run_automation_only_task` | `ingest_errors_tasks` | Run automation (assumes summary exists) | 1 |
| `configure_Loom_for_existing_org` | `issues_tasks` | Onboarding configuration | 3 |
| `trigger_autofix_from_issue_summary` | `Loom_tasks` | Async autofix trigger | 1 |
| `process_autofix_updates` | `Loom_tasks` | Route updates to entrypoints | 0 |

### Sentry Apps Webhooks

**CONFIRMED**

Loom broadcasts lifecycle events to installed Sentry Apps:

| Event | Confidence |
|-------|------------|
| `Loom.root_cause_started` | CONFIRMED |
| `Loom.root_cause_completed` | CONFIRMED |
| `Loom.solution_started` | CONFIRMED |
| `Loom.solution_completed` | CONFIRMED |
| `Loom.coding_started` | CONFIRMED |
| `Loom.coding_completed` | CONFIRMED |
| `Loom.pr_created` | CONFIRMED |
| `Loom.impact_assessment_started` | INFERRED |
| `Loom.impact_assessment_completed` | INFERRED |
| `Loom.triage_started` | INFERRED |
| `Loom.triage_completed` | INFERRED |

---

## 10. Data Privacy & Security

**CONFIRMED** — All from official documentation.

### Core Guarantees

| Guarantee | Detail |
|-----------|--------|
| No training by default | "Sentry does not train generative AI models using your data by default and without your permission" |
| Output confidentiality | AI output shown only to authorized users in your account |
| Subprocessor restrictions | Contractually prohibited from using customer data for model training |
| PII scrubbing | Applied before any training data inclusion |
| Deletion propagation | Deleting service data removes it from ML models |
| Retention | Mirrors underlying service data policies |

### Data Flow to AI Providers

| Feature | Data Sent | NOT Sent |
|---------|-----------|----------|
| AI Code Review | File names, code diffs, PR descriptions | Full repository content |
| Issue Fix / RCA | Error messages, stack traces, traces, logs, profiles, relevant source code | — |
| Summarization | Issue metadata, event context | — |

### Controls

| Control | Scope | Effect |
|---------|-------|--------|
| `Show Generative AI Features` toggle | Organization | Disables all AI features |
| Loom settings dropdown | Organization | Granular feature control |
| `Prevent code generation` | Organization (Advanced) | Blocks code generation + PR creation, not chat snippets |
| `sentry:hide_ai_features` | Organization | Kill switch |
| Data scrubbing tools | Project | Applied before data transmission |

### LLM Provider Configuration (MCP)

| Setting | Options |
|---------|---------|
| `EMBEDDED_AGENT_PROVIDER` | `"openai"` or `"anthropic"` |
| `OPENAI_API_KEY` | For OpenAI provider |
| `ANTHROPIC_API_KEY` | For Anthropic provider |

**CLOSED-SOURCE**: The specific LLM models powering Loom's inference (GPT-4, Claude, etc.) are not publicly disclosed. The `/api/0/Loom/models/` endpoint exists but response contents are not documented. The MCP server supports both OpenAI and Anthropic as embedded providers.

---

## 11. Pricing

### Current Model (January 2026+)

**CONFIRMED** — Official documentation, corroborated by January 2026 web announcements.

| Property | Value |
|----------|-------|
| Cost | $40 per active contributor per month |
| Active contributor | Any user making 2+ PRs to a Loom-connected repo in a month |
| Plan availability | Team, Business, Enterprise, Trial |
| Billing | Separate monthly charge, NOT against PAYG budget |
| Reset | Contributor counts reset monthly |
| Exclusions | GitHub bots marked as `[bot]` not counted |
| Usage | Unlimited within subscription |
| Trial | One-time 14-day trial |

### Legacy Model (Pre-January 2026, Being Phased Out)

**CONFIRMED**

| Property | Value |
|----------|-------|
| Base fee | $20/month per Sentry subscription |
| Included | $25 worth of Loom event credits |
| Overages | Draw from PAYG budget |
| Issue Scans | $0.003-$0.00219/run (tiered) |
| Issue Fixes | $1.00/run |

**INFERRED** — Seat-based transition managed by `is_Loom_seat_based_tier_enabled()` combining feature flag and billing flag checks (PR #104290).

---

## 12. Limitations & Known Gaps

Scope: `CONTEXT-ONLY` + `EXTERNAL-CLOSED` — these are external Sentry gaps, not evidence that qyl is missing functionality.

### Confirmed Limitations

| Limitation | Detail | Confidence |
|-----------|--------|------------|
| GitHub Cloud only | No GitLab, Bitbucket, Azure DevOps, self-hosted GitHub Enterprise | CONFIRMED |
| Cloud-only | Self-hosted Sentry instances have no Loom access | CONFIRMED |
| Drafts skipped | AI Code Review skips PRs in draft state | CONFIRMED |
| No retroactive analysis | Existing issues need manual trigger after Loom enablement | CONFIRMED |
| Code generation toggle | Disabling also blocks PR creation but not chat snippets | CONFIRMED |
| MCP maturity | "MCP is a developing technology and changes should be expected" | CONFIRMED |

### Inferred Limitations

| Limitation | Detail | Confidence |
|-----------|--------|------------|
| Age filter | Issues older than 2 weeks skipped for automation | INFERRED |
| Rate limiting | Scanner: 15/10s, Autofix: 20/hr (x tuning), no unlimited bypass | INFERRED |
| LLM opacity | Specific models not disclosed publicly | INFERRED |
| Cost scaling | Active contributor pricing scales with team size, not usage | CONFIRMED |

### Known Closed-Source Gaps

| Component | What We Know | What We Don't |
|-----------|-------------|---------------|
| LLM inference layer | Multiple models used, likely GPT + Claude | Exact model names, versions, routing logic |
| Prompt engineering | `prompts.py` exists in open code | Actual prompt contents in Loom service |
| Model training | jina-embeddings-v2 for grouping | Training data, fine-tuning process |
| GPU scoring | Fixability scores 0.0-1.0 | Model architecture, training methodology |
| Code review logic | "Loom Prevent" codename, request/response flow | Review heuristics, bug prediction model |

---

## 13. Experimental & Preview Features

Scope: `CONTEXT-ONLY` — external product lifecycle tracking, not a qyl acceptance gate.

### In Active Development (March 2026)

| Feature | Evidence | Flag | Confidence |
|---------|----------|------|------------|
| **Claude Code Agent** | 6+ PRs building full integration stack | `organizations:integrations-claude-code` | EXPERIMENTAL |
| **Slack Explorer** | @mention-based investigation in Slack threads | `Loom-slack-explorer` | EXPERIMENTAL |
| **Replay DOM Inspector** | "Inspect Element" sends DOM to Explorer | Multiple open PRs | EXPERIMENTAL |
| **Unified SCM Platform** | Abstraction for Git operations, GitLab explored | Issue #107469, PR #109468 | EXPERIMENTAL |
| **PR Review Dashboard** | Internal tool, "not meant to be released to customers" | `pr-review-dashboard` | EXPERIMENTAL |
| **Supergroup Embeddings** | Cross-issue semantic grouping from Explorer RCA | `projects:supergroup-embeddings-explorer` | EXPERIMENTAL |
| **V2 Grouping Model** | Improved similarity model | `projects:similarity-grouping-v2-model` | EXPERIMENTAL |
| **Viewer Context** | Org/user audit context on all Loom API calls | PRs #109697, #109719 | EXPERIMENTAL |
| **Structured Logs** | Loom uses structured logs during debugging | Marked "beta" in docs | EXPERIMENTAL |
| **Context Rule Parsing** | Auto-parses Cursor/Windsurf/Claude Code config files | Documented but new | EXPERIMENTAL |
| **Test Generation** | Unit test generation service | Code exists, no docs | EXPERIMENTAL |

### Recently Graduated (Shipped to 100%)

| Feature | Former Flag | Confidence |
|---------|------------|------------|
| Loom Webhooks | `organizations:Loom-webhooks` | CONFIRMED |
| Project Preferences | `organizations:autofix-Loom-preferences` | CONFIRMED |
| Coding Agent Integrations | `organizations:Loom-coding-agent-integrations` | CONFIRMED |

### Deprecated / Removed

| Feature | Former Flag | Confidence |
|---------|------------|------------|
| Unlimited auto-triggered runs | `organizations:unlimited-auto-triggered-autofix-runs` | CONFIRMED (dead code removed) |

---

## 14. Source Traceability

Every claim in this document can be traced to one of these source categories:

### Primary Sources (CONFIRMED)

| Source | URL |
|--------|-----|
| Loom Documentation | https://docs.sentry.io/product/ai-in-sentry/Loom/ |
| Issue Fix Documentation | https://docs.sentry.io/product/ai-in-sentry/Loom/issue-fix/ |
| AI Code Review Documentation | https://docs.sentry.io/product/ai-in-sentry/Loom/ai-code-review/ |
| AI/ML Data Policy | https://docs.sentry.io/security-legal-pii/security/ai-ml-policy/ |
| Pricing Documentation | https://docs.sentry.io/pricing/ |
| Loom API Endpoints | https://docs.sentry.io/api/Loom/ |
| MCP Server Documentation | https://docs.sentry.io/product/sentry-mcp/ |
| MCP Server Repository | https://github.com/getsentry/sentry-mcp |
| GitHub Integration Docs | https://docs.sentry.io/organization/integrations/source-code-mgmt/github/ |
| Issue Noise Blog Post | https://blog.sentry.io/how-sentry-decreased-issue-noise-with-ai/ |
| 2026 Sentry Blog Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-sentry-blog-seer-debug-with-ai.md` |
| 2026 Sentry Changelog Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-sentry-changelog-seer-now-debugs.md` |

### Secondary Sources (INFERRED / EXPERIMENTAL)

| Source | URL Pattern |
|--------|-------------|
| Open PRs | https://github.com/getsentry/sentry/pulls?q=Loom+is:open |
| Closed PRs | https://github.com/getsentry/sentry/pulls?q=Loom+is:closed |
| Loom-labeled Issues | https://github.com/getsentry/sentry/issues?q=is:issue+state:open+label:Loom |
| Open-source code | `src/sentry/Loom/**` in getsentry/sentry |
| Loom service mirror | https://github.com/doc-sheet/Loom |
| 2026 Partner Translation Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-28-ichizoku-jp-seer-debug-with-ai.md` |
| 2026 News Coverage Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-techintelpro-seer-expands.md` |
| 2026 Wire Coverage Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-28-ai-techpark-business-wire-seer.md` |
| 2026 Third-Party Explainer Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-02-13-oreateai-seer-copilot.md` |

### Closed-Source Boundary

The following components are known to exist in the closed `getsentry` repository and cannot be directly inspected:

| Component | Evidence of Existence |
|-----------|----------------------|
| Billing integration | Settings reference `is_Loom_seat_based_tier_enabled()` |
| Feature flag definitions | Flagpole references in open code |
| Loom service deployment config | Connection pool URLs, Terraform references |
| LLM model selection logic | `/v1/models` endpoint, `EMBEDDED_AGENT_PROVIDER` setting |
| Prompt templates (Loom-side) | RPC bridge provides data, inference runs in Loom |
| GPU scoring model | `SEER_SCORING_URL` references, fixability thresholds in code |

---

## Module Map

Complete source tree of `src/sentry/Loom/` in the open-source `getsentry/sentry` repository:

```
src/sentry/Loom/
├── __init__.py
├── apps.py                         # Django AppConfig
├── constants.py                    # SCM provider constants
├── signed_Loom_api.py              # HMAC-signed HTTP client
├── Loom_setup.py                   # Access checks
├── sentry_data_models.py           # Data model helpers
├── breakpoints.py                  # Performance breakpoint detection
├── issue_detection.py              # LLM-based issue creation
├── math.py                         # Vendored entropy functions
├── supergroups.py                  # Supergroup embedding API
├── trace_summary.py                # Trace summarization
├── utils.py                        # Repository filtering helpers
├── vendored.py                     # Vendored scipy entropy
│
├── anomaly_detection/
│   ├── delete_rule.py
│   ├── get_anomaly_data.py         # Main anomaly detection
│   ├── get_historical_anomalies.py
│   ├── store_data.py
│   ├── store_data_workflow_engine.py
│   ├── types.py                    # AnomalyDetectionConfig, etc.
│   └── utils.py
│
├── assisted_query/
│   ├── discover_tools.py           # Discover/event query tools
│   ├── issues_tools.py             # Issue search tools
│   └── traces_tools.py             # Trace search tools
│
├── autofix/
│   ├── artifact_schemas.py         # RootCauseArtifact, SolutionArtifact
│   ├── autofix.py                  # trigger_autofix(), _call_autofix()
│   ├── autofix_agent.py            # Explorer-based pipeline
│   ├── autofix_tools.py            # Profile/event detail tools
│   ├── coding_agent.py             # Third-party coding agent handoff
│   ├── constants.py                # AutofixStatus, FixabilityScoreThresholds
│   ├── issue_summary.py            # AI summarization + fixability + automation
│   ├── on_completion_hook.py       # Pipeline continuation logic
│   ├── prompts.py                  # LLM prompt templates
│   ├── types.py                    # TypedDicts for API payloads
│   ├── utils.py                    # Connection pools, rate limiting, state
│   └── webhooks.py                 # GitHub PR webhook handler
│
├── code_review/
│   ├── metrics.py
│   ├── models.py                   # LoomCodeReviewConfig
│   ├── preflight.py
│   ├── utils.py                    # Webhook handler, endpoint routing
│   └── webhooks/
│       ├── check_run.py
│       ├── handlers.py
│       ├── issue_comment.py
│       ├── pull_request.py
│       ├── task.py
│       └── types.py
│
├── endpoints/                      # 20+ REST API endpoints
│   ├── group_ai_autofix.py
│   ├── group_ai_summary.py
│   ├── group_autofix_setup_check.py
│   ├── group_autofix_update.py
│   ├── organization_Loom_rpc.py    # Massive Loom->Sentry RPC bridge
│   ├── organization_Loom_explorer_chat.py
│   ├── organization_Loom_explorer_runs.py
│   ├── organization_trace_summary.py
│   ├── project_Loom_preferences.py
│   ├── search_agent_start.py
│   ├── Loom_rpc.py                 # Internal RPC functions
│   └── ... (15+ more)
│
├── entrypoints/
│   ├── cache.py                    # LoomOperatorAutofixCache
│   ├── metrics.py                  # Lifecycle metrics
│   ├── operator.py                 # LoomOperator orchestrator
│   ├── registry.py                 # Entrypoint registry
│   ├── types.py                    # LoomEntrypoint protocol
│   └── slack/
│       └── entrypoint.py           # Slack entrypoint
│
├── explorer/
│   ├── client.py                   # LoomExplorerClient
│   ├── client_models.py            # LoomRunState, ExplorerRun
│   ├── coding_agent_handoff.py     # Launch coding agents from Explorer
│   ├── context_engine_utils.py
│   ├── custom_tool_utils.py        # ExplorerTool base class
│   ├── index_data.py               # Transaction/trace/profile indexing
│   ├── on_completion_hook.py       # Completion hook base class
│   └── tools.py                    # Built-in explorer tools
│
├── fetch_issues/
│   ├── by_error_type.py
│   ├── by_function_name.py
│   ├── by_text_query.py
│   └── utils.py
│
├── migrations/
│   ├── 0001_add_Loomorganizationsettings.py
│   └── 0002_add_default_coding_agent.py
│
├── models/
│   ├── organization_settings.py    # LoomOrganizationSettings
│   └── Loom_api_models.py          # Pydantic models for API contracts
│
├── services/
│   └── test_generation/            # Unit test generation (EXPERIMENTAL)
│       ├── impl.py
│       ├── model.py
│       └── service.py
│
├── similarity/
│   ├── config.py                   # V1 stable, V2 rollout config
│   ├── grouping_records.py         # CRUD for Loom grouping records
│   ├── similar_issues.py           # Core similarity function
│   ├── types.py                    # LoomSimilarIssueData
│   └── utils.py
│
└── workflows/
    └── compare.py                  # Distribution comparison
```

---

*Generated via requirements engineering methodology applied to open-source breadcrumbs. The `getsentry/sentry` repository is the primary evidence source. Claims tagged CLOSED-SOURCE represent acknowledged gaps where implementation details reside in the proprietary `getsentry` codebase.*
