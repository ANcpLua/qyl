  # Seer Design Specification

**Reverse-Engineered from Public Sources**

| Field | Value |
|-------|-------|
| Subject | Sentry Seer AI Debugging Agent |
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
   - 4.6 [AI Code Review (Seer Prevent)](#46-ai-code-review-seer-prevent)
   - 4.7 [Explorer (Interactive Agent)](#47-explorer-interactive-agent)
   - 4.8 [Anomaly Detection](#48-anomaly-detection)
   - 4.9 [Trace Summarization](#49-trace-summarization)
   - 4.10 [Assisted Query / Search Agent](#410-assisted-query--search-agent)
   - 4.11 [Test Generation](#411-test-generation)
5. [Coding Agent Providers](#5-coding-agent-providers)
6. [API Surface](#6-api-surface)
   - 6.1 [Public REST API](#61-public-rest-api)
   - 6.2 [Seer Service Endpoints (Sentry -> Seer)](#62-seer-service-endpoints-sentry---seer)
   - 6.3 [RPC Bridge (Seer -> Sentry)](#63-rpc-bridge-seer---sentry)
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
| **Seer** | Sentry's AI debugging agent. Umbrella name for all ML/AI features in the platform. |
| **Seer Prevent** | Internal codename for the AI Code Review subsystem. |
| **Autofix** | The automated issue-fixing pipeline (root cause -> solution -> code changes -> PR). |
| **Explorer** | The interactive agentic debugging interface (formerly the `Cmd+/` chat). |
| **Fixability Score** | A 0.0-1.0 score predicting whether an issue can be automatically fixed. GPU-computed. |
| **Supergroup** | Cross-issue semantic grouping triggered from Explorer RCA results. |
| **GroupHashMetadata** | Sentry model storing per-hash Seer state (embedding date, model version, match distance). |
| **SeerOperator** | Internal orchestrator connecting entrypoints (Slack, web) to Seer lifecycle events. |
| **Triage Signals** | The automation pipeline that decides whether to scan, summarize, and autofix an issue. |
| **Coding Agent** | External service that receives Seer's analysis and generates code changes (Seer built-in, Cursor, GitHub Copilot, Claude Code). |
| **HMAC-SHA256** | Authentication scheme used for all Sentry <-> Seer HTTP communication. |
| **pgvector** | PostgreSQL extension used for embedding storage and HNSW nearest-neighbor search. |
| **HNSW** | Hierarchical Navigable Small World — approximate nearest neighbor index algorithm. |
| **Flagpole** | Sentry's feature flag evaluation system. |

---

## 2. System Overview

**CONFIRMED**

Seer is a **separate service** from the Sentry monolith. It runs as multiple specialized microservices (pods), each handling a different ML/AI workload. The Sentry Django application acts as the frontend, data provider, and orchestrator, while Seer services perform the actual inference.

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
│                 Seer Service Cluster                   │
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
│  └──────────┘  Models: GCS gs://sentry-ml/seer/models  │
│                Observability: Langfuse                  │
└───────────────────────────────────────────────────────┘
```

### Communication Pattern

**CONFIRMED** — All Sentry-to-Seer calls use `make_signed_seer_api_request()` with:
- HMAC-SHA256 signing via `SEER_API_SHARED_SECRET`
- `Authorization: Rpcsignature rpc0:{signature}` header
- Optional `X-Viewer-Context` + `X-Viewer-Context-Signature` for audit trails
- urllib3 connection pooling (migrated from `requests.post` in Feb-Mar 2026)

**CONFIRMED** — Seer-to-Sentry callbacks use `OrganizationSeerRpcEndpoint` at `/api/0/internal/seer-rpc/` with `SEER_RPC_SHARED_SECRET`.

---

## 3. Service Topology

**INFERRED** from connection pool configuration and URL settings.

| Service | Setting | Connection Pool | Purpose |
|---------|---------|----------------|---------|
| Autofix/Explorer | `SEER_AUTOFIX_URL` | `seer_autofix_default_connection_pool` | Autofix, explorer, code review routing, project preferences, models, assisted query |
| Summarization | `SEER_SUMMARIZATION_URL` | `seer_summarization_default_connection_pool` | Issue + trace summaries |
| Anomaly Detection | `SEER_ANOMALY_DETECTION_URL` | `seer_anomaly_detection_default_connection_pool` | Time-series anomaly detection |
| Grouping | `SEER_GROUPING_URL` | `seer_grouping_connection_pool` | Embedding-based similar issue detection |
| Breakpoint Detection | `SEER_BREAKPOINT_DETECTION_URL` | `seer_breakpoint_connection_pool` | Performance regression detection |
| Scoring (GPU) | `SEER_SCORING_URL` | `fixability_connection_pool_gpu` | Fixability scoring |
| Code Review | `SEER_PREVENT_AI_URL` | `seer_code_review_connection_pool` | AI code review (Seer Prevent) |

**INFERRED** — At minimum 7 distinct URL configurations, suggesting either 7 separate deployments or route-based load balancing across fewer physical services.

---

## 4. Capabilities Catalog

### 4.1 Issue Grouping & Similarity (ML)

**CONFIRMED**

When a new event arrives that does not match existing groups by hash, Sentry calls Seer to check if the stacktrace is semantically similar to existing groups.

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
| `seer_date_sent` | When the hash was sent to Seer |
| `seer_event_sent` | Which event's stacktrace was sent |
| `seer_model` | Model version used |
| `seer_matched_grouphash` | FK to the matched group's hash |
| `seer_match_distance` | Similarity distance score |
| `seer_latest_training_model` | Tracks latest training-mode model version |

**Filtering criteria** (INFERRED):
- Event must have a stacktrace and a usable title
- Token count filtering replaced frame count filtering (PR #103997)
- Killswitch options: global Seer killswitch + similarity-specific killswitch

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

The first stage of the autofix pipeline. Seer analyzes the issue, telemetry, and stacktraces to determine the root cause.

| Property | Value | Confidence |
|----------|-------|------------|
| Input data | Error messages, stack traces, distributed traces, structured logs, performance profiles, source code | CONFIRMED (docs) |
| Multi-repo | Can analyze across multiple linked GitHub repos | CONFIRMED (docs) |
| Streaming | Reasoning is streamed to user in real-time | CONFIRMED (docs) |
| User feedback | Thumbs up/down on RCA card, tracked via `seer.autofix.feedback_submitted` | INFERRED (PR #104569) |
| Webhook event | `seer.root_cause_started`, `seer.root_cause_completed` | CONFIRMED (code) |

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
  seer.root_cause_*  seer.solution_*  seer.coding_*       seer.pr_created
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
- Scanner: `seer.max_num_scanner_autotriggered_per_ten_seconds` (default 15)
- Autofix: `seer.max_num_autofix_autotriggered_per_hour` (default 20, multiplied by tuning level)
- Issues older than 2 weeks are skipped for automation to prevent flood on enablement

**Billing** (CONFIRMED):
- `DataCategory.SEER_AUTOFIX` — billed per run
- `DataCategory.SEER_SCANNER` — billed per scan

**Dual-mode architecture** (INFERRED — code):
1. **Legacy mode** (default): Direct POST to Seer service
2. **Explorer mode** (`?mode=explorer`): Multi-step agentic pipeline via `SeerExplorerClient`

---

### 4.6 AI Code Review (Seer Prevent)

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
| Internal codename | Seer Prevent | INFERRED (code: `SEER_PREVENT_AI_URL`) |
| DB tracking | `CodeReviewRun` model: `task_enqueued` -> `seer_request_sent` -> `succeeded/failed` | INFERRED (PR #108445) |
| Retention | Cleanup task purges rows older than 90 days | INFERRED (code) |
| A/B testing | `organizations:code-review-experiments-enabled` flag + per-PR hash-based assignment | EXPERIMENTAL (code) |
| SCM support | GitHub Cloud only | CONFIRMED (docs) |

**Metrics** (INFERRED — PR #105984):
- `sentry.seer.code_review.webhook.received`
- `sentry.seer.code_review.webhook.filtered` (with `reason` tag)
- `sentry.seer.code_review.webhook.enqueued`
- `sentry.seer.code_review.webhook.error` (with `error_type` tag)
- `sentry.seer.code_review.task.e2e_latency`

---

### 4.7 Explorer (Interactive Agent)

**CONFIRMED** (feature-flagged)

The agentic investigation interface where users ask questions about errors, traces, and code.

| Property | Value | Confidence |
|----------|-------|------------|
| UI entry | `Cmd+/` in Sentry | CONFIRMED (docs) |
| Feature flag | `seer-explorer` | CONFIRMED (code) |
| Can trigger autofix | Yes, from within Explorer context | INFERRED (PR #108389) |
| Screenshot support | Frontend can pass screenshots for visual context | INFERRED (PR #99744) |
| Replay DOM inspection | "Inspect Element" in replay viewer sends DOM to Explorer | EXPERIMENTAL (PR #108527) |
| Supergroup embeddings | After Explorer generates RCA, triggers supergroup embedding | EXPERIMENTAL (PR #107819) |
| Slack integration | `@mention` in Slack triggers Explorer runs | EXPERIMENTAL |

**Slack Integration Detail** (EXPERIMENTAL — multiple open PRs):
- `SlackMentionHandler` parses mentions, extracts prompts, builds thread context
- `SlackEntrypoint` and `SeerOperator` pattern for workflow orchestration
- Renders: Issue Alert, Root Cause, Proposed Solution, Code Changes, Pull Request
- Thinking face reaction for immediate acknowledgment
- Feature flag: `seer-slack-explorer`

---

### 4.8 Anomaly Detection

**CONFIRMED**

Time-series anomaly detection for alert monitoring.

| Property | Value | Confidence |
|----------|-------|------------|
| Endpoint | `/v1/anomaly-detection/store` | CONFIRMED (code) |
| Pool | `seer_anomaly_detection_default_connection_pool` | CONFIRMED (code) |
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
| Module | `src/sentry/seer/services/test_generation/` | CONFIRMED (code) |
| Status | Present in code, no public documentation | EXPERIMENTAL |

---

## 5. Coding Agent Providers

**CONFIRMED** — Seer supports pluggable coding agent backends via a base class hierarchy.

| Provider | Status | Feature Flag | Integration Method | Confidence |
|----------|--------|--------------|-------------------|------------|
| **Seer (built-in)** | Production | None (default) | Direct via autofix pipeline | CONFIRMED |
| **Cursor** | Production | Graduated (`seer-coding-agent-integrations`) | Cursor Background Agent API | CONFIRMED (docs) |
| **GitHub Copilot** | Production | Graduated | Copilot Coding Agent Tasks API | INFERRED (PR #108565) |
| **Claude Code** | In Development | `organizations:integrations-claude-code` | Anthropic Claude Code agent API | EXPERIMENTAL (PRs #109526, #109738, #109750) |

**Base class hierarchy** (INFERRED — PR #109730):
```
CodingAgentIntegrationProvider   # Common provider config, setup dialog
  └─ CodingAgentPipelineView     # Pipeline view logic
  └─ CodingAgentIntegration      # Metadata persistence
```

**Organization settings** (CONFIRMED — code):
- `SeerOrganizationSettings.default_coding_agent` — `"seer"`, `"cursor"`, or `null`
- `SeerOrganizationSettings.default_coding_agent_integration_id` — FK to Integration

---

## 6. API Surface

### 6.1 Public REST API

**CONFIRMED** — These endpoints are documented and user-facing.

| Endpoint | Method | Purpose | Rate Limit |
|----------|--------|---------|------------|
| `/api/0/seer/models/` | GET | List active LLM model names | — |
| `/api/0/issues/{issue_id}/autofix/` | POST | Start autofix run | 25/min user, 100/hr org |
| `/api/0/issues/{issue_id}/autofix/` | GET | Get autofix state | 1024/min user |
| `/api/0/issues/{issue_id}/autofix/setup/` | GET | Check autofix prerequisites | — |
| `/api/0/issues/{issue_id}/autofix/update/` | POST | Send update to running autofix | — |
| `/api/0/issues/{issue_id}/ai-summary/` | POST | Generate AI issue summary | — |
| `/api/0/organizations/{org}/seer/setup-check/` | GET | Check Seer quota/billing | — |
| `/api/0/organizations/{org}/seer/onboarding-check/` | GET | Onboarding status | — |
| `/api/0/organizations/{org}/autofix-automation-settings/` | GET/PUT | Org automation settings | — |
| `/api/0/organizations/{org}/trace-summary/` | POST | AI trace summarization | — |
| `/api/0/organizations/{org}/seer-explorer/chat/` | POST | Explorer chat | — |
| `/api/0/organizations/{org}/seer-explorer/runs/` | GET | List Explorer runs | — |
| `/api/0/organizations/{org}/seer-explorer/update/` | POST | Update Explorer run | — |
| `/api/0/projects/{org}/{project}/seer-preferences/` | GET/PUT | Project Seer preferences | — |
| `/api/0/organizations/{org}/search-agent/start/` | POST | Start assisted search | — |
| `/api/0/organizations/{org}/search-agent/state/` | GET | Get search agent state | — |

### 6.2 Seer Service Endpoints (Sentry -> Seer)

**CONFIRMED** — These are the HTTP paths Sentry calls on the Seer service, extracted from code.

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

### 6.3 RPC Bridge (Seer -> Sentry)

**CONFIRMED** — `OrganizationSeerRpcEndpoint` at `/api/0/internal/seer-rpc/` is HMAC-authenticated.

The RPC bridge exposes dozens of functions for the Seer service to call back into Sentry, including:

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
| Seer integration | Can trigger Seer analysis, get fix recommendations, monitor fix status |
| Disable Seer | `MCP_DISABLE_SKILLS=seer` |
| Status | "Released for production, however MCP is a developing technology" |

**Note**: No MCP integration exists *inside* the `getsentry/sentry` codebase. The MCP server is a separate project that calls Sentry's public API.

---

## 7. Data Layer

### Django Models

**CONFIRMED** — Code-visible models.

**`SeerOrganizationSettings`** (`src/sentry/seer/models/organization_settings.py`):

| Field | Type | Purpose |
|-------|------|---------|
| `organization` | FK (unique, indexed) | Owning organization |
| `default_coding_agent` | CharField | `"seer"`, `"cursor"`, or null |
| `default_coding_agent_integration_id` | HybridCloudFK | FK to Integration |

**`GroupHashMetadata`** (extended by Seer — see Section 4.1 for fields)

**`CodeReviewRun`** (INFERRED — PR #108445):
- Tracks lifecycle: `task_enqueued` -> `seer_request_sent` -> `seer_request_succeeded/failed`

**`CodeReviewEvent`** (INFERRED — PR #108533):
- Rows for the internal PR Review Dashboard

### Migrations

| Migration | Purpose |
|-----------|---------|
| `0001_add_seerorganizationsettings.py` | Creates the settings table |
| `0002_add_default_coding_agent.py` | Adds coding agent fields |

### Caching Strategy

**CONFIRMED** — Code-visible cache keys.

| Cache Key Pattern | TTL | Purpose |
|-------------------|-----|---------|
| `ai-group-summary-v2:{group_id}` | 7 days | Issue summaries |
| `ai-trace-summary:{trace_slug}` | 7 days | Trace summaries |
| `seer-project-has-repos:{org_id}:{project_id}` | 15 min | Repo status |
| `seer:seat-based-tier:{org_id}` | 4 hours | Pricing tier |
| `autofix_access_check:{group_id}` | 1 min | Access check |
| `SeerOperatorAutofixCache` (by group_id/run_id) | — | Operator state |

### External Storage

**INFERRED** — From the Seer service repository mirror:

| Storage | Purpose |
|---------|---------|
| PostgreSQL + pgvector | Embedding storage, HNSW index for similarity search |
| Google Cloud Storage (`gs://sentry-ml/seer/models`) | ML model artifacts |
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
| `SEER_PREVENT_AI_URL` | Base URL: code review (Seer Prevent) |
| `SEER_API_SHARED_SECRET` | HMAC secret for Sentry -> Seer |
| `SEER_RPC_SHARED_SECRET` | HMAC secret for Seer -> Sentry |
| `SEER_AUTOFIX_GITHUB_APP_USER_ID` | Seer's GitHub App user ID |
| `SEER_MAX_GROUPING_DISTANCE` | Max distance threshold for similarity |
| `SEER_ANOMALY_DETECTION_TIMEOUT` | Timeout for anomaly detection |
| `SEER_BREAKPOINT_DETECTION_TIMEOUT` | Timeout for breakpoint detection |
| `SEER_FIXABILITY_TIMEOUT` | Timeout for fixability scoring |
| `CLAUDE_CODE_CLIENT_CLASS` | Dynamic client loading for Claude Code agent |

### Feature Flags

**Tags**: `A` = Active, `E` = Experimental, `G` = Graduated (100%), `D` = Deprecated/Dead

| Flag | Scope | Purpose | Tag |
|------|-------|---------|-----|
| `organizations:gen-ai-features` | Org | **Master gate** for all Seer AI features | A |
| `organizations:seer-explorer` | Org | Explorer agent UI | A |
| `organizations:autofix-on-explorer` | Org | Route autofix through Explorer pipeline | A |
| `organizations:seat-based-seer-enabled` | Org | Seat-based billing tier | A |
| `organizations:single-trace-summary` | Org | Trace summarization | A |
| `organizations:code-review-experiments-enabled` | Org | A/B testing for code review | A |
| `organizations:integrations-claude-code` | Org | Claude Code coding agent | E |
| `organizations:seer-slack-workflows-explorer` | Org | Slack workflows on Explorer | E |
| `projects:similarity-grouping-v2-model` | Project | V2 grouping model rollout | E |
| `projects:supergroup-embeddings-explorer` | Project | Supergroup embeddings via Explorer | E |
| `seer-explorer` | System | Explorer feature | A |
| `seer-slack-explorer` | System | Slack @mention Explorer | E |
| `pr-review-dashboard` | System | Internal PR review dashboard | E |
| `triage-signals-v0-org` | System | New automation flow | A |
| `seer-agent-pr-consolidation` | System | Consolidated PR toggle UI | A |
| `organizations:seer-webhooks` | Org | Webhook subscriptions | G |
| `organizations:autofix-seer-preferences` | Org | Project preferences | G |
| `organizations:seer-coding-agent-integrations` | Org | External coding agents | G |
| `organizations:unlimited-auto-triggered-autofix-runs` | Org | Bypass rate limiting | D |

### Organization Options

| Option | Type | Purpose | Confidence |
|--------|------|---------|------------|
| `sentry:hide_ai_features` | bool | Kill switch for all AI features | CONFIRMED |
| `sentry:enable_seer_coding` | bool | Enable/disable code changes step | CONFIRMED |
| `sentry:default_autofix_automation_tuning` | str | Default automation tuning for org | INFERRED |
| `sentry:auto_open_prs` | bool | Default PR creation for new projects | INFERRED |
| `sentry:default_automation_handoff` | str | Default coding agent for new projects | INFERRED |

### Project Options

| Option | Type | Purpose | Confidence |
|--------|------|---------|------------|
| `sentry:seer_scanner_automation` | bool | Enable Seer scanner for project | CONFIRMED |
| `sentry:autofix_automation_tuning` | str | Tuning: off/super_low/low/medium/high/always | CONFIRMED |

### Rate Limit Options

| Option | Default | Confidence |
|--------|---------|------------|
| `seer.max_num_scanner_autotriggered_per_ten_seconds` | 15 | INFERRED |
| `seer.max_num_autofix_autotriggered_per_hour` | 20 (x tuning multiplier) | INFERRED |
| `seer.similarity.circuit-breaker-config` | — | CONFIRMED |
| `seer.similarity.grouping-ingest-retries` | — | CONFIRMED |
| `seer.similarity.grouping-ingest-timeout` | — | CONFIRMED |

---

## 9. Integration Points

### GitHub

**CONFIRMED**

| Integration | Method | Details |
|-------------|--------|---------|
| Source code access | Contents (Read & Write) | Fetch files, commits, blame data |
| PR creation | Seer GitHub App (`seer-by-sentry`) | Separate from main Sentry GitHub App |
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
| `configure_seer_for_existing_org` | `issues_tasks` | Onboarding configuration | 3 |
| `trigger_autofix_from_issue_summary` | `seer_tasks` | Async autofix trigger | 1 |
| `process_autofix_updates` | `seer_tasks` | Route updates to entrypoints | 0 |

### Sentry Apps Webhooks

**CONFIRMED**

Seer broadcasts lifecycle events to installed Sentry Apps:

| Event | Confidence |
|-------|------------|
| `seer.root_cause_started` | CONFIRMED |
| `seer.root_cause_completed` | CONFIRMED |
| `seer.solution_started` | CONFIRMED |
| `seer.solution_completed` | CONFIRMED |
| `seer.coding_started` | CONFIRMED |
| `seer.coding_completed` | CONFIRMED |
| `seer.pr_created` | CONFIRMED |
| `seer.impact_assessment_started` | INFERRED |
| `seer.impact_assessment_completed` | INFERRED |
| `seer.triage_started` | INFERRED |
| `seer.triage_completed` | INFERRED |

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
| Seer settings dropdown | Organization | Granular feature control |
| `Prevent code generation` | Organization (Advanced) | Blocks code generation + PR creation, not chat snippets |
| `sentry:hide_ai_features` | Organization | Kill switch |
| Data scrubbing tools | Project | Applied before data transmission |

### LLM Provider Configuration (MCP)

| Setting | Options |
|---------|---------|
| `EMBEDDED_AGENT_PROVIDER` | `"openai"` or `"anthropic"` |
| `OPENAI_API_KEY` | For OpenAI provider |
| `ANTHROPIC_API_KEY` | For Anthropic provider |

**CLOSED-SOURCE**: The specific LLM models powering Seer's inference (GPT-4, Claude, etc.) are not publicly disclosed. The `/api/0/seer/models/` endpoint exists but response contents are not documented. The MCP server supports both OpenAI and Anthropic as embedded providers.

---

## 11. Pricing

### Current Model (January 2026+)

**CONFIRMED** — Official documentation.

| Property | Value |
|----------|-------|
| Cost | $40 per active contributor per month |
| Active contributor | Any user making 2+ PRs to a Seer-connected repo in a month |
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
| Included | $25 worth of Seer event credits |
| Overages | Draw from PAYG budget |
| Issue Scans | $0.003-$0.00219/run (tiered) |
| Issue Fixes | $1.00/run |

**INFERRED** — Seat-based transition managed by `is_seer_seat_based_tier_enabled()` combining feature flag and billing flag checks (PR #104290).

---

## 12. Limitations & Known Gaps

### Confirmed Limitations

| Limitation | Detail | Confidence |
|-----------|--------|------------|
| GitHub Cloud only | No GitLab, Bitbucket, Azure DevOps, self-hosted GitHub Enterprise | CONFIRMED |
| Cloud-only | Self-hosted Sentry instances have no Seer access | CONFIRMED |
| Drafts skipped | AI Code Review skips PRs in draft state | CONFIRMED |
| No retroactive analysis | Existing issues need manual trigger after Seer enablement | CONFIRMED |
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
| Prompt engineering | `prompts.py` exists in open code | Actual prompt contents in Seer service |
| Model training | jina-embeddings-v2 for grouping | Training data, fine-tuning process |
| GPU scoring | Fixability scores 0.0-1.0 | Model architecture, training methodology |
| Code review logic | "Seer Prevent" codename, request/response flow | Review heuristics, bug prediction model |

---

## 13. Experimental & Preview Features

### In Active Development (March 2026)

| Feature | Evidence | Flag | Confidence |
|---------|----------|------|------------|
| **Claude Code Agent** | 6+ PRs building full integration stack | `organizations:integrations-claude-code` | EXPERIMENTAL |
| **Slack Explorer** | @mention-based investigation in Slack threads | `seer-slack-explorer` | EXPERIMENTAL |
| **Replay DOM Inspector** | "Inspect Element" sends DOM to Explorer | Multiple open PRs | EXPERIMENTAL |
| **Unified SCM Platform** | Abstraction for Git operations, GitLab explored | Issue #107469, PR #109468 | EXPERIMENTAL |
| **PR Review Dashboard** | Internal tool, "not meant to be released to customers" | `pr-review-dashboard` | EXPERIMENTAL |
| **Supergroup Embeddings** | Cross-issue semantic grouping from Explorer RCA | `projects:supergroup-embeddings-explorer` | EXPERIMENTAL |
| **V2 Grouping Model** | Improved similarity model | `projects:similarity-grouping-v2-model` | EXPERIMENTAL |
| **Viewer Context** | Org/user audit context on all Seer API calls | PRs #109697, #109719 | EXPERIMENTAL |
| **Structured Logs** | Seer uses structured logs during debugging | Marked "beta" in docs | EXPERIMENTAL |
| **Context Rule Parsing** | Auto-parses Cursor/Windsurf/Claude Code config files | Documented but new | EXPERIMENTAL |
| **Test Generation** | Unit test generation service | Code exists, no docs | EXPERIMENTAL |

### Recently Graduated (Shipped to 100%)

| Feature | Former Flag | Confidence |
|---------|------------|------------|
| Seer Webhooks | `organizations:seer-webhooks` | CONFIRMED |
| Project Preferences | `organizations:autofix-seer-preferences` | CONFIRMED |
| Coding Agent Integrations | `organizations:seer-coding-agent-integrations` | CONFIRMED |

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
| Seer Documentation | https://docs.sentry.io/product/ai-in-sentry/seer/ |
| Issue Fix Documentation | https://docs.sentry.io/product/ai-in-sentry/seer/issue-fix/ |
| AI Code Review Documentation | https://docs.sentry.io/product/ai-in-sentry/seer/ai-code-review/ |
| AI/ML Data Policy | https://docs.sentry.io/security-legal-pii/security/ai-ml-policy/ |
| Pricing Documentation | https://docs.sentry.io/pricing/ |
| Seer API Endpoints | https://docs.sentry.io/api/seer/ |
| MCP Server Documentation | https://docs.sentry.io/product/sentry-mcp/ |
| MCP Server Repository | https://github.com/getsentry/sentry-mcp |
| GitHub Integration Docs | https://docs.sentry.io/organization/integrations/source-code-mgmt/github/ |
| Issue Noise Blog Post | https://blog.sentry.io/how-sentry-decreased-issue-noise-with-ai/ |

### Secondary Sources (INFERRED / EXPERIMENTAL)

| Source | URL Pattern |
|--------|-------------|
| Open PRs | https://github.com/getsentry/sentry/pulls?q=seer+is:open |
| Closed PRs | https://github.com/getsentry/sentry/pulls?q=seer+is:closed |
| Seer-labeled Issues | https://github.com/getsentry/sentry/issues?q=is:issue+state:open+label:Seer |
| Open-source code | `src/sentry/seer/**` in getsentry/sentry |
| Seer service mirror | https://github.com/doc-sheet/seer |

### Closed-Source Boundary

The following components are known to exist in the closed `getsentry` repository and cannot be directly inspected:

| Component | Evidence of Existence |
|-----------|----------------------|
| Billing integration | Settings reference `is_seer_seat_based_tier_enabled()` |
| Feature flag definitions | Flagpole references in open code |
| Seer service deployment config | Connection pool URLs, Terraform references |
| LLM model selection logic | `/v1/models` endpoint, `EMBEDDED_AGENT_PROVIDER` setting |
| Prompt templates (Seer-side) | RPC bridge provides data, inference runs in Seer |
| GPU scoring model | `SEER_SCORING_URL` references, fixability thresholds in code |

---

## Module Map

Complete source tree of `src/sentry/seer/` in the open-source `getsentry/sentry` repository:

```
src/sentry/seer/
├── __init__.py
├── apps.py                         # Django AppConfig
├── constants.py                    # SCM provider constants
├── signed_seer_api.py              # HMAC-signed HTTP client
├── seer_setup.py                   # Access checks
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
│   ├── models.py                   # SeerCodeReviewConfig
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
│   ├── organization_seer_rpc.py    # Massive Seer->Sentry RPC bridge
│   ├── organization_seer_explorer_chat.py
│   ├── organization_seer_explorer_runs.py
│   ├── organization_trace_summary.py
│   ├── project_seer_preferences.py
│   ├── search_agent_start.py
│   ├── seer_rpc.py                 # Internal RPC functions
│   └── ... (15+ more)
│
├── entrypoints/
│   ├── cache.py                    # SeerOperatorAutofixCache
│   ├── metrics.py                  # Lifecycle metrics
│   ├── operator.py                 # SeerOperator orchestrator
│   ├── registry.py                 # Entrypoint registry
│   ├── types.py                    # SeerEntrypoint protocol
│   └── slack/
│       └── entrypoint.py           # Slack entrypoint
│
├── explorer/
│   ├── client.py                   # SeerExplorerClient
│   ├── client_models.py            # SeerRunState, ExplorerRun
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
│   ├── 0001_add_seerorganizationsettings.py
│   └── 0002_add_default_coding_agent.py
│
├── models/
│   ├── organization_settings.py    # SeerOrganizationSettings
│   └── seer_api_models.py          # Pydantic models for API contracts
│
├── services/
│   └── test_generation/            # Unit test generation (EXPERIMENTAL)
│       ├── impl.py
│       ├── model.py
│       └── service.py
│
├── similarity/
│   ├── config.py                   # V1 stable, V2 rollout config
│   ├── grouping_records.py         # CRUD for Seer grouping records
│   ├── similar_issues.py           # Core similarity function
│   ├── types.py                    # SeerSimilarIssueData
│   └── utils.py
│
└── workflows/
    └── compare.py                  # Distribution comparison
```

---

*Generated via requirements engineering methodology applied to open-source breadcrumbs. The `getsentry/sentry` repository is the primary evidence source. Claims tagged CLOSED-SOURCE represent acknowledged gaps where implementation details reside in the proprietary `getsentry` codebase.*
