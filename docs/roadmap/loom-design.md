# Loom Design Specification

**Reverse-Engineered from Public Sources**

| Field   | Value                                                                            |
|---------|----------------------------------------------------------------------------------|
| Subject | Sentry Loom AI Debugging Agent                                                   |
| Version | 1.0 (March 2026)                                                                 |
| Method  | Requirements engineering on open-source breadcrumbs                              |
| Sources | `getsentry/sentry` PRs, issues, code; `docs.sentry.io`; Sentry blog; API surface |

---

## Confidence Classification

Every claim in this document carries a confidence tag. The taxonomy mirrors requirement maturity levels used
in [Requirements-Engineering-NASA-cFE](https://github.com/ANcpLua/Requirements-Engineering-NASA-cFE):

| Tag               | Meaning                                                                      | Equivalent                                     |
|-------------------|------------------------------------------------------------------------------|------------------------------------------------|
| **CONFIRMED**     | Documented in public docs + visible in open-source code                      | SSRx verbatim from NASA SRS                    |
| **INFERRED**      | Evidence in merged PRs/issues, high confidence but not officially documented | Manual attempt — derived from primary material |
| **EXPERIMENTAL**  | Behind feature flags, open PRs, or explicitly marked preview/beta            | Baseline AI output — plausible but unverified  |
| **CLOSED-SOURCE** | Known to exist in `getsentry` (closed repo), cannot inspect implementation   | SSR6 incomplete — acknowledged gap             |
| **DEPRECATED**    | Feature flag removed, code deleted, or superseded                            | Removed requirement                            |

---

## qyl Scope Labels

For qyl planning and implementation tracking, use this scope taxonomy in addition
to the confidence tags above:

| Label                | Meaning                                                                   |
|----------------------|---------------------------------------------------------------------------|
| `IMPLEMENTED-IN-QYL` | Capability exists in this repository and is testable locally.             |
| `CONTEXT-ONLY`       | Comparative/reference information; useful context, not a local ship gate. |
| `EXTERNAL-CLOSED`    | Known unknowns in external closed-source systems.                         |
| `NOT-PLANNED`        | Explicitly excluded from qyl architecture and roadmap.                    |

Closed-source Sentry internals remain non-inspectable, but qyl implements its own
open Loom-like backend surface (triage, autofix, code review, handoff, regression,
webhook ingestion, dashboard, and MCP tooling).

### Implementation Evidence in qyl

| Capability                                      | Status               | Evidence                                                                                                                                                                                                                                                                                                     |
|-------------------------------------------------|----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Endpoint families for Loom-like workflows       | `IMPLEMENTED-IN-QYL` | `Program.cs` maps `MapAutofixEndpoints`, `MapRegressionEndpoints`, `MapAgentHandoffEndpoints`, `MapCodeReviewEndpoints`, `MapGitHubWebhookEndpoints`, `MapLoomSettingsEndpoints`, `MapTriageEndpoints`. Endpoint registration exists, but registration alone is not treated as proof of runtime correctness. |
| Autofix pipeline                                | `IMPLEMENTED-IN-QYL` | `AutofixAgentService.cs`, `AutofixOrchestrator.cs`, `AutofixEndpoints.cs`, `DuckDbStore.Autofix*.cs`. Current document status: implementation present, end-to-end verification still required.                                                                                                               |
| Fixability scoring and triage                   | `IMPLEMENTED-IN-QYL` | `TriagePipelineService.cs`, `TriagePrompts.cs`, `TriageEndpoints.cs`, `DuckDbSchema.Triage.cs`. Current document status: implementation present, real-issue scoring verification still required.                                                                                                             |
| Code review endpoints and service               | `IMPLEMENTED-IN-QYL` | `CodeReviewEndpoints.cs`, `CodeReviewService.cs`, `CodeReviewPrompt.cs`. Current document status: implementation present, webhook-to-comment flow verification still required.                                                                                                                               |
| GitHub webhook ingestion + signature validation | `IMPLEMENTED-IN-QYL` | `GitHubWebhookEndpoints.cs` (`X-Hub-Signature-256`, `HMACSHA256`). Current document status: implementation present, signed payload verification still required.                                                                                                                                              |
| Agent handoff lifecycle                         | `IMPLEMENTED-IN-QYL` | `AgentHandoffEndpoints.cs`, `AgentHandoffService.cs`, `DuckDbStore.Handoff.cs`. Current document status: implementation present, lifecycle transition verification still required.                                                                                                                           |
| Regression detection and querying               | `IMPLEMENTED-IN-QYL` | `RegressionDetectionService.cs`, `RegressionEndpoints.cs`, `DuckDbStore.Regressions.cs`. Current document status: implementation present, detection/query verification still required.                                                                                                                       |
| Dashboard/UI for Loom flows                     | `IMPLEMENTED-IN-QYL` | `LoomDashboardPage.tsx`, `IssueTriagePage.tsx`, `IssueFixRunsPage.tsx`, `CodeReviewPage.tsx`. Current document status: implementation present, Playwright validation still required.                                                                                                                         |
| MCP tooling for Loom flows                      | `IMPLEMENTED-IN-QYL` | `AutofixMcpTools.cs`, `TriageTools.cs`, `RegressionTools.cs`, `GitHubMcpTools.cs`, `AgentHandoffTools.cs`, `AssistedQueryTools.cs`, `TestGenerationTools.cs`. Current document status: tool registration exists, protocol-level invocation verification still required.                                      |

### qyl Verification Matrix

This table is the current ship bar for qyl. A capability is not considered verified until the full flow runs end-to-end
with real inputs and real outputs.

| Capability                | qyl Status           | Verification Still Required                                                                                     |
|---------------------------|----------------------|-----------------------------------------------------------------------------------------------------------------|
| Autofix pipeline          | `IMPLEMENTED-IN-QYL` | Run full issue -> root cause -> solution -> code change flow; confirm outputs are real and persisted correctly. |
| Triage/fixability scoring | `IMPLEMENTED-IN-QYL` | Execute scoring against real issues and validate thresholds, persistence, and API shape.                        |
| Code review               | `IMPLEMENTED-IN-QYL` | Exercise GitHub webhook ingestion through PR analysis and confirm comment/status output.                        |
| GitHub webhook ingestion  | `IMPLEMENTED-IN-QYL` | Validate HMAC-SHA256 signature handling and event routing with signed payloads.                                 |
| Agent handoff             | `IMPLEMENTED-IN-QYL` | Verify lifecycle transitions, state storage, and recovery behavior.                                             |
| Regression detection      | `IMPLEMENTED-IN-QYL` | Verify detection, storage, and query layer with real time-series inputs.                                        |
| Dashboard UI              | `IMPLEMENTED-IN-QYL` | Cover with Playwright after Phase 1 frontend stabilization.                                                     |
| MCP tooling               | `IMPLEMENTED-IN-QYL` | Call tools through the MCP protocol and confirm real responses, not just registration.                          |

### 2026 Web Source Synthesis

The six extracted 2026 web markdown files collapse into two useful source groups: one primary product announcement
cluster and one secondary explainer. The partner translation and press/news reposts do not add net-new product behavior
beyond the primary Sentry post, but they do reinforce positioning, pricing, and operating assumptions.

#### Primary product signal: Seer expanded across the full development loop

Scope label for qyl: `CONTEXT-ONLY`

Deduplicated across:

- Sentry blog primary announcement
- Ichizoku Japanese translation mirror
- TechIntelPro secondary coverage
- AI-Tech Park / Business Wire syndication
- Sentry changelog page, which was only partially extractable in this corpus

Consolidated product claims:

- Seer is explicitly positioned as a shift-left system spanning local development, code review, and production rather
  than a production-only debugger.
- Runtime telemetry is the differentiator. The consistent message across sources is that traces, logs, metrics, errors,
  and related context are required to catch failures that static code inspection misses.
- Local development workflow depends on the Sentry MCP server as the bridge between a coding agent and runtime telemetry
  during reproduction of a bug.
- Code review focuses on production-risking defects, not style or low-signal lint-like feedback.
- Production flow remains root-cause-first, with optional code generation or delegation to an external coding agent when
  confidence is high.
- Open-ended investigation over telemetry is real but still preview-stage, so it should not be treated as a hard ship
  requirement for qyl.
- Pricing converged in January 2026 to `$40` per active contributor per month with unlimited usage, where an active
  contributor is defined by `2+` pull requests in a connected repository during the billing month.

qyl implication:

- The local product target is not "AI everywhere" in the abstract. The target is a telemetry-grounded debugging loop
  that proves value in three concrete surfaces: local triage/autofix, pre-merge review, and post-merge or production
  investigation.

#### Secondary product signal: Seer as debugging copilot, with privacy constraints

Scope label for qyl: `CONTEXT-ONLY`

Deduplicated across:

- Oreate AI third-party explainer

Consolidated product claims:

- Seer is framed as a copilot that operates on rich debugging context rather than as a generic assistant.
- Multi-repository and distributed-system reasoning are part of the public story, even if implementation details are not
  fully public.
- Privacy messaging matters: customer data and source code are not used for model training without explicit consent, and
  generated output is scoped to authorized users.

qyl implication:

- qyl should prioritize strong operator trust boundaries: explicit data movement, auditable tool actions, and clear
  separation between analysis context and code mutation.

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

- [Module Map](#module-map)

15. [qyl Platform Capabilities Reference](#15-qyl-platform-capabilities-reference)
    - 15.1 [GenAI Semantic Conventions Model](#151-genai-semantic-conventions-model)
    - 15.2 [DuckDB Storage — Appender Architecture](#152-duckdb-storage--appender-architecture)
    - 15.3 [MCP Platform Design](#153-mcp-platform-design)
    - 15.4 [AI Chat Analytics (6 Modules)](#154-ai-chat-analytics-6-modules)
    - 15.5 [AG-UI + Declarative Workflows](#155-ag-ui--declarative-workflows)
    - 15.6 [Compile-Time Tracing Annotations](#156-compile-time-tracing-annotations)
    - 15.7 [Zero-Cost Observability Contracts](#157-zero-cost-observability-contracts)
    - 15.8 [Port Architecture (OTLP Standard Compliance)](#158-port-architecture-otlp-standard-compliance)
    - 15.9 [Aspire 13.x Feature Coverage](#159-aspire-13x-feature-coverage)
    - 15.10 [Hosting Resource Model](#1510-hosting-resource-model)
    - 15.11 [Agent Continuation Evaluation (Heuristic-First Pattern)](#1511-agent-continuation-evaluation-heuristic-first-pattern)
16. [Sentry Platform Features (Beyond Loom)](#16-sentry-platform-features-beyond-loom)
    - 16.1 [AI Agent Monitoring](#161-ai-agent-monitoring)
    - 16.2 [LLM Monitoring](#162-llm-monitoring)
    - 16.3 [Session Replay](#163-session-replay)
    - 16.4 [Uptime & Cron Monitoring](#164-uptime--cron-monitoring)
    - 16.5 [Release Health](#165-release-health)
    - 16.6 [User Feedback](#166-user-feedback)
    - 16.7 [Dashboards & Insights](#167-dashboards--insights)
    - 16.8 [Alerts](#168-alerts)
    - 16.9 [Logs Explorer](#169-logs-explorer)
    - 16.10 [Pricing Model](#1610-pricing-model)
17. [sentry-mcp Architecture](#17-sentry-mcp-architecture)
    - 17.1 [Tool Catalog](#171-tool-catalog)
    - 17.2 [Skills-Based Authorization](#172-skills-based-authorization)
    - 17.3 [Embedded Agent Framework](#173-embedded-agent-framework)
    - 17.4 [Dual OAuth Architecture](#174-dual-oauth-architecture)
    - 17.5 [Deployment Models](#175-deployment-models)
    - 17.6 [Error Handling & Rate Limiting](#176-error-handling--rate-limiting)
    - 17.7 [Constraints & Capabilities](#177-constraints--capabilities)
    - 17.8 [Technology Stack](#178-technology-stack)
18. [Competitive Analysis](#18-competitive-analysis)
    - 18.1 [qodo-skills (Multi-Provider Code Review)](#181-qodo-skills-multi-provider-code-review)
    - 18.2 [Feature Gap Matrix (Sentry vs. qyl)](#182-feature-gap-matrix-sentry-vs-qyl)
19. [Development Principles](#19-development-principles)
20. [Seer AI Platform — Deep Architecture](#20-seer-ai-platform--deep-architecture)
    - 20.1 [Autofix Pipeline — 5-Step LLM Chain](#201-autofix-pipeline--5-step-llm-chain)
    - 20.2 [Explorer Agent — Interactive Debugging](#202-explorer-agent--interactive-debugging)
    - 20.3 [Code Review — Bug Prediction](#203-code-review--bug-prediction)
    - 20.4 [Anomaly Detection — Prophet-Like Forecasting](#204-anomaly-detection--prophet-like-forecasting)
    - 20.5 [Issue Similarity — Embeddings V1/V2](#205-issue-similarity--embeddings-v1v2)
    - 20.6 [Breakpoint Detection — Trend Analysis](#206-breakpoint-detection--trend-analysis)
    - 20.7 [Fetch Issues — Context Retrieval](#207-fetch-issues--context-retrieval)
    - 20.8 [RPC Method Registry](#208-rpc-method-registry)
    - 20.9 [Seer API Endpoints](#209-seer-api-endpoints)
    - 20.10 [Seer Feature Flags](#2010-seer-feature-flags)
    - 20.11 [Seer Data Models](#2011-seer-data-models)
    - 20.12 [Seer Entrypoints & Background Tasks](#2012-seer-entrypoints--background-tasks)
    - 20.13 [Seer Error Handling & Monitoring](#2013-seer-error-handling--monitoring)
21. [qyl Roadmap — Extended Feature Reference](#21-qyl-roadmap--extended-feature-reference)
    - 21.1 [AI Chat Analytics — Extended](#211-ai-chat-analytics--extended-from-ai-chat-analyticsmd)
    - 21.2 [Port Architecture — Extended](#212-port-architecture--extended-from-antipattern-remediationmd)
    - 21.3 [DuckDB Appender Purge](#213-duckdb-appender-purge-from-hades-storage-purgemd)
    - 21.4 [MCP Platform — Extended](#214-mcp-platform--extended-from-mcp-platformmd)
    - 21.5 [GenAI Semconv Full Reference](#215-genai-semconv-full-reference-from-otel-semconv-referencemd)
    - 21.6 [Traced Annotations — Stories](#216-traced-annotations--stories-from-traced-annotationsmd)
    - 21.7 [Zero-Cost Observability — Phases](#217-zero-cost-observability--phases-from-zero-cost-observabilitymd)
    - 21.8 [AG-UI — Implementation Detail](#218-ag-ui--implementation-detail-from-ag-ui-design--impl-docs)
    - 21.9 [Aspire Coverage — Extended Matrix](#219-aspire-coverage--extended-matrix-from-aspire-coveragemd)
    - 21.10 [CI/CD Improvements](#2110-cicd-improvements-from-suggested-improvementsyaml)
22. [Requirements Registry](#22-requirements-registry)
23. [Traceability Matrix](#23-traceability-matrix)

---

## 1. Glossary

| Term                  | Definition                                                                                                                      |
|-----------------------|---------------------------------------------------------------------------------------------------------------------------------|
| **Loom**              | Sentry's AI debugging agent. Umbrella name for all ML/AI features in the platform.                                              |
| **Loom Prevent**      | Internal codename for the AI Code Review subsystem.                                                                             |
| **Autofix**           | The automated issue-fixing pipeline (root cause -> solution -> code changes -> PR).                                             |
| **Explorer**          | The interactive agentic debugging interface (formerly the `Cmd+/` chat).                                                        |
| **Fixability Score**  | A 0.0-1.0 score predicting whether an issue can be automatically fixed. GPU-computed.                                           |
| **Supergroup**        | Cross-issue semantic grouping triggered from Explorer RCA results.                                                              |
| **GroupHashMetadata** | Sentry model storing per-hash Loom state (embedding date, model version, match distance).                                       |
| **LoomOperator**      | Internal orchestrator connecting entrypoints (Slack, web) to Loom lifecycle events.                                             |
| **Triage Signals**    | The automation pipeline that decides whether to scan, summarize, and autofix an issue.                                          |
| **Coding Agent**      | External service that receives Loom's analysis and generates code changes (Loom built-in, Cursor, GitHub Copilot, Claude Code). |
| **HMAC-SHA256**       | Authentication scheme used for all Sentry <-> Loom HTTP communication.                                                          |
| **pgvector**          | PostgreSQL extension used for embedding storage and HNSW nearest-neighbor search.                                               |
| **HNSW**              | Hierarchical Navigable Small World — approximate nearest neighbor index algorithm.                                              |
| **Flagpole**          | Sentry's feature flag evaluation system.                                                                                        |

---

## 2. System Overview

**CONFIRMED**

Loom is a **separate service** from the Sentry monolith. It runs as multiple specialized microservices (pods), each
handling a different ML/AI workload. The Sentry Django application acts as the frontend, data provider, and
orchestrator, while Loom services perform the actual inference.

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

**CONFIRMED** — Loom-to-Sentry callbacks use `OrganizationLoomRpcEndpoint` at `/api/0/internal/Loom-rpc/` with
`SEER_RPC_SHARED_SECRET`.

---

## 3. Service Topology

**INFERRED** from connection pool configuration and URL settings.

| Service              | Setting                         | Connection Pool                                  | Purpose                                                                             |
|----------------------|---------------------------------|--------------------------------------------------|-------------------------------------------------------------------------------------|
| Autofix/Explorer     | `SEER_AUTOFIX_URL`              | `Loom_autofix_default_connection_pool`           | Autofix, explorer, code review routing, project preferences, models, assisted query |
| Summarization        | `SEER_SUMMARIZATION_URL`        | `Loom_summarization_default_connection_pool`     | Issue + trace summaries                                                             |
| Anomaly Detection    | `SEER_ANOMALY_DETECTION_URL`    | `Loom_anomaly_detection_default_connection_pool` | Time-series anomaly detection                                                       |
| Grouping             | `SEER_GROUPING_URL`             | `Loom_grouping_connection_pool`                  | Embedding-based similar issue detection                                             |
| Breakpoint Detection | `SEER_BREAKPOINT_DETECTION_URL` | `Loom_breakpoint_connection_pool`                | Performance regression detection                                                    |
| Scoring (GPU)        | `SEER_SCORING_URL`              | `fixability_connection_pool_gpu`                 | Fixability scoring                                                                  |
| Code Review          | `SEER_PREVENT_AI_URL`           | `Loom_code_review_connection_pool`               | AI code review (Loom Prevent)                                                       |

**INFERRED** — At minimum 7 distinct URL configurations, suggesting either 7 separate deployments or route-based load
balancing across fewer physical services.

---

## 4. Capabilities Catalog

### 4.1 Issue Grouping & Similarity (ML)

**CONFIRMED**

When a new event arrives that does not match existing groups by hash, Sentry calls Loom to check if the stacktrace is
semantically similar to existing groups.

| Property            | Value                                                                    | Confidence                |
|---------------------|--------------------------------------------------------------------------|---------------------------|
| Embedding model     | `jinaai/jina-embeddings-v2-base-en`                                      | CONFIRMED (PR #99873)     |
| Vector dimensions   | 768                                                                      | CONFIRMED (blog)          |
| Storage             | PostgreSQL + pgvector (HNSW index)                                       | CONFIRMED (blog)          |
| Latency             | Sub-100ms end-to-end                                                     | CONFIRMED (blog)          |
| Impact              | 40% reduction in new issues                                              | CONFIRMED (blog)          |
| False positive rate | Near-zero (conservative thresholds)                                      | CONFIRMED (blog)          |
| Quantization        | Full float32 precision (float16/int8/binary evaluated, rejected)         | CONFIRMED (blog)          |
| Query strategy      | Two-stage: permissive HNSW distance -> strict final distance             | CONFIRMED (blog)          |
| Model versioning    | `SEER_SIMILARITY_MODEL_VERSION`, `GroupingVersion` enum                  | CONFIRMED (code)          |
| V2 model            | Rolling out behind `projects:similarity-grouping-v2-model` Flagpole flag | EXPERIMENTAL (PR #102263) |
| Training mode       | `training_mode=True` sends embeddings without grouping decisions         | INFERRED (PR #109539)     |

**GroupHashMetadata fields** (CONFIRMED — code):

| Field                        | Purpose                                   |
|------------------------------|-------------------------------------------|
| `Loom_date_sent`             | When the hash was sent to Loom            |
| `Loom_event_sent`            | Which event's stacktrace was sent         |
| `Loom_model`                 | Model version used                        |
| `Loom_matched_grouphash`     | FK to the matched group's hash            |
| `Loom_match_distance`        | Similarity distance score                 |
| `Loom_latest_training_model` | Tracks latest training-mode model version |

**Filtering criteria** (INFERRED):

- Event must have a stacktrace and a usable title
- Token count filtering replaced frame count filtering (PR #103997)
- Killswitch options: global Loom killswitch + similarity-specific killswitch

---

### 4.2 Issue Summarization

**CONFIRMED**

AI-generated summaries provide headlines, root cause hypotheses, and trace insights.

| Property           | Value                                                          | Confidence           |
|--------------------|----------------------------------------------------------------|----------------------|
| Response schema    | `headline`, `whats_wrong`, `trace`, `possible_cause`, `scores` | CONFIRMED (code)     |
| Cache key          | `ai-group-summary-v2:{group_id}`                               | CONFIRMED (code)     |
| Cache TTL          | 7 days                                                         | CONFIRMED (code)     |
| Locking            | Distributed lock during generation                             | CONFIRMED (code)     |
| Automation trigger | Summary generation can trigger autofix based on fixability     | CONFIRMED (code)     |
| Service            | Routed to dedicated summarization pod                          | INFERRED (PR #97926) |

---

### 4.3 Fixability Scoring

**CONFIRMED**

A GPU-backed service scores each issue on a 0.0-1.0 scale to determine automation behavior.

| Score Range | Label                 | Automation Action         | Confidence       |
|-------------|-----------------------|---------------------------|------------------|
| < 0.25      | `SUPER_LOW`           | No automation             | CONFIRMED (code) |
| 0.25-0.40   | `LOW`                 | Depends on tuning setting | CONFIRMED (code) |
| 0.40-0.66   | `MEDIUM`              | Root cause only           | CONFIRMED (code) |
| 0.66-0.76   | `HIGH`                | Code changes              | CONFIRMED (code) |
| >= 0.78     | `SUPER_HIGH` + buffer | Open PR                   | CONFIRMED (code) |

---

### 4.4 Root Cause Analysis

**CONFIRMED**

The first stage of the autofix pipeline. Loom analyzes the issue, telemetry, and stacktraces to determine the root
cause.

| Property      | Value                                                                                                | Confidence            |
|---------------|------------------------------------------------------------------------------------------------------|-----------------------|
| Input data    | Error messages, stack traces, distributed traces, structured logs, performance profiles, source code | CONFIRMED (docs)      |
| Multi-repo    | Can analyze across multiple linked GitHub repos                                                      | CONFIRMED (docs)      |
| Streaming     | Reasoning is streamed to user in real-time                                                           | CONFIRMED (docs)      |
| User feedback | Thumbs up/down on RCA card, tracked via `Loom.autofix.feedback_submitted`                            | INFERRED (PR #104569) |
| Webhook event | `Loom.root_cause_started`, `Loom.root_cause_completed`                                               | CONFIRMED (code)      |

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

| Level       | Behavior                       |
|-------------|--------------------------------|
| `off`       | Disabled                       |
| `super_low` | Minimal automation             |
| `low`       | Conservative                   |
| `medium`    | Default for seat-based pricing |
| `high`      | Aggressive                     |
| `always`    | Maximum automation             |

**Stopping points** (CONFIRMED — code):

| Value          | Behavior                             |
|----------------|--------------------------------------|
| `code_changes` | Stop after generating code (default) |
| `open_pr`      | Automatically open a GitHub PR       |

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

| Property                          | Value                                                                               | Confidence                             |
|-----------------------------------|-------------------------------------------------------------------------------------|----------------------------------------|
| Trigger: PR opened                | Yes (skips drafts)                                                                  | CONFIRMED (docs)                       |
| Trigger: Draft -> ready           | Yes                                                                                 | CONFIRMED (docs)                       |
| Trigger: New commit on ready PR   | Yes                                                                                 | CONFIRMED (docs)                       |
| Trigger: `@sentry review` comment | Yes                                                                                 | CONFIRMED (docs)                       |
| Trigger: Check run re-request     | Yes                                                                                 | INFERRED (code)                        |
| Data sent to AI                   | File names, code diffs, PR description only                                         | CONFIRMED (docs)                       |
| Output                            | Inline PR comments + GitHub status check                                            | CONFIRMED (docs)                       |
| Status check states               | Success (green), Neutral (yellow), Error (red), Cancelled                           | CONFIRMED (docs)                       |
| Internal codename                 | Loom Prevent                                                                        | INFERRED (code: `SEER_PREVENT_AI_URL`) |
| DB tracking                       | `CodeReviewRun` model: `task_enqueued` -> `Loom_request_sent` -> `succeeded/failed` | INFERRED (PR #108445)                  |
| Retention                         | Cleanup task purges rows older than 90 days                                         | INFERRED (code)                        |
| A/B testing                       | `organizations:code-review-experiments-enabled` flag + per-PR hash-based assignment | EXPERIMENTAL (code)                    |
| SCM support                       | GitHub Cloud only                                                                   | CONFIRMED (docs)                       |

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

| Property              | Value                                                       | Confidence                |
|-----------------------|-------------------------------------------------------------|---------------------------|
| UI entry              | `Cmd+/` in Sentry                                           | CONFIRMED (docs)          |
| Feature flag          | `Loom-explorer`                                             | CONFIRMED (code)          |
| Can trigger autofix   | Yes, from within Explorer context                           | INFERRED (PR #108389)     |
| Screenshot support    | Frontend can pass screenshots for visual context            | INFERRED (PR #99744)      |
| Replay DOM inspection | "Inspect Element" in replay viewer sends DOM to Explorer    | EXPERIMENTAL (PR #108527) |
| Supergroup embeddings | After Explorer generates RCA, triggers supergroup embedding | EXPERIMENTAL (PR #107819) |
| Slack integration     | `@mention` in Slack triggers Explorer runs                  | EXPERIMENTAL              |

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

| Property        | Value                                             | Confidence                      |
|-----------------|---------------------------------------------------|---------------------------------|
| Endpoint        | `/v1/anomaly-detection/store`                     | CONFIRMED (code)                |
| Pool            | `Loom_anomaly_detection_default_connection_pool`  | CONFIRMED (code)                |
| Aggregate types | Counting vs. other alert types distinguished      | INFERRED (PR #107649)           |
| Retry logic     | Retries on transient 503s                         | INFERRED (PRs #105542, #105854) |
| Timeout         | Configurable via `SEER_ANOMALY_DETECTION_TIMEOUT` | CONFIRMED (code)                |

---

### 4.9 Trace Summarization

**CONFIRMED**

AI-generated trace analysis.

| Property     | Value                                                                                                | Confidence       |
|--------------|------------------------------------------------------------------------------------------------------|------------------|
| Endpoint     | `/v1/automation/summarize/trace`                                                                     | CONFIRMED (code) |
| Response     | `trace_id`, `summary`, `key_observations`, `performance_characteristics`, `suggested_investigations` | CONFIRMED (code) |
| Cache key    | `ai-trace-summary:{trace_slug}`                                                                      | CONFIRMED (code) |
| Cache TTL    | 7 days                                                                                               | CONFIRMED (code) |
| Feature flag | `organizations:single-trace-summary`                                                                 | CONFIRMED (code) |
| Service      | Routed to summarization pod                                                                          | CONFIRMED (code) |

---

### 4.10 Assisted Query / Search Agent

**CONFIRMED**

Natural language to Sentry query translation.

| Property           | Value                                      | Confidence       |
|--------------------|--------------------------------------------|------------------|
| Translate endpoint | `/v1/assisted-query/translate`             | CONFIRMED (code) |
| Agentic translate  | `/v1/assisted-query/translate-agentic`     | CONFIRMED (code) |
| Start agent        | `/v1/assisted-query/start`                 | CONFIRMED (code) |
| Get state          | `/v1/assisted-query/state`                 | CONFIRMED (code) |
| Cache              | `/v1/assisted-query/create-cache`          | CONFIRMED (code) |
| Tool types         | Discover tools, Issues tools, Traces tools | CONFIRMED (code) |

---

### 4.11 Test Generation

**EXPERIMENTAL**

Unit test generation service.

| Property | Value                                       | Confidence       |
|----------|---------------------------------------------|------------------|
| Endpoint | `/v1/automation/codegen/unit-tests`         | CONFIRMED (code) |
| Module   | `src/sentry/Loom/services/test_generation/` | CONFIRMED (code) |
| Status   | Present in code, no public documentation    | EXPERIMENTAL     |

---

## 5. Coding Agent Providers

**CONFIRMED** — Loom supports pluggable coding agent backends via a base class hierarchy.

| Provider            | Status         | Feature Flag                                 | Integration Method              | Confidence                                   |
|---------------------|----------------|----------------------------------------------|---------------------------------|----------------------------------------------|
| **Loom (built-in)** | Production     | None (default)                               | Direct via autofix pipeline     | CONFIRMED                                    |
| **Cursor**          | Production     | Graduated (`Loom-coding-agent-integrations`) | Cursor Background Agent API     | CONFIRMED (docs)                             |
| **GitHub Copilot**  | Production     | Graduated                                    | Copilot Coding Agent Tasks API  | INFERRED (PR #108565)                        |
| **Claude Code**     | In Development | `organizations:integrations-claude-code`     | Anthropic Claude Code agent API | EXPERIMENTAL (PRs #109526, #109738, #109750) |

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

| Endpoint                                                  | Method  | Purpose                        | Rate Limit              |
|-----------------------------------------------------------|---------|--------------------------------|-------------------------|
| `/api/0/Loom/models/`                                     | GET     | List active LLM model names    | —                       |
| `/api/0/issues/{issue_id}/autofix/`                       | POST    | Start autofix run              | 25/min user, 100/hr org |
| `/api/0/issues/{issue_id}/autofix/`                       | GET     | Get autofix state              | 1024/min user           |
| `/api/0/issues/{issue_id}/autofix/setup/`                 | GET     | Check autofix prerequisites    | —                       |
| `/api/0/issues/{issue_id}/autofix/update/`                | POST    | Send update to running autofix | —                       |
| `/api/0/issues/{issue_id}/ai-summary/`                    | POST    | Generate AI issue summary      | —                       |
| `/api/0/organizations/{org}/Loom/setup-check/`            | GET     | Check Loom quota/billing       | —                       |
| `/api/0/organizations/{org}/Loom/onboarding-check/`       | GET     | Onboarding status              | —                       |
| `/api/0/organizations/{org}/autofix-automation-settings/` | GET/PUT | Org automation settings        | —                       |
| `/api/0/organizations/{org}/trace-summary/`               | POST    | AI trace summarization         | —                       |
| `/api/0/organizations/{org}/Loom-explorer/chat/`          | POST    | Explorer chat                  | —                       |
| `/api/0/organizations/{org}/Loom-explorer/runs/`          | GET     | List Explorer runs             | —                       |
| `/api/0/organizations/{org}/Loom-explorer/update/`        | POST    | Update Explorer run            | —                       |
| `/api/0/projects/{org}/{project}/Loom-preferences/`       | GET/PUT | Project Loom preferences       | —                       |
| `/api/0/organizations/{org}/search-agent/start/`          | POST    | Start assisted search          | —                       |
| `/api/0/organizations/{org}/search-agent/state/`          | GET     | Get search agent state         | —                       |

### 6.2 Loom Service Endpoints (Sentry -> Loom)

**CONFIRMED** — These are the HTTP paths Sentry calls on the Loom service, extracted from code.

| Path                                                  | Pool              | Purpose                                         |
|-------------------------------------------------------|-------------------|-------------------------------------------------|
| `/v1/automation/autofix/start`                        | Autofix           | Start an autofix run                            |
| `/v1/automation/autofix/update`                       | Autofix           | Update (select root cause, solution, create PR) |
| `/v1/automation/autofix/state`                        | Autofix           | Get autofix state by group_id or run_id         |
| `/v1/automation/autofix/state/pr`                     | Autofix           | Get autofix state by PR ID                      |
| `/v1/automation/autofix/prompt`                       | Autofix           | Get autofix prompt for coding agent handoff     |
| `/v1/automation/autofix/coding-agent/state/update`    | Autofix           | Update coding agent state                       |
| `/v1/automation/autofix/coding-agent/state/set`       | Autofix           | Store coding agent states                       |
| `/v1/automation/summarize/trace`                      | Summarization     | Summarize a trace                               |
| `/v1/automation/summarize/issue`                      | Summarization     | Summarize an issue                              |
| `/v1/automation/summarize/fixability`                 | Scoring (GPU)     | Generate fixability score                       |
| `/v1/automation/explorer/index`                       | Autofix           | Index explorer data                             |
| `/v1/automation/explorer/index/org-project-knowledge` | Autofix           | Index org project knowledge                     |
| `/v1/automation/codegen/unit-tests`                   | Autofix           | Generate unit tests                             |
| `/v1/automation/codegen/pr-review/rerun`              | Code Review       | Rerun PR review                                 |
| `/v1/automation/overwatch-request`                    | Code Review       | Code review request                             |
| `/v1/project-preference`                              | Autofix           | Get project preferences                         |
| `/v1/project-preference/set`                          | Autofix           | Set project preferences                         |
| `/v1/project-preference/bulk`                         | Autofix           | Bulk get preferences                            |
| `/v1/project-preference/bulk-set`                     | Autofix           | Bulk set preferences                            |
| `/v1/project-preference/remove-repository`            | Autofix           | Remove repository                               |
| `/v1/models`                                          | Autofix           | List available models                           |
| `/v1/llm/generate`                                    | Autofix           | Direct LLM generation                           |
| `/v1/assisted-query/translate`                        | Autofix           | Translate NL to query                           |
| `/v1/assisted-query/start`                            | Autofix           | Start search agent                              |
| `/v1/assisted-query/state`                            | Autofix           | Get search agent state                          |
| `/v1/assisted-query/translate-agentic`                | Autofix           | Agentic query translation                       |
| `/v1/assisted-query/create-cache`                     | Autofix           | Create query cache                              |
| `/v1/explorer/service-map/update`                     | Autofix           | Update service map                              |
| `/v0/issues/supergroups`                              | Autofix           | Supergroup embeddings                           |
| `/v1/workflows/compare/cohort`                        | Anomaly Detection | Compare distributions                           |

### 6.3 RPC Bridge (Loom -> Sentry)

**CONFIRMED** — `OrganizationLoomRpcEndpoint` at `/api/0/internal/Loom-rpc/` is HMAC-authenticated.

The RPC bridge exposes dozens of functions for the Loom service to call back into Sentry, including:

| RPC Function Category                           | Confidence |
|-------------------------------------------------|------------|
| Event/span/trace data access (via Snuba protos) | CONFIRMED  |
| Organization and project metadata lookup        | CONFIRMED  |
| Issue occurrence creation (LLM-detected issues) | INFERRED   |
| Repository and code mapping information         | CONFIRMED  |
| Attribute distribution queries                  | INFERRED   |
| Log and metric queries                          | INFERRED   |
| Profile data retrieval                          | INFERRED   |
| Webhook broadcasting to Sentry Apps             | CONFIRMED  |
| Feature flag checks                             | CONFIRMED  |

### 6.4 MCP Server

**CONFIRMED** — Separately maintained at `github.com/getsentry/sentry-mcp`.

| Property         | Value                                                                  |
|------------------|------------------------------------------------------------------------|
| URL              | `https://mcp.sentry.dev/mcp` (hosted, no install required)             |
| Auth             | OAuth 2.0 browser-based flow                                           |
| Self-hosted      | `npx @sentry/mcp-server@latest --access-token=TOKEN` (STDIO)           |
| Tool count       | 16+ tools across Core, Analysis, Advanced categories                   |
| Loom integration | Can trigger Loom analysis, get fix recommendations, monitor fix status |
| Disable Loom     | `MCP_DISABLE_SKILLS=Loom`                                              |
| Status           | "Released for production, however MCP is a developing technology"      |

**Note**: No MCP integration exists *inside* the `getsentry/sentry` codebase. The MCP server is a separate project that
calls Sentry's public API.

---

## 7. Data Layer

### Django Models

**CONFIRMED** — Code-visible models.

**`LoomOrganizationSettings`** (`src/sentry/Loom/models/organization_settings.py`):

| Field                                 | Type                 | Purpose                       |
|---------------------------------------|----------------------|-------------------------------|
| `organization`                        | FK (unique, indexed) | Owning organization           |
| `default_coding_agent`                | CharField            | `"Loom"`, `"cursor"`, or null |
| `default_coding_agent_integration_id` | HybridCloudFK        | FK to Integration             |

**`GroupHashMetadata`** (extended by Loom — see Section 4.1 for fields)

**`CodeReviewRun`** (INFERRED — PR #108445):

- Tracks lifecycle: `task_enqueued` -> `Loom_request_sent` -> `Loom_request_succeeded/failed`

**`CodeReviewEvent`** (INFERRED — PR #108533):

- Rows for the internal PR Review Dashboard

### Migrations

| Migration                              | Purpose                    |
|----------------------------------------|----------------------------|
| `0001_add_Loomorganizationsettings.py` | Creates the settings table |
| `0002_add_default_coding_agent.py`     | Adds coding agent fields   |

### Caching Strategy

**CONFIRMED** — Code-visible cache keys.

| Cache Key Pattern                               | TTL     | Purpose         |
|-------------------------------------------------|---------|-----------------|
| `ai-group-summary-v2:{group_id}`                | 7 days  | Issue summaries |
| `ai-trace-summary:{trace_slug}`                 | 7 days  | Trace summaries |
| `Loom-project-has-repos:{org_id}:{project_id}`  | 15 min  | Repo status     |
| `Loom:seat-based-tier:{org_id}`                 | 4 hours | Pricing tier    |
| `autofix_access_check:{group_id}`               | 1 min   | Access check    |
| `LoomOperatorAutofixCache` (by group_id/run_id) | —       | Operator state  |

### External Storage

**INFERRED** — From the Loom service repository mirror:

| Storage                                             | Purpose                                             |
|-----------------------------------------------------|-----------------------------------------------------|
| PostgreSQL + pgvector                               | Embedding storage, HNSW index for similarity search |
| Google Cloud Storage (`gs://sentry-ml/Loom/models`) | ML model artifacts                                  |
| Langfuse                                            | AI operation tracing/observability                  |

---

## 8. Configuration & Feature Flags

### Django Settings

**CONFIRMED** — Code-visible settings.

| Setting                             | Purpose                                                               |
|-------------------------------------|-----------------------------------------------------------------------|
| `SEER_AUTOFIX_URL`                  | Base URL: autofix, explorer, code review routing, preferences, models |
| `SEER_SUMMARIZATION_URL`            | Base URL: issue + trace summaries                                     |
| `SEER_ANOMALY_DETECTION_URL`        | Base URL: anomaly detection                                           |
| `SEER_GROUPING_URL`                 | Base URL: similarity grouping                                         |
| `SEER_BREAKPOINT_DETECTION_URL`     | Base URL: performance regression                                      |
| `SEER_SCORING_URL`                  | Base URL: GPU fixability scoring                                      |
| `SEER_PREVENT_AI_URL`               | Base URL: code review (Loom Prevent)                                  |
| `SEER_API_SHARED_SECRET`            | HMAC secret for Sentry -> Loom                                        |
| `SEER_RPC_SHARED_SECRET`            | HMAC secret for Loom -> Sentry                                        |
| `SEER_AUTOFIX_GITHUB_APP_USER_ID`   | Loom's GitHub App user ID                                             |
| `SEER_MAX_GROUPING_DISTANCE`        | Max distance threshold for similarity                                 |
| `SEER_ANOMALY_DETECTION_TIMEOUT`    | Timeout for anomaly detection                                         |
| `SEER_BREAKPOINT_DETECTION_TIMEOUT` | Timeout for breakpoint detection                                      |
| `SEER_FIXABILITY_TIMEOUT`           | Timeout for fixability scoring                                        |
| `CLAUDE_CODE_CLIENT_CLASS`          | Dynamic client loading for Claude Code agent                          |

### Feature Flags

**Tags**: `A` = Active, `E` = Experimental, `G` = Graduated (100%), `D` = Deprecated/Dead

| Flag                                                  | Scope   | Purpose                                  | Tag |
|-------------------------------------------------------|---------|------------------------------------------|-----|
| `organizations:gen-ai-features`                       | Org     | **Master gate** for all Loom AI features | A   |
| `organizations:Loom-explorer`                         | Org     | Explorer agent UI                        | A   |
| `organizations:autofix-on-explorer`                   | Org     | Route autofix through Explorer pipeline  | A   |
| `organizations:seat-based-Loom-enabled`               | Org     | Seat-based billing tier                  | A   |
| `organizations:single-trace-summary`                  | Org     | Trace summarization                      | A   |
| `organizations:code-review-experiments-enabled`       | Org     | A/B testing for code review              | A   |
| `organizations:integrations-claude-code`              | Org     | Claude Code coding agent                 | E   |
| `organizations:Loom-slack-workflows-explorer`         | Org     | Slack workflows on Explorer              | E   |
| `projects:similarity-grouping-v2-model`               | Project | V2 grouping model rollout                | E   |
| `projects:supergroup-embeddings-explorer`             | Project | Supergroup embeddings via Explorer       | E   |
| `Loom-explorer`                                       | System  | Explorer feature                         | A   |
| `Loom-slack-explorer`                                 | System  | Slack @mention Explorer                  | E   |
| `pr-review-dashboard`                                 | System  | Internal PR review dashboard             | E   |
| `triage-signals-v0-org`                               | System  | New automation flow                      | A   |
| `Loom-agent-pr-consolidation`                         | System  | Consolidated PR toggle UI                | A   |
| `organizations:Loom-webhooks`                         | Org     | Webhook subscriptions                    | G   |
| `organizations:autofix-Loom-preferences`              | Org     | Project preferences                      | G   |
| `organizations:Loom-coding-agent-integrations`        | Org     | External coding agents                   | G   |
| `organizations:unlimited-auto-triggered-autofix-runs` | Org     | Bypass rate limiting                     | D   |

### Organization Options

| Option                                     | Type | Purpose                               | Confidence |
|--------------------------------------------|------|---------------------------------------|------------|
| `sentry:hide_ai_features`                  | bool | Kill switch for all AI features       | CONFIRMED  |
| `sentry:enable_Loom_coding`                | bool | Enable/disable code changes step      | CONFIRMED  |
| `sentry:default_autofix_automation_tuning` | str  | Default automation tuning for org     | INFERRED   |
| `sentry:auto_open_prs`                     | bool | Default PR creation for new projects  | INFERRED   |
| `sentry:default_automation_handoff`        | str  | Default coding agent for new projects | INFERRED   |

### Project Options

| Option                             | Type | Purpose                                      | Confidence |
|------------------------------------|------|----------------------------------------------|------------|
| `sentry:Loom_scanner_automation`   | bool | Enable Loom scanner for project              | CONFIRMED  |
| `sentry:autofix_automation_tuning` | str  | Tuning: off/super_low/low/medium/high/always | CONFIRMED  |

### Rate Limit Options

| Option                                               | Default                  | Confidence |
|------------------------------------------------------|--------------------------|------------|
| `Loom.max_num_scanner_autotriggered_per_ten_seconds` | 15                       | INFERRED   |
| `Loom.max_num_autofix_autotriggered_per_hour`        | 20 (x tuning multiplier) | INFERRED   |
| `Loom.similarity.circuit-breaker-config`             | —                        | CONFIRMED  |
| `Loom.similarity.grouping-ingest-retries`            | —                        | CONFIRMED  |
| `Loom.similarity.grouping-ingest-timeout`            | —                        | CONFIRMED  |

---

## 9. Integration Points

### GitHub

**CONFIRMED**

| Integration        | Method                                            | Details                               |
|--------------------|---------------------------------------------------|---------------------------------------|
| Source code access | Contents (Read & Write)                           | Fetch files, commits, blame data      |
| PR creation        | Loom GitHub App (`Loom-by-sentry`)                | Separate from main Sentry GitHub App  |
| Code review        | Webhooks (pull_request, issue_comment, check_run) | Posts inline comments + status checks |
| PR tracking        | Webhook handler (`autofix/webhooks.py`)           | Tracks opened/closed/merged analytics |
| Copilot handoff    | Copilot Coding Agent Tasks API                    | Polls task status                     |
| SCM limitation     | **GitHub Cloud only**                             | No GitLab, Bitbucket, Azure DevOps    |
| SCM abstraction    | **In progress** (issue #107469)                   | Unified SCM platform, GitLab explored |

**Required GitHub Permissions** (CONFIRMED — docs):

| Permission      | Level           | Purpose                             |
|-----------------|-----------------|-------------------------------------|
| Administration  | Read-only       | Branch protection, default branches |
| Checks          | Read & Write    | Status checks, code review results  |
| Commit Statuses | Read & Write    | Status integration                  |
| Contents        | Read & Write    | Source files, commits, blame        |
| Issues          | Read & Write    | Linked issue operations             |
| Members         | Read-only (Org) | User mapping                        |
| Pull Requests   | Read & Write    | PR comments, code review            |
| Webhooks        | Read & Write    | Event subscriptions                 |

### Slack

**CONFIRMED** (expanding to EXPERIMENTAL)

| Capability                         | Status         | Confidence   |
|------------------------------------|----------------|--------------|
| Autofix notifications              | Production     | CONFIRMED    |
| Explorer @mention                  | In development | EXPERIMENTAL |
| Diff rendering for autofix results | In development | EXPERIMENTAL |
| Thinking face reaction on mention  | In development | INFERRED     |

### Celery Tasks

**CONFIRMED**

| Task                                  | Queue                 | Purpose                                 | Retries      |
|---------------------------------------|-----------------------|-----------------------------------------|--------------|
| `check_autofix_status`                | `issues_tasks`        | Detect stalled runs (15 min timeout)    | 1            |
| `generate_summary_and_run_automation` | `ingest_errors_tasks` | Summary + automation (post-process)     | 1            |
| `generate_issue_summary_only`         | `ingest_errors_tasks` | Summary without automation              | 3 (3s delay) |
| `run_automation_only_task`            | `ingest_errors_tasks` | Run automation (assumes summary exists) | 1            |
| `configure_Loom_for_existing_org`     | `issues_tasks`        | Onboarding configuration                | 3            |
| `trigger_autofix_from_issue_summary`  | `Loom_tasks`          | Async autofix trigger                   | 1            |
| `process_autofix_updates`             | `Loom_tasks`          | Route updates to entrypoints            | 0            |

### Sentry Apps Webhooks

**CONFIRMED**

Loom broadcasts lifecycle events to installed Sentry Apps:

| Event                              | Confidence |
|------------------------------------|------------|
| `Loom.root_cause_started`          | CONFIRMED  |
| `Loom.root_cause_completed`        | CONFIRMED  |
| `Loom.solution_started`            | CONFIRMED  |
| `Loom.solution_completed`          | CONFIRMED  |
| `Loom.coding_started`              | CONFIRMED  |
| `Loom.coding_completed`            | CONFIRMED  |
| `Loom.pr_created`                  | CONFIRMED  |
| `Loom.impact_assessment_started`   | INFERRED   |
| `Loom.impact_assessment_completed` | INFERRED   |
| `Loom.triage_started`              | INFERRED   |
| `Loom.triage_completed`            | INFERRED   |

---

## 10. Data Privacy & Security

**CONFIRMED** — All from official documentation.

### Core Guarantees

| Guarantee                 | Detail                                                                                              |
|---------------------------|-----------------------------------------------------------------------------------------------------|
| No training by default    | "Sentry does not train generative AI models using your data by default and without your permission" |
| Output confidentiality    | AI output shown only to authorized users in your account                                            |
| Subprocessor restrictions | Contractually prohibited from using customer data for model training                                |
| PII scrubbing             | Applied before any training data inclusion                                                          |
| Deletion propagation      | Deleting service data removes it from ML models                                                     |
| Retention                 | Mirrors underlying service data policies                                                            |

### Data Flow to AI Providers

| Feature         | Data Sent                                                                  | NOT Sent                |
|-----------------|----------------------------------------------------------------------------|-------------------------|
| AI Code Review  | File names, code diffs, PR descriptions                                    | Full repository content |
| Issue Fix / RCA | Error messages, stack traces, traces, logs, profiles, relevant source code | —                       |
| Summarization   | Issue metadata, event context                                              | —                       |

### Controls

| Control                              | Scope                   | Effect                                                  |
|--------------------------------------|-------------------------|---------------------------------------------------------|
| `Show Generative AI Features` toggle | Organization            | Disables all AI features                                |
| Loom settings dropdown               | Organization            | Granular feature control                                |
| `Prevent code generation`            | Organization (Advanced) | Blocks code generation + PR creation, not chat snippets |
| `sentry:hide_ai_features`            | Organization            | Kill switch                                             |
| Data scrubbing tools                 | Project                 | Applied before data transmission                        |

### LLM Provider Configuration (MCP)

| Setting                   | Options                     |
|---------------------------|-----------------------------|
| `EMBEDDED_AGENT_PROVIDER` | `"openai"` or `"anthropic"` |
| `OPENAI_API_KEY`          | For OpenAI provider         |
| `ANTHROPIC_API_KEY`       | For Anthropic provider      |

**CLOSED-SOURCE**: The specific LLM models powering Loom's inference (GPT-4, Claude, etc.) are not publicly disclosed.
The `/api/0/Loom/models/` endpoint exists but response contents are not documented. The MCP server supports both OpenAI
and Anthropic as embedded providers.

---

## 11. Pricing

### Current Model (January 2026+)

**CONFIRMED** — Official documentation, corroborated by January 2026 web announcements.

| Property           | Value                                                      |
|--------------------|------------------------------------------------------------|
| Cost               | $40 per active contributor per month                       |
| Active contributor | Any user making 2+ PRs to a Loom-connected repo in a month |
| Plan availability  | Team, Business, Enterprise, Trial                          |
| Billing            | Separate monthly charge, NOT against PAYG budget           |
| Reset              | Contributor counts reset monthly                           |
| Exclusions         | GitHub bots marked as `[bot]` not counted                  |
| Usage              | Unlimited within subscription                              |
| Trial              | One-time 14-day trial                                      |

### Legacy Model (Pre-January 2026, Being Phased Out)

**CONFIRMED**

| Property    | Value                             |
|-------------|-----------------------------------|
| Base fee    | $20/month per Sentry subscription |
| Included    | $25 worth of Loom event credits   |
| Overages    | Draw from PAYG budget             |
| Issue Scans | $0.003-$0.00219/run (tiered)      |
| Issue Fixes | $1.00/run                         |

**INFERRED** — Seat-based transition managed by `is_Loom_seat_based_tier_enabled()` combining feature flag and billing
flag checks (PR #104290).

---

## 12. Limitations & Known Gaps

Scope: `CONTEXT-ONLY` + `EXTERNAL-CLOSED` — these are external Sentry gaps, not evidence that qyl is missing
functionality.

### Confirmed Limitations

| Limitation              | Detail                                                            | Confidence |
|-------------------------|-------------------------------------------------------------------|------------|
| GitHub Cloud only       | No GitLab, Bitbucket, Azure DevOps, self-hosted GitHub Enterprise | CONFIRMED  |
| Cloud-only              | Self-hosted Sentry instances have no Loom access                  | CONFIRMED  |
| Drafts skipped          | AI Code Review skips PRs in draft state                           | CONFIRMED  |
| No retroactive analysis | Existing issues need manual trigger after Loom enablement         | CONFIRMED  |
| Code generation toggle  | Disabling also blocks PR creation but not chat snippets           | CONFIRMED  |
| MCP maturity            | "MCP is a developing technology and changes should be expected"   | CONFIRMED  |

### Inferred Limitations

| Limitation    | Detail                                                          | Confidence |
|---------------|-----------------------------------------------------------------|------------|
| Age filter    | Issues older than 2 weeks skipped for automation                | INFERRED   |
| Rate limiting | Scanner: 15/10s, Autofix: 20/hr (x tuning), no unlimited bypass | INFERRED   |
| LLM opacity   | Specific models not disclosed publicly                          | INFERRED   |
| Cost scaling  | Active contributor pricing scales with team size, not usage     | CONFIRMED  |

### Known Closed-Source Gaps

| Component           | What We Know                                   | What We Don't                              |
|---------------------|------------------------------------------------|--------------------------------------------|
| LLM inference layer | Multiple models used, likely GPT + Claude      | Exact model names, versions, routing logic |
| Prompt engineering  | `prompts.py` exists in open code               | Actual prompt contents in Loom service     |
| Model training      | jina-embeddings-v2 for grouping                | Training data, fine-tuning process         |
| GPU scoring         | Fixability scores 0.0-1.0                      | Model architecture, training methodology   |
| Code review logic   | "Loom Prevent" codename, request/response flow | Review heuristics, bug prediction model    |

---

## 13. Experimental & Preview Features

Scope: `CONTEXT-ONLY` — external product lifecycle tracking, not a qyl acceptance gate.

### In Active Development (March 2026)

| Feature                   | Evidence                                               | Flag                                      | Confidence   |
|---------------------------|--------------------------------------------------------|-------------------------------------------|--------------|
| **Claude Code Agent**     | 6+ PRs building full integration stack                 | `organizations:integrations-claude-code`  | EXPERIMENTAL |
| **Slack Explorer**        | @mention-based investigation in Slack threads          | `Loom-slack-explorer`                     | EXPERIMENTAL |
| **Replay DOM Inspector**  | "Inspect Element" sends DOM to Explorer                | Multiple open PRs                         | EXPERIMENTAL |
| **Unified SCM Platform**  | Abstraction for Git operations, GitLab explored        | Issue #107469, PR #109468                 | EXPERIMENTAL |
| **PR Review Dashboard**   | Internal tool, "not meant to be released to customers" | `pr-review-dashboard`                     | EXPERIMENTAL |
| **Supergroup Embeddings** | Cross-issue semantic grouping from Explorer RCA        | `projects:supergroup-embeddings-explorer` | EXPERIMENTAL |
| **V2 Grouping Model**     | Improved similarity model                              | `projects:similarity-grouping-v2-model`   | EXPERIMENTAL |
| **Viewer Context**        | Org/user audit context on all Loom API calls           | PRs #109697, #109719                      | EXPERIMENTAL |
| **Structured Logs**       | Loom uses structured logs during debugging             | Marked "beta" in docs                     | EXPERIMENTAL |
| **Context Rule Parsing**  | Auto-parses Cursor/Windsurf/Claude Code config files   | Documented but new                        | EXPERIMENTAL |
| **Test Generation**       | Unit test generation service                           | Code exists, no docs                      | EXPERIMENTAL |

### Recently Graduated (Shipped to 100%)

| Feature                   | Former Flag                                    | Confidence |
|---------------------------|------------------------------------------------|------------|
| Loom Webhooks             | `organizations:Loom-webhooks`                  | CONFIRMED  |
| Project Preferences       | `organizations:autofix-Loom-preferences`       | CONFIRMED  |
| Coding Agent Integrations | `organizations:Loom-coding-agent-integrations` | CONFIRMED  |

### Deprecated / Removed

| Feature                       | Former Flag                                           | Confidence                    |
|-------------------------------|-------------------------------------------------------|-------------------------------|
| Unlimited auto-triggered runs | `organizations:unlimited-auto-triggered-autofix-runs` | CONFIRMED (dead code removed) |

---

## 14. Source Traceability

Every claim in this document can be traced to one of these source categories:

### Primary Sources (CONFIRMED)

| Source                           | URL                                                                                                                    |
|----------------------------------|------------------------------------------------------------------------------------------------------------------------|
| Loom Documentation               | https://docs.sentry.io/product/ai-in-sentry/Loom/                                                                      |
| Issue Fix Documentation          | https://docs.sentry.io/product/ai-in-sentry/Loom/issue-fix/                                                            |
| AI Code Review Documentation     | https://docs.sentry.io/product/ai-in-sentry/Loom/ai-code-review/                                                       |
| AI/ML Data Policy                | https://docs.sentry.io/security-legal-pii/security/ai-ml-policy/                                                       |
| Pricing Documentation            | https://docs.sentry.io/pricing/                                                                                        |
| Loom API Endpoints               | https://docs.sentry.io/api/Loom/                                                                                       |
| MCP Server Documentation         | https://docs.sentry.io/product/sentry-mcp/                                                                             |
| MCP Server Repository            | https://github.com/getsentry/sentry-mcp                                                                                |
| GitHub Integration Docs          | https://docs.sentry.io/organization/integrations/source-code-mgmt/github/                                              |
| Issue Noise Blog Post            | https://blog.sentry.io/how-sentry-decreased-issue-noise-with-ai/                                                       |
| 2026 Sentry Blog Extraction      | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-sentry-blog-seer-debug-with-ai.md`   |
| 2026 Sentry Changelog Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-sentry-changelog-seer-now-debugs.md` |

### Secondary Sources (INFERRED / EXPERIMENTAL)

| Source                                | URL Pattern                                                                                                          |
|---------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| Open PRs                              | https://github.com/getsentry/sentry/pulls?q=Loom+is:open                                                             |
| Closed PRs                            | https://github.com/getsentry/sentry/pulls?q=Loom+is:closed                                                           |
| Loom-labeled Issues                   | https://github.com/getsentry/sentry/issues?q=is:issue+state:open+label:Loom                                          |
| Open-source code                      | `src/sentry/Loom/**` in getsentry/sentry                                                                             |
| Loom service mirror                   | https://github.com/doc-sheet/Loom                                                                                    |
| 2026 Partner Translation Extraction   | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-28-ichizoku-jp-seer-debug-with-ai.md` |
| 2026 News Coverage Extraction         | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-27-techintelpro-seer-expands.md`      |
| 2026 Wire Coverage Extraction         | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-01-28-ai-techpark-business-wire-seer.md` |
| 2026 Third-Party Explainer Extraction | `/Users/ancplua/sentry-seer-sourcepack/sources/seer-web-2026/extracted/2026-02-13-oreateai-seer-copilot.md`          |

### Closed-Source Boundary

The following components are known to exist in the closed `getsentry` repository and cannot be directly inspected:

| Component                      | Evidence of Existence                                        |
|--------------------------------|--------------------------------------------------------------|
| Billing integration            | Settings reference `is_Loom_seat_based_tier_enabled()`       |
| Feature flag definitions       | Flagpole references in open code                             |
| Loom service deployment config | Connection pool URLs, Terraform references                   |
| LLM model selection logic      | `/v1/models` endpoint, `EMBEDDED_AGENT_PROVIDER` setting     |
| Prompt templates (Loom-side)   | RPC bridge provides data, inference runs in Loom             |
| GPU scoring model              | `SEER_SCORING_URL` references, fixability thresholds in code |

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

*Generated via requirements engineering methodology applied to open-source breadcrumbs. The `getsentry/sentry`
repository is the primary evidence source. Claims tagged CLOSED-SOURCE represent acknowledged gaps where implementation
details reside in the proprietary `getsentry` codebase.*

---

## 15. qyl Platform Capabilities Reference

This section consolidates qyl-specific capabilities extracted from internal roadmap and design documents.
Each entry states **what** the capability does, **where** it lives, **what status** it has, and
**how** it relates to the Sentry Loom features described in sections 1–14.

Source documents: `aspire-coverage.md`, `antipattern-remediation.md`, `otel-semconv-reference.md`,
`traced-annotations.md`, `hades-storage-purge.md`, `mcp-platform.md`, `zero-cost-observability.md`,
`ai-chat-analytics.md`, `2026-03-03-qyl-agui-declarative-design.md`, `2026-03-03-qyl-agui-declarative-impl.md`,
`PRINCIPLES.md`, `SCOPE-TAXONOMY.md`.

---

### 15.1 GenAI Semantic Conventions Model

**Scope:** `IMPLEMENTED-IN-QYL`
**Loom Correlation:** Sections 4.2–4.7 (every Loom capability emits spans with these attributes)
**Source:** `otel-semconv-reference.md`

qyl captures, normalises, and stores GenAI telemetry using the OTel Semantic Conventions v1.40.
This is the attribute vocabulary that makes the Loom-like pipelines (autofix, triage, code review,
agent handoff) queryable and observable.

#### Span Types

| Span Type       | Span Name Pattern                         | Key Attributes                                                                                                                                             | qyl Use                                           |
|-----------------|-------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------|
| Inference       | `{gen_ai.system} {gen_ai.operation.name}` | `gen_ai.system`, `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.response.model`, `gen_ai.response.input_tokens`, `gen_ai.response.output_tokens` | Token accounting, cost tracking, latency analysis |
| Embeddings      | `{gen_ai.system} embeddings`              | Same as inference + `gen_ai.request.encoding_formats`                                                                                                      | Vector search instrumentation                     |
| Retrieval       | `{gen_ai.system} retrieve`                | `db.system`, `db.collection.name`                                                                                                                          | RAG pipeline visibility                           |
| Execute Tool    | `execute_tool {gen_ai.tool.name}`         | `gen_ai.tool.name`, `gen_ai.tool.call.id`                                                                                                                  | Tool call sequencing in agent runs                |
| Agent Lifecycle | `{gen_ai.agent.name}`                     | `gen_ai.agent.id`, `gen_ai.agent.name`                                                                                                                     | Agent run timeline reconstruction                 |
| MCP             | `mcp.{method_name}`                       | `mcp.method.name`, `mcp.request.id`, `mcp.session.id`, `mcp.transport`, `mcp.tool.name`                                                                    | MCP tool call tracing                             |

#### Agent Operation Types

| Operation      | Description                               |
|----------------|-------------------------------------------|
| `planning`     | Agent decomposes user goal into sub-tasks |
| `tool_calling` | Agent selects and calls tools             |
| `reflection`   | Agent evaluates its own prior output      |
| `handoff`      | Agent delegates to sub-agent or human     |

#### Events (Log Records on Inference Spans)

| Event Name                 | Content                               | Privacy                |
|----------------------------|---------------------------------------|------------------------|
| `gen_ai.system.message`    | System prompt                         | Opt-in, off by default |
| `gen_ai.user.message`      | User turn (text or multi-modal array) | Opt-in, off by default |
| `gen_ai.assistant.message` | Assistant turn including tool calls   | Opt-in, off by default |
| `gen_ai.tool.message`      | Tool result returned to model         | Opt-in, off by default |
| `gen_ai.choice`            | Single completion choice              | Opt-in, off by default |

#### Metrics (Histograms via OTLP)

| Metric                                        | Unit      | What It Measures                                           |
|-----------------------------------------------|-----------|------------------------------------------------------------|
| `gen_ai.client.token.usage`                   | `{token}` | Input/output tokens per call, split by `gen_ai.token.type` |
| `gen_ai.client.operation.duration`            | `s`       | End-to-end latency of model call                           |
| `gen_ai.client.operation.time_to_first_chunk` | `s`       | Time to first streaming chunk (TTFC)                       |
| `gen_ai.server.time_to_first_token`           | `s`       | Server-side TTFT (requires provider support)               |
| `gen_ai.server.time_per_output_token`         | `s`       | Per-token generation speed (TPOT)                          |

#### DuckDB Storage Mapping

| OTel Field                                      | DuckDB Column            | Table   |
|-------------------------------------------------|--------------------------|---------|
| `gen_ai.system`                                 | `gen_ai_system`          | `spans` |
| `gen_ai.operation.name`                         | `gen_ai_operation`       | `spans` |
| `gen_ai.request.model`                          | `gen_ai_request_model`   | `spans` |
| `gen_ai.response.model`                         | `gen_ai_response_model`  | `spans` |
| `gen_ai.response.input_tokens`                  | `input_tokens`           | `spans` |
| `gen_ai.response.output_tokens`                 | `output_tokens`          | `spans` |
| `gen_ai.agent.id`                               | `agent_id`               | `spans` |
| `mcp.session.id`                                | `mcp_session_id`         | `spans` |
| Provider extensions (`openai.*`, `anthropic.*`) | `attributes` (JSON blob) | `spans` |

#### Provider-Specific Extensions

| Provider  | Extra Attributes                                                                                                       |
|-----------|------------------------------------------------------------------------------------------------------------------------|
| OpenAI    | `openai.request.response_format`, `openai.request.seed`, `openai.response.system_fingerprint`                          |
| Anthropic | `anthropic.request.thinking.enabled`, `anthropic.request.thinking.budget_tokens`, `anthropic.response.thinking_tokens` |

#### Semconv Generation Pipeline

One generator reads `@opentelemetry/semantic-conventions` (npm) and produces typed constants for five targets:

| Output     | File                                                        | Consumer                               |
|------------|-------------------------------------------------------------|----------------------------------------|
| TypeScript | `src/qyl.dashboard/src/lib/semconv.ts`                      | Dashboard attribute keys               |
| C#         | `src/qyl.servicedefaults/.../SemanticConventions.g.cs`      | .NET SDK string constants              |
| C# UTF-8   | `src/qyl.servicedefaults/.../SemanticConventions.Utf8.g.cs` | Collector zero-allocation OTLP parsing |
| TypeSpec   | `core/specs/generated/semconv.g.tsp`                        | API codegen typed models               |
| DuckDB SQL | `src/qyl.collector/Storage/promoted-columns.g.sql`          | Promoted attribute columns             |

Run: `cd eng/semconv && npm run generate` or `nuke Generate --force-generate`.

---

### 15.2 DuckDB Storage — Appender Architecture

**Scope:** `IMPLEMENTED-IN-QYL`
**Loom Correlation:** Section 7 (Data Layer) — qyl's storage equivalent
**Source:** `hades-storage-purge.md`

qyl replaced its Roslyn source generator (`qyl.instrumentation.generators`) for INSERT statement
construction with DuckDB's native Mapped Appender API. This is the hot ingestion path for all
Loom-like telemetry (autofix spans, triage scores, code review events).

#### What Changed

| Before                                                                                 | After                                             |
|----------------------------------------------------------------------------------------|---------------------------------------------------|
| Roslyn generator produced parameterized INSERT SQL                                     | `DuckDBAppenderMap<T>` with direct column mapping |
| `SpanStorageRow` was `partial record` with `[DuckDbTable]`/`[DuckDbColumn]` attributes | Plain `sealed record` with no generator markers   |
| `ulong` → `decimal` cast for UBIGINT columns                                           | Native `ulong` → UBIGINT (DuckDB.NET v1.4.4)      |
| Manual `$1..$N` parameter binding for 26 columns                                       | `appender.AppendRecords(batch.Spans)` one-liner   |

#### Storage Models

**SpanStorageRow** (26 columns):

| Column               | Type              | Nullable | Purpose                            |
|----------------------|-------------------|----------|------------------------------------|
| `SpanId`             | `string`          | No       | Primary key                        |
| `TraceId`            | `string`          | No       | Trace correlation                  |
| `ParentSpanId`       | `string`          | Yes      | Parent span link                   |
| `SessionId`          | `string`          | Yes      | Session grouping                   |
| `Name`               | `string`          | No       | Span name                          |
| `Kind`               | `byte`            | No       | SpanKind enum                      |
| `StartTimeUnixNano`  | `ulong`           | No       | Start timestamp (UBIGINT)          |
| `EndTimeUnixNano`    | `ulong`           | No       | End timestamp (UBIGINT)            |
| `DurationNs`         | `ulong`           | No       | Duration in nanoseconds (UBIGINT)  |
| `StatusCode`         | `byte`            | No       | OTel status code                   |
| `StatusMessage`      | `string`          | Yes      | Error message                      |
| `ServiceName`        | `string`          | Yes      | Resource service.name              |
| `GenAiProviderName`  | `string`          | Yes      | Promoted: gen_ai.system            |
| `GenAiRequestModel`  | `string`          | Yes      | Promoted: gen_ai.request.model     |
| `GenAiResponseModel` | `string`          | Yes      | Promoted: gen_ai.response.model    |
| `GenAiInputTokens`   | `long?`           | Yes      | Promoted: token count in           |
| `GenAiOutputTokens`  | `long?`           | Yes      | Promoted: token count out          |
| `GenAiTemperature`   | `double?`         | Yes      | Promoted: sampling temperature     |
| `GenAiStopReason`    | `string`          | Yes      | Promoted: finish reason            |
| `GenAiToolName`      | `string`          | Yes      | Promoted: tool name                |
| `GenAiToolCallId`    | `string`          | Yes      | Promoted: tool call ID             |
| `GenAiCostUsd`       | `double?`         | Yes      | Computed cost in USD               |
| `AttributesJson`     | `string`          | Yes      | All non-promoted attributes (JSON) |
| `ResourceJson`       | `string`          | Yes      | Resource attributes (JSON)         |
| `BaggageJson`        | `string`          | Yes      | W3C baggage (JSON)                 |
| `SchemaUrl`          | `string`          | Yes      | OTel schema URL                    |
| `CreatedAt`          | `DateTimeOffset?` | Yes      | DuckDB DEFAULT CURRENT_TIMESTAMP   |

**LogStorageRow** (16 columns): `LogId`, `TraceId`, `SpanId`, `SessionId`, `TimeUnixNano`,
`ObservedTimeUnixNano`, `SeverityNumber`, `SeverityText`, `Body`, `ServiceName`, `AttributesJson`,
`ResourceJson`, `SourceFile`, `SourceLine`, `SourceColumn`, `SourceMethod`, `CreatedAt`.

#### ON CONFLICT Handling

DuckDB Appender does not support `ON CONFLICT`. qyl uses pre-filter: check existing span IDs
before append, skip duplicates. OTLP ingestion is idempotent; duplicates are rare.

---

### 15.3 MCP Platform Design

**Scope:** `IMPLEMENTED-IN-QYL` (core features) / remaining items `NOT STARTED`
**Loom Correlation:** Section 6.4 (MCP Server)
**Source:** `mcp-platform.md`

#### Implementation Status

| Feature                                              | Status                                                                         |
|------------------------------------------------------|--------------------------------------------------------------------------------|
| Tool annotations (ReadOnly, Destructive, Idempotent) | DONE — all tools annotated via `[McpServerTool]`                               |
| Skills system (tool grouping + filtering)            | DONE — `QylSkillKind` enum (8 categories), `QYL_SKILLS` env var                |
| Error taxonomy                                       | DONE — `CollectorHelper.ExecuteAsync` categorizes by HTTP status               |
| Constraint scoping                                   | DONE — `QylScope` + `ScopingDelegatingHandler` via `QYL_SERVICE`/`QYL_SESSION` |
| Monolith split (core/cloud/sentry)                   | NOT STARTED                                                                    |
| Streamable HTTP transport                            | NOT STARTED                                                                    |
| OAuth (RFC 9728)                                     | NOT STARTED                                                                    |
| IObservabilityBackend interface                      | NOT STARTED                                                                    |
| Sentry backend adapter                               | NOT STARTED                                                                    |

#### Skill Categories (8)

| Skill Kind   | Tools Included                   | Purpose                                    |
|--------------|----------------------------------|--------------------------------------------|
| `Inspect`    | Telemetry, StructuredLog, Search | Read-only telemetry queries                |
| `Health`     | Storage                          | Collector health and storage status        |
| `Analytics`  | Analytics                        | Aggregation queries (token usage, latency) |
| `Agent`      | Agent, GenAi                     | Agent run inspection and GenAI spans       |
| `Build`      | Build                            | MSBuild failure capture and analysis       |
| `Anomaly`    | Regression, Issue, Triage        | Anomaly detection and issue management     |
| `Copilot`    | Copilot, Workflow                | Copilot integration and workflow execution |
| `ClaudeCode` | Console, Workspace               | Claude Code specific tools                 |

Activation: `QYL_SKILLS=Inspect,Agent,Analytics` enables only those categories.

#### Tool File Inventory (14 files, 60+ methods)

| Tool File               | Skill Kind |
|-------------------------|------------|
| `TelemetryTools.cs`     | Inspect    |
| `StructuredLogTools.cs` | Inspect    |
| `GenAiTools.cs`         | Agent      |
| `AgentTools.cs`         | Agent      |
| `IssueTools.cs`         | Anomaly    |
| `BuildTools.cs`         | Build      |
| `ReplayTools.cs`        | Inspect    |
| `SearchTools.cs`        | Inspect    |
| `WorkflowTools.cs`      | Copilot    |
| `AnalyticsTools.cs`     | Analytics  |
| `StorageTools.cs`       | Health     |
| `ConsoleTools.cs`       | ClaudeCode |
| `CopilotTools.cs`       | Copilot    |
| `WorkspaceTools.cs`     | ClaudeCode |

Plus Loom-specific MCP tools: `AutofixMcpTools.cs`, `TriageTools.cs`, `RegressionTools.cs`,
`GitHubMcpTools.cs`, `AgentHandoffTools.cs`, `AssistedQueryTools.cs`, `TestGenerationTools.cs`.

---

### 15.4 AI Chat Analytics (6 Modules)

**Scope:** `IMPLEMENTED-IN-QYL` (design complete, API endpoints planned)
**Loom Correlation:** Section 4.7 (Explorer) — analytics over the telemetry that Loom-like agents generate
**Source:** `ai-chat-analytics.md`

All 6 modules query the existing `spans` table using `gen_ai.*` attributes. No schema changes needed.

#### Module Overview

| Module                | Purpose                        | Key Query Pattern                                                                                             | MCP Tool                                         |
|-----------------------|--------------------------------|---------------------------------------------------------------------------------------------------------------|--------------------------------------------------|
| **Conversations**     | Browse all AI conversations    | Group spans by `gen_ai.conversation.id` / `session_id` / `trace_id`                                           | `qyl.list_conversations`, `qyl.get_conversation` |
| **Coverage Gaps**     | Identify topics where AI fails | Filter uncertain conversations (errors, timeouts, empty completions, excessive retries), cluster by span name | `qyl.get_coverage_gaps`                          |
| **Top Questions**     | Most common user topics        | Group all conversations by topic, regardless of answer quality                                                | `qyl.get_top_questions`                          |
| **Source Analytics**  | Which knowledge sources matter | Track `gen_ai.data_source.id` citations, identify dead sources                                                | `qyl.get_source_analytics`                       |
| **User Satisfaction** | Feedback trends over time      | Aggregate `qyl.feedback.reaction` (upvote/downvote) + `gen_ai.evaluation.score.value`                         | `qyl.get_satisfaction`                           |
| **User Tracking**     | Individual user journeys       | Group by `enduser.id`, cross-platform identity via conversation linking                                       | `qyl.list_users`, `qyl.get_user_journey`         |

#### Uncertainty Signals (Coverage Gaps Detection)

| Signal             | Detection Method                                                   | Semconv Attribute                  |
|--------------------|--------------------------------------------------------------------|------------------------------------|
| Error responses    | `status_code = 2`                                                  | `error.type`                       |
| High latency       | Duration > 2x median                                               | `gen_ai.client.operation.duration` |
| Tool call failures | Execute-tool spans with error status                               | `gen_ai.tool.name` + `error.type`  |
| Empty completions  | `gen_ai.usage.output_tokens = 0`                                   | `gen_ai.usage.output_tokens`       |
| Excessive retries  | Multiple inference spans with similar prompts in same conversation | `gen_ai.conversation.id` grouping  |
| Token anomalies    | Input+output > 3x median                                           | Token count attributes             |
| Negative feedback  | `qyl.feedback.reaction = "downvote"`                               | `qyl.feedback.reaction`            |

#### Custom qyl Extensions (beyond OTel standard)

| Attribute                 | Type     | Purpose                    |
|---------------------------|----------|----------------------------|
| `qyl.feedback.reaction`   | `string` | `"upvote"` or `"downvote"` |
| `qyl.feedback.comment`    | `string` | User's feedback text       |
| `qyl.feedback.irrelevant` | `bool`   | Marks irrelevant answers   |
| `qyl.feedback.incorrect`  | `bool`   | Marks incorrect answers    |

#### API Endpoints (Phase 2)

| Endpoint                                         | Method |
|--------------------------------------------------|--------|
| `/api/v1/analytics/conversations`                | GET    |
| `/api/v1/analytics/conversations/{id}`           | GET    |
| `/api/v1/analytics/coverage-gaps`                | GET    |
| `/api/v1/analytics/coverage-gaps/{gapId}/status` | PATCH  |
| `/api/v1/analytics/top-questions`                | GET    |
| `/api/v1/analytics/source-analytics`             | GET    |
| `/api/v1/analytics/satisfaction`                 | GET    |
| `/api/v1/analytics/users`                        | GET    |
| `/api/v1/analytics/users/{id}/journey`           | GET    |

#### Future: Semantic Clustering via Embeddings (Phase 5)

| Feature           | Model                        | Reason                                 |
|-------------------|------------------------------|----------------------------------------|
| Coverage Gaps     | `all-mpnet-base-v2`          | Semantic clustering of uncertain spans |
| Top Questions     | `multi-qa-mpnet-base-cos-v1` | Question-to-question similarity        |
| MCP tool queries  | `all-MiniLM-L6-v2`           | Fast embedding at query time           |
| User Satisfaction | None                         | Structured DuckDB aggregation only     |
| User Tracking     | None                         | Structured DuckDB aggregation only     |

---

### 15.5 AG-UI + Declarative Workflows

**Scope:** `IMPLEMENTED-IN-QYL`
**Loom Correlation:** Section 4.7 (Explorer) — qyl's agent runtime
**Source:** `2026-03-03-qyl-agui-declarative-design.md`, `2026-03-03-qyl-agui-declarative-impl.md`

#### Architecture

```
CopilotKit React / Angular / Vanilla JS
       |  AG-UI protocol (SSE)
       v
qyl.collector  MapQylAguiChat("/api/v1/copilot/chat")
       |
       v
QylAgentBuilder -> AIAgent (instrumented)
       |  InstrumentedChatClient (OTel spans + CopilotMetrics)
       v
GitHub Copilot / Azure OpenAI / Ollama / GitHub Models
       |  MCP tools via InstrumentedAIFunction spans
```

#### New Files

| File                              | Project       | Purpose                                                                                        |
|-----------------------------------|---------------|------------------------------------------------------------------------------------------------|
| `Agents/QylAgentBuilder.cs`       | qyl.copilot   | Fluent `AIAgent` factory: wraps any provider with `InstrumentedChatClient`                     |
| `Workflows/DeclarativeEngine.cs`  | qyl.copilot   | Loads `.yaml` AdaptiveDialog workflows, wires `InstrumentedChatClient`, streams `StreamUpdate` |
| `Copilot/CopilotAguiEndpoints.cs` | qyl.collector | Registers `AddAGUI()` + `MapAGUI("/api/v1/copilot/chat", agent)`                               |

#### AG-UI SSE Protocol

```
POST /api/v1/copilot/chat
  Body: { threadId, runId, messages: [{role, content}], context? }

Response (SSE stream):
  data: {"type":"RUN_STARTED","runId":"...","threadId":"..."}
  data: {"type":"TEXT_MESSAGE_START","messageId":"...","role":"assistant"}
  data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":"Hello"}
  data: {"type":"TEXT_MESSAGE_END","messageId":"..."}
  data: {"type":"RUN_FINISHED","runId":"...","threadId":"..."}

Errors: RUN_ERROR event (not HTTP 5xx)
Cancellation: stream closes, no RUN_ERROR
```

#### Packages

| Package                                       | Version                | Used By       |
|-----------------------------------------------|------------------------|---------------|
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | 1.0.0-preview.260225.1 | qyl.collector |
| `Microsoft.Agents.AI.Hosting`                 | 1.0.0-preview.260225.1 | qyl.copilot   |
| `Microsoft.Agents.AI.Workflows.Declarative`   | 1.0.0-rc2              | qyl.copilot   |

---

### 15.6 Compile-Time Tracing Annotations

**Scope:** `IMPLEMENTED-IN-QYL` (core) / stories T-001..T-007 planned
**Loom Correlation:** Instrumentation layer feeding all Loom-like pipelines
**Source:** `traced-annotations.md`

qyl uses Roslyn source-gen interceptors for zero-overhead tracing. No JVM agent, no runtime
reflection — the interceptor is compiled directly into the call site IL.

#### Current Attributes

| Attribute                        | Target          | Purpose                                                                         |
|----------------------------------|-----------------|---------------------------------------------------------------------------------|
| `[Traced("ActivitySourceName")]` | Class or Method | Creates a span around every public method (class-level) or the decorated method |
| `[TracedTag("tag.name")]`        | Parameter       | Records parameter value as span attribute                                       |
| `[NoTrace]`                      | Method          | Opt-out from class-level `[Traced]`                                             |

#### Feature Parity vs Java `@WithSpan`

| Feature              | Java                     | C# (qyl)                                                         |
|----------------------|--------------------------|------------------------------------------------------------------|
| Method-level tracing | `@WithSpan`              | `[Traced("source")]`                                             |
| Class-level tracing  | Not available            | `[Traced]` on class — all public methods traced                  |
| Parameter capture    | `@SpanAttribute`         | `[TracedTag]` with `SkipIfNull`                                  |
| Opt-out              | Config string            | `[NoTrace]` attribute                                            |
| MSBuild toggle       | Not available            | `<QylTraced>false</QylTraced>` disables pipeline                 |
| Async streaming      | Limited                  | `Task`/`ValueTask` supported, `IAsyncEnumerable` planned (T-002) |
| Static methods       | N/A                      | Supported                                                        |
| Overhead             | Runtime bytecode weaving | Zero — direct call in generated IL                               |

#### Planned Stories

| ID    | Feature                                                                 | Priority | Effort |
|-------|-------------------------------------------------------------------------|----------|--------|
| T-003 | Fix exception attributes (`exception.*` instead of `GenAiAttributes.*`) | P0 (bug) | XS     |
| T-001 | `RootSpan = true` — context-breaking spans for background jobs          | P1       | S      |
| T-006 | `SkipIfDefault` — skip value-type defaults (`0`, `Guid.Empty`)          | P1       | S      |
| T-002 | `IAsyncEnumerable<T>` streaming span lifetime                           | P2       | M      |
| T-004 | `[TracedTag]` on properties — instance state as span context            | P2       | M      |
| T-005 | Analyzer diagnostics (QSD001–QSD005) for attribute misuse               | P2       | M      |
| T-007 | `[TracedReturn]` — capture return value as span attribute               | P3       | L      |

#### Generator Pipeline

```
ServiceDefaultsSourceGenerator.cs -> QSG005 -> TracedIntercepts.g.cs
  TracedCallSiteAnalyzer.cs  (finds [Traced] methods, extracts model)
  TracedInterceptorEmitter.cs (emits [InterceptsLocation] interceptors)
  Models.cs (TracedCallSite, TracedTagParameter)
```

---

### 15.7 Zero-Cost Observability Contracts

**Scope:** `IMPLEMENTED-IN-QYL` (Phases 0–2) / Phases 3–5 `NOT STARTED`
**Loom Correlation:** Foundation for selective telemetry collection
**Source:** `zero-cost-observability.md`

#### Concept

Instrumentation exists at zero cost until someone subscribes to it. Compile-time contracts
guarantee schema agreement between producer (interceptor) and consumer (DuckDB appender) before
the first byte flows.

#### Implementation Status

| Phase                           | Status         | What It Does                                                             |
|---------------------------------|----------------|--------------------------------------------------------------------------|
| Phase 0: Subscription Manager   | DONE           | `SubscriptionManager.cs`, `ObserveEndpoints.cs` — `POST /api/v1/observe` |
| Phase 1: Observability Modes    | DONE (partial) | `ObserveCatalog.cs`, `SchemaVersionNegotiator.cs`                        |
| Phase 2: Catalog Endpoint       | DONE           | `GET /api/v1/observe/catalog` — lists available subscriptions            |
| Phase 3: Subscription Contracts | NOT STARTED    | Roslyn generator extension for contract emission                         |
| Phase 4: Schema Negotiation     | NOT STARTED    | Multi-version semconv support                                            |
| Phase 5: Contract Validation    | NOT STARTED    | Startup appender-map verification                                        |

---

### 15.8 Port Architecture (OTLP Standard Compliance)

**Scope:** `IMPLEMENTED-IN-QYL` (design approved, implementation spec ready)
**Loom Correlation:** Infrastructure — how OTLP telemetry reaches the collector
**Source:** `antipattern-remediation.md`

#### Port Layout

| Port | Env Var         | Protocol   | Purpose                                   |
|------|-----------------|------------|-------------------------------------------|
| 5100 | `QYL_PORT`      | HTTP/1.1+2 | Dashboard, REST API, SSE streaming        |
| 4317 | `QYL_GRPC_PORT` | HTTP/2     | OTLP gRPC (standard)                      |
| 4318 | `QYL_OTLP_PORT` | HTTP/1.1+2 | OTLP HTTP (standard) — dedicated listener |

Port 4318 is a dedicated OTLP HTTP listener added for OTel standard compliance.
OTLP HTTP ingestion (`/v1/traces`, `/v1/logs`) remains accessible on both 5100 (legacy)
and 4318 (standard) for backward compatibility. Port 5100 OTLP is deprecated over time.

#### Affected Files (13 total)

`Program.cs`, `MetaResponse.cs`, `StartupBanner.cs`, `Dockerfile`, `docker-compose.yml`,
`eng/compose.yaml`, `compose.yaml`, `CollectorDiscovery.cs`, `Build.cs`, `BuildInfra.cs`,
`OnboardingPage.tsx`, `CLAUDE.md`, `.github/copilot-instructions.md`.

---

### 15.9 Aspire 13.x Feature Coverage

**Scope:** `CONTEXT-ONLY` (competitive positioning, not a Loom feature)
**Loom Correlation:** None directly — platform infrastructure comparison
**Source:** `aspire-coverage.md`

qyl vs Aspire 13.x result: **9 DONE, 5 EXCEEDS, 4 PARTIAL, 12 NOT PRESENT, 12 NOT APPLICABLE**.

#### Where qyl Exceeds Aspire

| Area                 | Aspire 13.x                                                                              | qyl                                                                                                              |
|----------------------|------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| MCP tooling          | 4 tools (`list_integrations`, `get_integration_docs`, `list_apphosts`, `select_apphost`) | 60+ tools across 14 tool files + 7 Loom-specific tool files                                                      |
| GenAI visualization  | Tool definitions + evaluations + audio/video preview                                     | Full agent run dashboard: trace waterfall, token/cost analytics, tool call sequencing                            |
| OTel SDK integration | Bundled OTel SDK version update                                                          | Source-generated compile-time interceptors (`[Traced]`, `[GenAi]`, `[Db]`), GenAI instrumentation, SemConv v1.40 |
| MCP availability     | Requires `aspire mcp init`                                                               | Always available, no init step                                                                                   |
| Telemetry via MCP    | Resource status only                                                                     | Full traces, logs, metrics, agent runs, structured logs                                                          |

#### qyl-Only Features (no Aspire equivalent)

| Feature                                          | Location                                           |
|--------------------------------------------------|----------------------------------------------------|
| Build failure capture (MSBuild binlog → DuckDB)  | `src/qyl.collector/BuildFailures/`                 |
| OTLP PII scrubbing at ingestion boundary         | `src/qyl.collector/Ingestion/`                     |
| Error fingerprinting + GenAI-aware grouping      | `src/qyl.collector/Errors/ErrorFingerprinter.cs`   |
| Autofix orchestration with policy gates          | `src/qyl.collector/Autofix/AutofixOrchestrator.cs` |
| Workflow engine (nodes, checkpoints, routing)    | `src/qyl.collector/Workflow/`                      |
| qyl.watch (live terminal span viewer)            | `src/qyl.watch/`                                   |
| qyl.watchdog (process anomaly detection)         | `src/qyl.watchdog/`                                |
| qyl.browser (Web Vitals + interactions OTLP SDK) | `src/qyl.browser/`                                 |

---

### 15.10 Hosting Resource Model

**Scope:** `IMPLEMENTED-IN-QYL`
**Loom Correlation:** None — platform infrastructure
**Source:** `aspire-coverage.md`

qyl.hosting orchestrates polyglot resources (the development environment where Loom-like
features are used):

| Resource Type                   | File                                             | Covers                                    |
|---------------------------------|--------------------------------------------------|-------------------------------------------|
| `IQylResource` (base interface) | `src/qyl.hosting/Resources/IQylResource.cs`      | All resource types                        |
| `ProjectResource` (.NET)        | `src/qyl.hosting/Resources/ProjectResource.cs`   | .NET projects                             |
| `NodeResource` (JS/Node)        | `src/qyl.hosting/Resources/NodeResource.cs`      | Node.js applications                      |
| `ViteResource` (Vite frontend)  | `src/qyl.hosting/Resources/ViteResource.cs`      | Vite dev servers with HMR                 |
| `PythonResource` (Python)       | `src/qyl.hosting/Resources/PythonResource.cs`    | Python processes                          |
| `UvicornResource` (ASGI)        | `src/qyl.hosting/Resources/PythonResource.cs`    | ASGI applications (FastAPI/Uvicorn)       |
| `ContainerResource` (Docker)    | `src/qyl.hosting/Resources/ContainerResource.cs` | Docker containers                         |
| `QylApp` / `QylAppBuilder`      | `src/qyl.hosting/QylApp.cs`, `QylAppBuilder.cs`  | Entry point builder                       |
| `QylRunner`                     | `src/qyl.hosting/QylRunner.cs`                   | Orchestration loop (start/stop resources) |

### 15.11 Agent Continuation Evaluation (Heuristic-First Pattern)

**Scope:** `IMPLEMENTED-IN-QYL` (PATTERN)
**Loom Correlation:** Section 4.7 (Explorer agentic loops), Section 17.3 (Embedded Agent Framework)
**Source:** `claude-judge-continuation` stop hook architecture decision

**Problem:** Agent evaluation hooks (e.g., "should this conversation continue?") invoke an LLM
evaluator on every turn. ~80% of these calls are trivially decidable without LLM inference,
wasting latency and cost.

**Solution: Two-phase evaluation (Option C)**

Phase 1 — Heuristic filter (eliminates ~80% of LLM evaluator calls):

| Heuristic | Logic | LLM Call? |
|-----------|-------|-----------|
| Last assistant turn is pure text, no tool call pending | Response is complete | Skip |
| Assistant turn ends with question to user | Waiting for input | Skip |
| Last turn = tool result, but assistant already wrote text after it | Already processed | Skip |
| `stop_hook_active = true` AND invocation count ≥ 2 | Already continued twice | Skip |

Phase 2 — Improved evaluator prompt (for the ~20% where LLM judgment is needed):

- **Root cause of false positives:** Prompt fails to distinguish "tool result received but not yet
  processed" vs. "tool result received AND already responded to"
- **Transcript tail window:** When `tail -n 4` truncates the assistant's response that followed
  a tool result, the evaluator sees an "unaddressed" result and incorrectly signals continuation
- **Fix:** Include structured turn metadata (turn type, response status) rather than raw transcript tail

**qyl applicability:** This pattern applies to any agent loop in qyl.copilot where an evaluator
decides continuation — DeclarativeEngine workflow steps, AG-UI streaming evaluation, and
QylAgentBuilder tool-use loops. The heuristic-first approach aligns with the zero-cost observability
principle (Section 15.7): avoid computation when the answer is structurally deterministic.

---

## 16. Sentry Platform Features (Beyond Loom)

Scope: `CONTEXT-ONLY` — Sentry product capabilities visible in public UI and documentation that inform
qyl's feature roadmap. These are not Loom-specific but represent the broader observability platform.

### 16.1 AI Agent Monitoring

**CONFIRMED** — Visible in Sentry UI under Insights > AI Agents.

| Property            | Value                                                                                                          |
|---------------------|----------------------------------------------------------------------------------------------------------------|
| Navigation          | Insights > AI Agents (dedicated tab alongside Frontend, Backend, Mobile)                                       |
| Sub-tabs            | Overview, Models, Tools                                                                                        |
| Traffic chart       | Bar chart of agent invocations over time with error/success breakdown                                          |
| Duration tracking   | Line chart with release markers for deployment context                                                         |
| LLM Calls           | Bar chart aggregating model invocations by model or time window                                                |
| Tool Calls          | Stacked bar showing tool invocation frequency and breakdown                                                    |
| Token tracking      | Total tokens per trace displayed in table                                                                      |
| Cost attribution    | Total cost ($) per trace displayed in table                                                                    |
| Trace table columns | TRACE ID, AGENTS/TRACE ROOT, ROOT DURATION, ERRORS, LLM CALLS, TOOL CALLS, TOTAL TOKENS, TOTAL COST, TIMESTAMP |
| Error correlation   | Issues panel showing top errors affecting agent runs                                                           |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` — `AgentRunsPage.tsx`, `AgentRunDetailPage.tsx` with trace
waterfall, token analytics, tool call sequencing. OTel GenAI semconv v1.40 with `gen_ai.agent.id`,
`gen_ai.agent.name`, evaluation spans.

### 16.2 LLM Monitoring

**CONFIRMED** — Visible in Sentry UI under Insights > Agents (Models tab).

| Property             | Value                                                              |
|----------------------|--------------------------------------------------------------------|
| Model-specific views | Per-model token usage, latency, error rates                        |
| Token accounting     | Input tokens, output tokens, total tokens per request              |
| Cost tracking        | Dollar cost per trace/request                                      |
| Metrics              | `gen_ai.client.token.usage`, `gen_ai.client.operation.duration`    |
| TTFT                 | `gen_ai.server.time_to_first_token` histogram                      |
| Provider support     | OpenAI, Anthropic, custom providers via OTel instrumentation       |
| MCP monitoring       | Dedicated MCP server monitoring (tool executions, resource access) |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` — Full GenAI span schema with promoted DuckDB columns.
Four metric instruments. Provider-specific extensions stored in `span_attributes` JSON.

### 16.3 Session Replay

**CONFIRMED** — Documented at docs.sentry.io, visible in Explore > Replays.

| Property          | Value                                                    |
|-------------------|----------------------------------------------------------|
| Platforms         | Web (GA), Android (GA), iOS (GA), React Native (GA)      |
| Video player      | Embedded session recording with playback controls        |
| Network timeline  | Request waterfall overlay during session                 |
| Breadcrumbs       | Timestamped user actions during session                  |
| Error correlation | Links replays to specific error events                   |
| DOM inspection    | "Inspect Element" in replay viewer sends DOM to Explorer |
| Access control    | Replay access restrictable by user whitelist             |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` (partial) — `qyl.browser` SDK captures Web Vitals and
user interactions. Full session recording not yet implemented.

### 16.4 Uptime & Cron Monitoring

**CONFIRMED** — Visible in Sentry UI under Uptime Monitors and Cron Monitoring.

| Property          | Value                                                       |
|-------------------|-------------------------------------------------------------|
| Uptime monitors   | HTTP endpoint health checks with configurable intervals     |
| Status timeline   | Horizontal bar visualization (green=up, red=down) over time |
| Cron monitoring   | Scheduled job execution tracking with timeout detection     |
| Alert integration | Uptime failures trigger alert rules                         |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` (partial) — `qyl.watchdog` for process anomaly detection.
Span-based job tracking available. Dedicated uptime HTTP checker not yet implemented.

### 16.5 Release Health

**CONFIRMED** — Visible in Explore > Releases.

| Property             | Value                                                  |
|----------------------|--------------------------------------------------------|
| Crash-free rate      | Percentage of crash-free sessions per release          |
| Adoption tracking    | Percentage of sessions on new release over time        |
| Regression detection | Compares error rates against previous release baseline |
| New issues           | Count of new issues introduced per release             |
| Release metadata     | Version, build, package, stage, commit associations    |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` — `RegressionDetectionService.cs` for regressions.
Release-level health via DuckDB queries on `deployment.environment` and `service.version`.

### 16.6 User Feedback

**CONFIRMED** — Visible in Sentry UI as dedicated feedback module.

| Property         | Value                                                    |
|------------------|----------------------------------------------------------|
| Tabs             | Inbox, Resolved, Spam                                    |
| AI summary       | Auto-generated summary of feedback themes                |
| Triage workflow  | Inbox -> Resolved/Spam classification                    |
| OTel integration | `gen_ai.evaluation.score.value`, `qyl.feedback.reaction` |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` (partial) — Evaluation spans and feedback tracking.
Dedicated feedback UI not yet implemented.

### 16.7 Dashboards & Insights

**CONFIRMED** — Documented at docs.sentry.io.

| Feature            | Description                                                  |
|--------------------|--------------------------------------------------------------|
| Widget composition | Multiple chart types on a single dashboard                   |
| Domain templates   | Pre-built for Frontend, Backend, Mobile, AI Agents           |
| Custom dashboards  | User-created with widget library                             |
| Global filters     | Filter all widgets simultaneously (time, project, env)       |
| Insights domains   | Frontend (Web Vitals), Backend (API/DB), Mobile (Frames), AI |
| Common patterns    | Percentile distribution (p50/p75/p90/p99), release markers   |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` (partial) — Feature-specific dashboard pages exist.
Generic widget composition and Insights domain tabs not yet implemented.

### 16.8 Alerts & Notifications

**CONFIRMED** — Documented at docs.sentry.io.

| Property              | Value                                                |
|-----------------------|------------------------------------------------------|
| Alert types           | Issue Alerts, Metric Alerts, Uptime Alerts           |
| Notification channels | Email, Slack, Discord, Jira, Microsoft Teams, Linear |
| Rule builder          | Time window + event count conditions                 |
| Snooze/mute           | Temporary suppression of alert firing                |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` (partial) — SSE streaming for real-time notification.
Full alert rule builder and multi-channel routing not yet implemented.

### 16.9 Logs Explorer

**CONFIRMED** — Visible in Explore > Logs (beta).

| Property             | Value                                                |
|----------------------|------------------------------------------------------|
| Search syntax        | Tag-based query with autocomplete                    |
| Filter chips         | Visual tag chips with expansion                      |
| Aggregation chart    | `countLogs()` histogram showing log volume over time |
| Nested attributes    | Expandable JSON-like attribute display               |
| Column customization | Configurable column visibility                       |

**qyl equivalent**: `IMPLEMENTED-IN-QYL` — OTLP log ingestion, DuckDB `logs` table, log query API.
Dedicated log explorer UI not yet in dashboard.

### 16.10 Pricing Model Detail

**CONFIRMED** — Visible in pricing page.

| Tier        | Price           | Key Features                                           |
|-------------|-----------------|--------------------------------------------------------|
| Developer   | Free            | 5K events/month, 1 project, 1 member                   |
| Team        | $26/mo          | Unlimited events (volume-based), alerts, retention     |
| Business    | $80/mo          | Profiling, Session Replay, SSO, custom domain          |
| Enterprise  | Custom          | Unlimited, SLA, dedicated support, on-premises         |
| Seer add-on | $40/contributor | Per active contributor (2+ PRs/month), unlimited usage |

Event-based pricing with volume tiers. AI features (Seer) as separate per-contributor add-on.

---

## 17. sentry-mcp Architecture

Scope: `CONTEXT-ONLY` — Technical details of `github.com/getsentry/sentry-mcp` (TypeScript monorepo)
informing qyl's MCP implementation.

### 17.1 Tool Catalog (19+ Tools)

**CONFIRMED** — Extracted from `packages/mcp-core/src/tools/`.

| Category             | Tools                                                                                      | Count |
|----------------------|--------------------------------------------------------------------------------------------|-------|
| Data Access          | find_organizations, find_teams, find_projects, find_releases, find_dsns                    | 5     |
| Issue Inspection     | get_issue_details, get_issue_tag_values, search_issues, search_events, search_issue_events | 5     |
| Trace & Performance  | get_trace_details, get_profile, get_event_attachment                                       | 3     |
| Resource Management  | create_project, create_team, create_dsn, update_project, update_issue                      | 5     |
| Utility              | whoami, search_docs, get_doc, analyze_issue_with_seer, get_sentry_resource, use_sentry     | 6     |
| Replacement (no LLM) | list_issues, list_events, list_issue_events                                                | 3     |

Tool definition uses Zod schemas, dynamic descriptions, skill gating, capability checking, and
typed annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`).

**qyl comparison**: qyl.mcp provides 60+ tools (14 files + 7 Loom-specific files) with `[McpServerTool]`
C# attributes. Same annotation pattern (ReadOnly, Destructive, Idempotent), different language.

### 17.2 Skills-Based Authorization

**CONFIRMED** — Extracted from `packages/mcp-core/src/skills.ts`.

| Skill                | Default  | Tool Count | Purpose                                       |
|----------------------|----------|------------|-----------------------------------------------|
| `inspect`            | enabled  | 16         | Search errors, analyze traces, explore events |
| `triage`             | disabled | 5          | Resolve/assign/update issues                  |
| `project-management` | disabled | 5          | Create/modify projects, teams, DSNs           |
| `seer`               | enabled  | 1          | AI root cause analysis                        |
| `docs`               | disabled | 2          | SDK documentation search                      |

Skills bundle related tools into user-facing authorization groups displayed in OAuth consent UI.
`MCP_DISABLE_SKILLS=seer` disables specific skills at runtime.

**qyl comparison**: `QylSkillKind` (8 categories) with `QYL_SKILLS` env var. qyl has more granular
skill categories but same underlying pattern.

### 17.3 Embedded Agent Framework

**CONFIRMED** — Extracted from `packages/mcp-core/src/internal/agents/`.

Three tools (`search_events`, `search_issues`, `search_issue_events`) use internal LLM agents via
Vercel AI SDK to translate natural language → Sentry query syntax. When no LLM provider is configured,
simpler replacement tools are used.

| Property       | Value                                             |
|----------------|---------------------------------------------------|
| AI SDK         | Vercel AI SDK (`generateText()`)                  |
| Providers      | OpenAI or Anthropic (configurable)                |
| Step limit     | `stopWhen: stepCountIs(5)` for agentic loops      |
| Output         | Structured output via `Output.object({ schema })` |
| Error recovery | `NoObjectGeneratedError` rescue parses raw text   |

### 17.4 Dual OAuth Architecture

**CONFIRMED** — Documented in `docs/cloudflare/oauth-architecture.md`.

```
Client -> MCP OAuth (Cloudflare) -> Sentry OAuth -> User authenticates
                                 <- Sentry tokens wrapped in encrypted MCP token
Client <- MCP access token (contains encrypted Sentry tokens)
```

Stateless per-request architecture. `@cloudflare/workers-oauth-provider` for MCP OAuth.
Token refresh cascades: MCP checks Sentry token expiry, refreshes both if needed.

### 17.5 Deployment Models

**CONFIRMED** — Two deployment modes.

| Mode   | Transport    | Auth                      | Platform            |
|--------|--------------|---------------------------|---------------------|
| Remote | HTTP (Hono)  | OAuth 2.0 browser flow    | Cloudflare Workers  |
| Stdio  | stdin/stdout | Access token via CLI flag | Node.js npm package |

**qyl comparison**: qyl.mcp uses stdio. Planned: Streamable HTTP for Azure Container Apps.

### 17.6 Error Handling & Rate Limiting

**CONFIRMED** — Custom error type hierarchy.

| Error Class          | Scope    | User Message                               |
|----------------------|----------|--------------------------------------------|
| `UserInputError`     | User     | Fixable by user (bad query, missing param) |
| `ConfigurationError` | Operator | Missing API key, wrong setup               |
| `LLMProviderError`   | AI       | Provider issue (rate limit, model error)   |
| `ApiClientError`     | 4xx      | Bad request to Sentry API                  |
| `ApiServerError`     | 5xx      | Transient Sentry API failure               |

Rate limiting: SHA-256 hashes identifiers before rate checking. Fails open if limiter unavailable.

### 17.7 Constraints & Capabilities

**CONFIRMED** — URL-based org/project scoping.

URL pattern `/mcp/:org?/:project?` extracts constraints. `verifyConstraintsAccess()` validates
user access with KV cache (15-min TTL). Tools requiring unavailable capabilities are hidden.

| Capability     | Purpose                          |
|----------------|----------------------------------|
| `hasSeer`      | AI root cause analysis available |
| `hasProfiling` | Profiling data available         |
| `hasReplays`   | Session replay available         |
| `hasTracing`   | Distributed tracing available    |

### 17.8 Technology Stack

| Layer         | Technology                          |
|---------------|-------------------------------------|
| Language      | TypeScript, Node.js >= 20           |
| MCP SDK       | `@modelcontextprotocol/sdk` ^1.26.0 |
| Web Framework | Hono (HTTP), Cloudflare Workers     |
| AI SDK        | Vercel AI SDK (generateText)        |
| Validation    | Zod schemas                         |
| Logging       | LogTape + @sentry/core              |
| Auth          | @cloudflare/workers-oauth-provider  |
| Testing       | Vitest, evaluation-driven testing   |

---

## 18. Competitive Analysis

### 18.1 qodo-skills (Multi-Provider Code Review)

Scope: `CONTEXT-ONLY` — Reference for multi-SCM patterns.

qodo-skills (`github.com/qodo-ai/qodo-skills`) provides shift-left code review via Agent Skills:

| Skill              | Purpose                                                   | Multi-Provider                          |
|--------------------|-----------------------------------------------------------|-----------------------------------------|
| `qodo-get-rules`   | Hierarchical coding rules (universal/org/repo/path scope) | N/A                                     |
| `qodo-pr-resolver` | Fix PR review issues, reply to inline comments            | GitHub, GitLab, Bitbucket, Azure DevOps |

**Key patterns:**

- **Hierarchical rule scoping**: Universal -> Org -> Repo -> Path, with severity enforcement
  (ERROR = must, WARNING = should, RECOMMENDATION = consider)
- **Multi-provider SCM**: Single workflow supports 4 Git providers via CLI detection
- **Agent Skills Standard**: SKILL.md with YAML frontmatter, compatible with Claude Code, Cursor,
  Windsurf, Cline
- **Contrast with Sentry**: Sentry Code Review is **GitHub Cloud only**. qodo demonstrates the
  multi-provider pattern Sentry lacks (Section 4.6, Section 12).

### 18.2 Feature Gap Matrix (Sentry vs. qyl)

| Feature                      | Sentry (Loom/Seer)           | qyl                       | Status        |
|------------------------------|------------------------------|---------------------------|---------------|
| Issue Grouping (ML)          | jina-embeddings-v2, pgvector | ErrorFingerprinter        | gap           |
| Issue Summarization          | GPU pod, 7d cache            | DuckDB + IChatClient      | partial       |
| Fixability Scoring           | GPU pod, 5-tier              | TriagePipelineService     | partial       |
| Root Cause Analysis          | 5-whys, multi-repo           | AutofixAgentService       | partial       |
| Autofix Pipeline             | 5-stage, 30min timeout       | AutofixOrchestrator       | partial       |
| AI Code Review               | GitHub only                  | CodeReviewService         | partial       |
| Explorer                     | Multi-turn, file editing     | AG-UI + Copilot           | partial       |
| Anomaly Detection            | 28-day baseline              | qyl.watchdog              | partial       |
| Trace Summarization          | Dedicated pod                | DuckDB queries            | partial       |
| Assisted Query               | NL -> Sentry syntax          | AssistedQueryTools.cs     | partial       |
| Test Generation              | PR-driven                    | TestGenerationTools.cs    | partial       |
| AI Agent Monitoring          | Insights > AI Agents         | AgentRunsPage             | parity        |
| LLM Monitoring               | Token/cost per trace         | GenAI semconv v1.40       | parity        |
| Session Replay               | Web + Mobile (GA)            | qyl.browser (partial)     | gap           |
| Profiling                    | Continuous, flame graphs     | —                         | NOT-PLANNED   |
| Custom Dashboards            | Widget composition           | Feature-specific pages    | gap           |
| Multi-Channel Alerts         | Email, Slack, Discord...     | SSE streaming             | gap           |
| Release Health               | Crash-free %, adoption       | RegressionDetection       | partial       |
| Cron Monitoring              | Scheduled job tracking       | —                         | gap           |
| User Feedback                | Inbox/Resolve/Spam           | Evaluation spans          | gap           |
| Multi-SCM Support            | GitHub Cloud only            | GitHub only               | tied          |
| MCP Tools                    | 19+ tools, 5 skills          | 60+ tools, 8 skills       | **qyl leads** |
| AG-UI Protocol               | —                            | SSE + CopilotKit          | **qyl only**  |
| Declarative Workflows        | —                            | YAML AdaptiveDialog       | **qyl only**  |
| Zero-Cost Observability      | —                            | Subscription Manager      | **qyl only**  |
| Compile-Time Instrumentation | —                            | [Traced], [GenAi], [Db]   | **qyl only**  |
| Build Intelligence           | —                            | MSBuild binlog capture    | **qyl only**  |
| Multi-Language Hosting       | —                            | qyl.hosting (6 resources) | **qyl only**  |
| OTel Semconv Pipeline        | —                            | 5-target codegen          | **qyl only**  |

---

## 19. Development Principles

Scope: `IMPLEMENTED-IN-QYL` (POLICY) — Engineering governance from `docs/decisions/PRINCIPLES.md`.

| #  | Principle                                          | Rationale                                                                  |
|----|----------------------------------------------------|----------------------------------------------------------------------------|
| 1  | Avoid short fixes, prioritize long-run correctness | Telemetry debt is invisible until queries break months later               |
| 2  | Call out bad assumptions plainly                   | "This span always exists" is the #1 cause of corrupted telemetry           |
| 3  | Plan first, checkpoints, no partial refactors      | Telemetry changes cross protocol -> storage -> query -> dashboard -> tests |
| 4  | Consider the entire schema, not just AI            | qyl is a signal processing system, not an "AI feature"                     |
| 5  | If you can't root-cause, stop — don't suppress     | Suppressing diagnostics in an observability platform is self-sabotage      |
| 6  | Map current vs. proposed pipeline                  | Ambiguous changes to ingest -> normalize -> store -> query are risky       |
| 7  | Use explicit scope labels in design docs           | `IMPLEMENTED-IN-QYL`, `CONTEXT-ONLY`, `EXTERNAL-CLOSED`, `NOT-PLANNED`     |
| 8  | Keep commands minimal                              | Fewer entry points = fewer accidental modes; prefer NUKE orchestration     |
| 9  | Prefer Playwright MCP over more tests              | E2E signal validation catches failures unit tests never will               |
| 10 | No suppression — no `#pragma warning disable`      | Enforced by ANcpLua.Analyzers; fix diagnostics, don't hide them            |
| 11 | Don't bypass NUKE                                  | NUKE encodes generation order, test ordering, artifact production          |

---

## 20. Seer AI Platform — Deep Architecture

**Scope:** `CONTEXT-ONLY` | **Confidence:** `CONFIRMED` (open-source) + `INFERRED` (sourcepack analysis)
**Source:** `sentry-seer-sourcepack/` — 115+ Python modules reverse-engineered

This section extends the capabilities catalog (§4) with implementation-level detail
extracted from Sentry's Seer codebase. Where §4 describes *what* Seer does, §20 describes *how*.

### 20.1 Autofix Pipeline — 5-Step LLM Chain

| Step                   | Artifact                   | Key Fields                                                                           | LLM Prompt                                                  |
|------------------------|----------------------------|--------------------------------------------------------------------------------------|-------------------------------------------------------------|
| 1. Root Cause Analysis | `RootCauseArtifact`        | `one_line_description`, `five_whys[]`, `reproduction_steps[]`                        | `root_cause_prompt(short_id, title, culprit, artifact_key)` |
| 2. Solution Design     | `SolutionArtifact`         | `one_line_summary`, `steps[{title, description}]`                                    | `solution_prompt(...)`                                      |
| 3. Code Implementation | `FileChange[]`             | `path`, `content`, `is_deleted`                                                      | `code_changes_prompt(...)` — uses coding agent tools        |
| 4. Impact Assessment   | `ImpactAssessmentArtifact` | `overall_description`, `impacts[{label, rating, evidence}]`                          | `impact_assessment_prompt(...)`                             |
| 5. Triage              | `TriageArtifact`           | `suspect_commit{sha, repo, message, author}`, `suggested_assignee{name, email, why}` | `triage_prompt(...)`                                        |

**Data Gathering** (pre-pipeline, 15s timeout per source):

| Source        | Volume                     | Retrieval                                                                                         |
|---------------|----------------------------|---------------------------------------------------------------------------------------------------|
| Trace Tree    | Full distributed trace     | `get_trace_waterfall()`                                                                           |
| Profiles      | Execution trees            | `get_profile_flamegraph()` (continuous/sampled)                                                   |
| Logs          | 80 before + 20 after error | Merged by message/severity                                                                        |
| Tags Overview | Top 3 values per tag       | Excludes: release, browser.name, level, replay_id, mechanism, os.name, runtime.name, device.class |

**Stopping Points** (auto-run control):

```
ROOT_CAUSE → SOLUTION → CODE_CHANGES → OPEN_PR
```

| Fixability Level | Score Threshold      | Auto-run Proceeds To |
|------------------|----------------------|----------------------|
| Medium           | ≥ 0.50               | ROOT_CAUSE           |
| High             | ≥ 0.65               | SOLUTION             |
| Super-High       | ≥ 0.76 + 0.02 buffer | CODE_CHANGES         |

Auto-run sources: `issue_details`, `alert`, `post_process`. User preferences act as upper bound.

**Billing:** Quota per org/project via `DataCategory.SEER_AUTOFIX`.

### 20.2 Explorer Agent — Interactive Debugging

Multi-tool agentic system for interactive issue investigation.

**Client interface:**

```
SeerExplorerClient(organization, user, project?,
    category_key/value, custom_tools[],
    on_completion_hook, intelligence_level="low|medium|high",
    is_interactive=False, enable_coding=False)
```

**Built-in tools (10):**

| Tool                                      | Purpose                                   |
|-------------------------------------------|-------------------------------------------|
| `execute_table_query`                     | Query events/spans/transactions via Snuba |
| `execute_timeseries_query`                | Time series analysis                      |
| `execute_trace_table_query`               | Trace-specific queries                    |
| `execute_issues_query`                    | Cross-project issue searches              |
| `get_issue_and_event_details_v2`          | Full event context                        |
| `get_trace_waterfall`                     | Trace visualization                       |
| `get_comparative_attribute_distributions` | Comparative span analysis                 |
| `get_profile_flamegraph`                  | Profiling data                            |
| `get_log_attributes_for_trace`            | Log filtering                             |
| `get_repository_definition`               | Code repo metadata                        |

**Extensibility:**

- Custom tools via `ExplorerTool[ParamsModel]` base class with `get_description()` + `execute()`
- On-completion hooks via `ExplorerOnCompletionHook.execute(org, run_id)`
- Coding agent handoff: Cursor, GitHub Copilot (launches external agents, polls status, extracts PR URLs via GitHub
  GraphQL)

### 20.3 Code Review — Bug Prediction

GitHub PR analysis with severity-gated comments.

| Trigger             | Event                         |
|---------------------|-------------------------------|
| PR ready for review | Automatic analysis            |
| New commits pushed  | Re-analysis                   |
| `@bot` command      | On-demand                     |
| PR closed           | Post-merge metrics collection |

Comment severity: Critical > High > Medium > Low. Minimum severity per feature (default: Medium).
Models: `BugPredictionSpecificInformation`, `SeerCodeReviewConfig`, `SeerCodeReviewRequestForPrReview`.

### 20.4 Anomaly Detection — Prophet-Like Forecasting

| Field                  | Values                                                |
|------------------------|-------------------------------------------------------|
| `time_period`          | Minutes                                               |
| `sensitivity`          | `low` / `medium` / `high`                             |
| `direction`            | `above` / `below` / `above_and_below`                 |
| `expected_seasonality` | `auto` / `hourly` / `daily` / `weekly` / combinations |
| `aggregate`            | `count` / other                                       |

Requirements: Minimum 7 days of historical data, timestamp-aligned buckets.
Response annotations: `high_confidence` / `low_confidence` / `none` / `no_data` with forecast
confidence bounds (`yhat_lower`, `yhat_upper`).

### 20.5 Issue Similarity — Embeddings V1/V2

| Version              | Status              | Feature Flag                                 |
|----------------------|---------------------|----------------------------------------------|
| `GroupingVersion.V1` | Stable (current)    | —                                            |
| `GroupingVersion.V2` | Rollout in progress | `projects:similarity-grouping-model-upgrade` |

Dual-write during rollout. Circuit breaker prevents cascading failures.
Response: `SeerSimilarIssueData[]` sorted by descending similarity with `should_group` decision.

### 20.6 Breakpoint Detection — Trend Analysis

Not in §4 — new capability. Statistical t-test comparing pre/post breakpoint periods.

**Input:**
`BreakpointRequest{data: {transaction_name: BreakpointTransaction}, sort?, allow_midpoint, validate_tail_hours, trend_percentage, min_change}`.

**Output:**
`BreakpointData{project, transaction, aggregate_range_1/2, unweighted_t_value, unweighted_p_value, trend_percentage, absolute_percentage_change, breakpoint (unix timestamp)}`.

### 20.7 Fetch Issues — Context Retrieval

Not in §4 — new capability.

| Method                                      | Purpose                 |
|---------------------------------------------|-------------------------|
| `by_text_query.fetch_issues(text)`          | Full-text search        |
| `by_function_name.fetch_issues(org_id, fn)` | Stack trace matching    |
| `by_error_type.fetch_issues(org_id, err)`   | Exception type matching |

Returns `SeerResponse{issues: int[], issues_full: IssueDetails[]}`.

### 20.8 RPC Method Registry

30+ read-only methods exposed to Seer from Sentry core.

**Organization-scoped:**

| Category            | Methods                                                                                                                                                                                                                                                                                                                                                                         |
|---------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Project enumeration | `get_organization_project_ids`, `get_organization_slug`                                                                                                                                                                                                                                                                                                                         |
| Bug prediction      | `has_repo_code_mappings`, `get_issues_by_function_name`, `get_issues_related_to_exception_type`, `get_issues_by_raw_query`, `get_latest_issue_event`                                                                                                                                                                                                                            |
| Assisted query      | `get_attribute_names`, `get_attribute_values_with_substring`, `get_attributes_and_values`, `get_event_filter_keys`, `get_event_filter_key_values`, `get_issue_filter_keys`, `get_filter_key_values`                                                                                                                                                                             |
| Explorer            | `get_trace_waterfall`, `get_repository_definition`, `execute_table_query`, `execute_timeseries_query`, `execute_trace_table_query`, `execute_issues_query`, `get_issue_and_event_details_v2`, `get_profile_flamegraph`, `get_replay_metadata`, `get_log_attributes_for_trace`, `get_metric_attributes_for_trace`, `get_issues_stats`, `get_comparative_attribute_distributions` |

**Project-scoped:**

| Category | Methods                                                                                                             |
|----------|---------------------------------------------------------------------------------------------------------------------|
| Explorer | `get_transactions_for_project`, `get_trace_for_transaction`, `get_profiles_for_trace`, `get_issues_for_transaction` |
| Autofix  | `get_error_event_details`, `get_profile_details`, `get_attributes_for_span`, `get_trace_item_attributes`            |
| Replays  | `get_replay_summary_logs`                                                                                           |

### 20.9 Seer API Endpoints

| Endpoint                                           | Method   | Purpose                         |
|----------------------------------------------------|----------|---------------------------------|
| `/organizations/{org}/seer-explorer/runs`          | GET      | List explorer runs (paginated)  |
| `/organizations/{org}/events-anomalies`            | GET/POST | Anomaly detection queries       |
| `/organizations/{org}/seer-explorer/chat`          | POST     | Interactive explorer chat       |
| `/organizations/{org}/seer-explorer/pr-groups`     | GET      | PR grouping results             |
| `/organizations/{org}/seer-explorer/update`        | POST     | Update explorer run state       |
| `/organizations/{org}/trace-summary`               | POST     | Summarize traces                |
| `/organizations/{org}/autofix-automation-settings` | GET/PUT  | Automation preferences          |
| `/organizations/{org}/seer-rpc`                    | POST     | RPC method dispatch (read-only) |
| `/organizations/{org}/seer-onboarding-check`       | GET      | Feature enablement check        |
| `/projects/{org}/{project}/seer-preferences`       | GET/PUT  | Repo & automation config        |
| `/groups/{group_id}/ai-autofix`                    | POST/GET | Trigger/check autofix           |
| `/groups/{group_id}/ai-summary`                    | POST     | Quick issue summary             |
| `/groups/{group_id}/autofix-update`                | POST     | Update autofix selection        |
| `/groups/{group_id}/autofix-setup-check`           | GET      | Autofix readiness               |

### 20.10 Seer Feature Flags

| Flag                                              | Purpose                    |
|---------------------------------------------------|----------------------------|
| `organizations:gen-ai-features`                   | Master switch for all Seer |
| `organizations:autofix-on-explorer`               | Use new Explorer pipeline  |
| `organizations:integrations-github-copilot-agent` | GitHub Copilot integration |
| `organizations:single-trace-summary`              | Trace summarization        |
| `organizations:seer-slack-workflows-explorer`     | Slack entrypoint           |
| `organizations:similarity-grouping-model-upgrade` | V2 embeddings rollout      |

Org options: `sentry:hide_ai_features` (user opt-out), `sentry:enable_seer_coding` (code generation).

### 20.11 Seer Data Models

```
AutofixRequest:
  organization_id, project_id: int
  issue: AutofixIssue {id, title, short_id}
  repos: SeerRepoDefinition[]

SeerRepoDefinition:
  provider: "github" | "github_enterprise"
  owner, name, external_id: str
  branch_name, instructions, base_commit_sha: str?
  branch_overrides: BranchOverride[]

SummarizeIssueResponse:
  group_id, headline: str
  whats_wrong, trace, possible_cause: str?
  scores: {possible_cause_confidence, possible_cause_novelty: float,
           is_fixable: bool, fixability_score: float(0-1),
           fixability_score_version: int}

EAPTrace:
  trace_id: str, org_id: int?, trace: list[dict]

ProfileData:
  profile_id: str, transaction_name: str?
  execution_tree: ExecutionTreeNode[]
  project_id: int, start_ts/end_ts: float?
  is_continuous: bool

ExecutionTreeNode:
  function, module, filename: str
  lineno: int, in_app: bool
  children: ExecutionTreeNode[]
  sample_count, first_seen_ns, last_seen_ns, duration_ns
```

### 20.12 Seer Entrypoints & Background Tasks

**Entrypoint Registry:** Extensible via `entrypoint_registry.registrations`. Current: Slack workflows.

**SeerOperator:**

- `has_access(organization)` — gate check
- `can_trigger_autofix(group)` — capability check
- `trigger_autofix(group, user, stopping_point)` — launch pipeline
- `trigger_autofix_explorer(...)` — launch explorer-based autofix

**Celery Tasks:**

| Task                                                   | Purpose                                     |
|--------------------------------------------------------|---------------------------------------------|
| `check_autofix_status(run_id, org_id)`                 | Poll Seer status after 15min                |
| `trigger_autofix_from_issue_summary(...)`              | Auto-triggered by fixability score          |
| `update_coding_agent_state(...)`                       | Poll external agent (Cursor/Copilot) status |
| `process_autofix_updates(event_type, payload, org_id)` | SentryApp webhook processing                |

**Caching:**

| Cache               | TTL             | Key Pattern                            |
|---------------------|-----------------|----------------------------------------|
| Trace summary       | 7 days          | `ai-trace-summary:{trace_slug}`        |
| Autofix state       | 5 minutes       | `SeerOperatorAutofixCache` (in-memory) |
| Project preferences | Decorator-based | `@cache_get`/`@cache_set`              |

### 20.13 Seer Error Handling & Monitoring

**Exceptions:** `SeerApiError(message, status)`, `SeerApiResponseValidationError`, `SeerPermissionError`.

**Circuit breaker** for similarity requests: `CircuitBreaker(key, config)`.

**Metrics:**

- `seer.similar_issues_request` (with tags)
- `anomaly_detection_alert.created`
- `ai.autofix.pr.{action}`

---

## 21. qyl Roadmap — Extended Feature Reference

**Scope:** `IMPLEMENTED-IN-QYL` (unless noted)
**Source:** Cross-referenced from `docs/roadmap/*.md` and `docs/plans/*.md`

Features from qyl roadmap documents that extend or detail capabilities beyond §15.

### 21.1 AI Chat Analytics — Extended (from `ai-chat-analytics.md`)

Features beyond §15.4:

| Feature                       | Description                                                                                                                                                        |
|-------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Semantic clustering (Phase 5) | Embedding models (all-mpnet-base-v2, multi-qa-mpnet-base-cos-v1, all-MiniLM-L6-v2) for dynamic topic clustering with silhouette score and cluster drift monitoring |
| AI self-improvement loop      | Coverage gaps tool enables copilot to identify documentation gaps autonomously via MCP                                                                             |
| Cross-platform tracking       | Unified analytics across GitHub Copilot Extensions, MCP agents, qyl.browser widget, Claude Code CLI via standard OTLP                                              |
| Token economics               | Per-conversation, per-user, per-topic cost modeling using `gen_ai.client.token.usage`                                                                              |
| Feedback reactions            | Upvote/downvote + binary flags (irrelevant, incorrect) stored as OTel span events with `qyl.feedback.*` attributes                                                 |
| User journey tracking         | Individual user conversation timelines, topic affinity, satisfaction trends, retention day calculation                                                             |
| Topic-based satisfaction      | Satisfaction rate per topic, identifying where AI fails by category                                                                                                |
| Conversation de-duplication   | Semantic similarity exclusion to filter redundant questions before analytics                                                                                       |
| Claude Code integration       | Native OTEL metrics/logs when API keys available + env var configuration                                                                                           |
| Evaluation spans              | `gen_ai.evaluation.score.value` + `gen_ai.evaluation.name` attributes for online/offline grading                                                                   |

### 21.2 Port Architecture — Extended (from `antipattern-remediation.md`)

Features beyond §15.8:

| Feature                       | Description                                                                            |
|-------------------------------|----------------------------------------------------------------------------------------|
| Port 4318 OTLP HTTP listener  | Dedicated HTTP/1.1+2 port for OTLP ingestion, separate from dashboard port 5100        |
| Port disambiguation           | 5100 (dashboard + REST + SSE), 4317 (gRPC), 4318 (HTTP OTLP)                           |
| Dynamic onboarding wizard     | `GET /api/v1/meta` returns actual collector ports at runtime                           |
| Env-var-first onboarding      | Universal `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318` before SDK-specific code |
| Worker/console app path       | `Host.CreateApplicationBuilder` pattern for non-web .NET apps                          |
| Deprecation warnings          | Log warnings when OTLP requests arrive on legacy port 5100                             |
| CollectorDiscovery multi-port | Probe targets include 4317 (gRPC) and 4318 (HTTP) with service name resolution         |

### 21.3 DuckDB Appender Purge (from `hades-storage-purge.md`)

Features beyond §15.2:

| Feature                                  | Description                                                                     |
|------------------------------------------|---------------------------------------------------------------------------------|
| DuckDB Appender-based bulk insert        | Replaces `DuckDbInsertGenerator` Roslyn project entirely                        |
| `SpanStorageRowMap` + `LogStorageRowMap` | `DuckDBAppenderMap<T>` subclasses mapping record properties to column names     |
| Zero generator-marker records            | No `@DuckDbTable`, `@DuckDbColumn`, `partial` keywords post-purge               |
| Pre-filter deduplication                 | `GetExistingSpanIds()` check before bulk append for idempotent ingestion        |
| `ulong` → `UBIGINT` native support       | DuckDB.NET v1.4.4, no decimal-cast workaround                                   |
| Removal of column count constants        | Appender self-discovers column count                                            |
| No `ON CONFLICT` with appender           | Requires separate pre-filter logic (no UPSERT support)                          |
| Generator project deletion               | `src/qyl.instrumentation.generators/` + all `[DuckDbColumn]` attributes removed |

### 21.4 MCP Platform — Extended (from `mcp-platform.md`)

Features beyond §15.3:

| Feature                                     | Status      | Description                                                 |
|---------------------------------------------|-------------|-------------------------------------------------------------|
| `QYL_SKILLS` env var filtering              | DONE        | Conditional DI registration per skill category              |
| ReadOnly/Destructive/Idempotent annotations | DONE        | `[McpServerTool]` attributes on all 60+ tools               |
| Error taxonomy in `CollectorHelper`         | DONE        | HTTP status-based error categorization                      |
| `QylScope` + `ScopingDelegatingHandler`     | DONE        | Constraint scoping via `QYL_SERVICE`/`QYL_SESSION` env vars |
| Monolith split (core/cloud/sentry)          | NOT STARTED | Three-tier architecture                                     |
| Streamable HTTP transport                   | NOT STARTED | HTTP instead of stdio                                       |
| OAuth RFC 9728                              | NOT STARTED | Token-based auth for remote MCP                             |
| `IObservabilityBackend` interface           | NOT STARTED | Plugin backend support                                      |
| Sentry backend connector                    | NOT STARTED | Sentry API bridge                                           |
| Platform connectors (GitHub App, VS Code)   | NOT STARTED | Ecosystem expansion                                         |

### 21.5 GenAI Semconv Full Reference (from `otel-semconv-reference.md`)

Features beyond §15.1:

| Feature                     | Description                                                                                                                                |
|-----------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| 40+ attributes              | Full reference across inference, embeddings, retrieval, tool call, agent, evaluation spans                                                 |
| Event body JSON schemas     | `gen_ai.system.message`, `gen_ai.user.message`, `gen_ai.assistant.message`, `gen_ai.tool.message`, `gen_ai.choice` with multimodal support |
| MCP span instrumentation    | `mcp.method.name`, `mcp.request.id`, `mcp.session.id`, `mcp.transport`, `mcp.server.name`                                                  |
| Evaluation spans            | `gen_ai.evaluation.name`, `gen_ai.evaluation.score.value` (0-1), `gen_ai.evaluation.score.label`, `gen_ai.evaluation.explanation`          |
| Provider extensions         | `openai.*` (response_format, seed), `anthropic.*` (thinking.enabled, thinking.budget_tokens)                                               |
| Server-side metrics         | `gen_ai.server.time_to_first_token`, `gen_ai.server.time_per_output_token`                                                                 |
| Semconv generation pipeline | TypeScript/C#/C#UTF8/TypeSpec/DuckDB SQL from single source-of-truth                                                                       |
| Attribute prefix categories | AI, Transport, Data, Infrastructure, Security, Runtime, Identity, Observe, Ops                                                             |

### 21.6 Traced Annotations — Stories (from `traced-annotations.md`)

Features beyond §15.6:

| Story | Feature                           | Description                                                                              |
|-------|-----------------------------------|------------------------------------------------------------------------------------------|
| T-001 | `RootSpan` property               | `parentContext: default` to orphan spans from parent-less operations                     |
| T-002 | `IAsyncEnumerable<T>` lifetime    | Wrapper keeps span open until stream exhaustion, not first yield                         |
| T-004 | `[TracedTag]` on properties       | Instance property capture via `@this.PropertyName` for class-level context               |
| T-005 | Analyzer diagnostics (QSD001-005) | Build warnings for misuse, abstract methods, unregistered ActivitySource, out/ref params |
| T-006 | `SkipIfDefault` for value types   | Skip recording zero, `Guid.Empty`, false when noisy                                      |
| T-007 | `[TracedReturn]` attribute        | Capture return value as span tag with optional property accessor                         |
| T-008 | `[SpanEvent]` parameter markers   | Milestone event creation without manual `Activity.AddEvent()`                            |
| —     | `[Traced]` class-level            | All public methods traced with `[NoTrace]` opt-out                                       |
| —     | `[TracedTag]` parameter capture   | Span tag extraction with `SkipIfNull` option                                             |
| —     | Exception attributes fix          | Correct `exception.type`/`exception.message`/`exception.stacktrace`                      |

### 21.7 Zero-Cost Observability — Phases (from `zero-cost-observability.md`)

Features beyond §15.7:

| Phase | Status      | Feature                                                                                                 |
|-------|-------------|---------------------------------------------------------------------------------------------------------|
| 0     | DONE        | `SubscriptionManager`: POST/DELETE/GET `/api/v1/observe`, ActivityListener wiring, glob-style filtering |
| 1     | DONE        | Observability Modes: `ObserveCatalog.cs`, `SchemaVersionNegotiator.cs` (partial)                        |
| 2     | DONE        | Catalog Endpoint: GET `/api/v1/observe/catalog` lists available subscriptions                           |
| 3     | NOT STARTED | Subscription Contracts: Roslyn generator binding interceptor attributes to storage columns              |
| 4     | NOT STARTED | Schema Negotiation: Multi-version semantic convention support                                           |
| 5     | NOT STARTED | Contract Validation: Startup verification that appender maps match schema                               |

**Dormant ActivitySources:** `GenAiSource` (qyl.genai), `DbSource` (qyl.db), `AgentSource` (qyl.agent), `TracedSource` (
qyl.traced) — lazy-initialized via `??=`.

**Dormant Meters:** `GenAiMeter`, `AgentMeter` — lazy-initialized.

**SSE live streaming:** `/api/v1/live` and `/api/v1/live/spans` with bounded channels and backpressure.

**Design:** `ParentBasedSampler(AlwaysOnSampler())` — always-on until subscription-based filtering activates.

### 21.8 AG-UI — Implementation Detail (from AG-UI design + impl docs)

Features beyond §15.5:

| Feature                             | Description                                                                                                           |
|-------------------------------------|-----------------------------------------------------------------------------------------------------------------------|
| `QylAgentBuilder` factory           | `FromCopilotAdapter(adapter)` and `FromChatClient(chatClient, agentName, ...)` with `InstrumentedChatClient` wrapping |
| `DeclarativeEngine` YAML runtime    | Loads `AdaptiveDialog` YAML, wires `ChatClientResponseAgentProvider`, streams `StreamUpdate` events                   |
| `InstrumentedChatClient` OTel layer | `gen_ai.*` spans + `CopilotMetrics` at `IChatClient` interception point                                               |
| `AIAgent.RunAsync()` interception   | `qyl.servicedefaults.generator` already intercepts (no `UseOpenTelemetry()` needed)                                   |
| Declarative YAML actions            | `InvokeMcpToolExecutor`, `ConditionGroup`, `SendActivity` with PowerFx expressions                                    |
| `StreamUpdate` SSE contract         | `RUN_STARTED`, `TEXT_MESSAGE_START/CONTENT/END`, `RUN_FINISHED`, `RUN_ERROR`                                          |
| Error boundaries                    | `HttpRequestException` + `JsonException` caught at `IChatClient` level only                                           |
| Cancellation                        | Silent close (no Error status, stream just closes)                                                                    |

### 21.9 Aspire Coverage — Extended Matrix (from `aspire-coverage.md`)

Features beyond §15.9:

**42-feature comparison against Aspire 13.x:**

| Status         | Count | Examples                                                                          |
|----------------|-------|-----------------------------------------------------------------------------------|
| DONE           | 9     | Resource model, environment injection, health checks                              |
| EXCEEDS        | 5     | GenAI Visualizer (AgentRunDetailPage), multi-language hosting, OTel GenAI semconv |
| PARTIAL        | 4     | JDBC-style endpoint injection, resource dependencies                              |
| NOT PRESENT    | 12    | Service discovery, Azure provisioning, test containers                            |
| NOT APPLICABLE | 12    | Visual Studio tooling, cloud-specific provisioning                                |

Notable: `AgentRunDetailPage` exceeds Aspire's GenAI Visualizer with waterfall spans, token/cost analytics,
and tool call sequencing with sequence numbers.

### 21.10 CI/CD Improvements (from `suggested-improvements.yaml`)

| ID                            | Type        | Size | Description                              | Status |
|-------------------------------|-------------|------|------------------------------------------|--------|
| `ci-sha-pin-actions`          | DESTRUCTIVE | XS   | Pin GitHub Actions to commit SHAs        | —      |
| `ci-enable-analyzers`         | DESTRUCTIVE | S    | Remove `-p:RunAnalyzers=false` from CI   | —      |
| `ci-vulnerability-audit-fail` | DESTRUCTIVE | XS   | Block CI on high-severity audit findings | —      |
| `ci-duckdb-schema-drift`      | ADDITIVE    | S    | TypeSpec → DuckDB schema validation job  | DONE   |

---

## 22. Requirements Registry

Methodology: [Requirements-Engineering-NASA-cFE](https://github.com/ANcpLua/Requirements-Engineering-NASA-cFE).
Every capability carries a unique ID, domain classification, scope label
(per [SCOPE-TAXONOMY.md](../decisions/SCOPE-TAXONOMY.md)), confidence tag, and rationale.

### ID Scheme

| Prefix  | Scope                | Source Sections                           |
|---------|----------------------|------------------------------------------|
| `LOOM-` | `CONTEXT-ONLY`       | [§4](#4-capabilities-catalog), [§6](#6-api-surface) |
| `SEER-` | `CONTEXT-ONLY`       | [§20](#20-seer-ai-platform--deep-architecture) |
| `MCP-`  | `CONTEXT-ONLY`       | [§17](#17-sentry-mcp-architecture)       |
| `PLAT-` | `CONTEXT-ONLY`       | [§16](#16-sentry-platform-features-beyond-loom) |
| `QYL-`  | `IMPLEMENTED-IN-QYL` | [§15](#15-qyl-platform-capabilities-reference), [§21](#21-qyl-roadmap--extended-feature-reference) |
| `COMP-` | `CONTEXT-ONLY`       | [§18](#18-competitive-analysis)          |

### Domain Objects

| Domain        | Description                                  | Sentry IDs                    | qyl IDs                       |
|---------------|----------------------------------------------|-------------------------------|-------------------------------|
| Autofix       | Bug fixing pipeline (RCA → solution → PR)    | LOOM-004, LOOM-005, SEER-001  | QYL-015a                      |
| Triage        | Issue scoring and prioritization             | LOOM-003                      | QYL-015b                      |
| CodeReview    | Pre-merge PR analysis                        | LOOM-006, SEER-003            | QYL-015c                      |
| Explorer      | Interactive debugging agent                  | LOOM-007, SEER-002            | —                             |
| Grouping      | Issue similarity and deduplication           | LOOM-001, SEER-005            | —                             |
| Summarization | Issue and trace summaries                    | LOOM-002, LOOM-009, SEER-008  | —                             |
| Anomaly       | Time-series anomaly and regression detection | LOOM-008, SEER-004, SEER-006  | —                             |
| Search        | NL → query translation                       | LOOM-010, SEER-007            | QYL-003 (partial)             |
| Testing       | Unit test generation from PRs                | LOOM-011                      | QYL-003 (partial)             |
| Insights      | Dashboards and monitoring                    | PLAT-001 .. PLAT-010          | QYL-001 (semconv), QYL-004    |
| MCP           | Tool protocol and authorization              | LOOM-015, MCP-001 .. MCP-008  | QYL-003                       |
| SDK           | Compile-time instrumentation + semconv       | —                             | QYL-001, QYL-006, QYL-007, QYL-011 |
| Storage       | DuckDB ingestion and schema                  | —                             | QYL-002                       |
| Protocol      | AG-UI, OTLP, SSE                             | —                             | QYL-005, QYL-008              |
| Analytics     | AI chat analytics (6 modules)                | —                             | QYL-004, QYL-012              |
| Hosting       | Polyglot resource orchestration              | —                             | QYL-010                       |
| API           | REST, RPC, endpoints                         | LOOM-012 .. LOOM-014, SEER-009 .. SEER-013 | — |

---

### LOOM — Sentry Loom Capabilities

| ID       | Capability                    | Domain        | Confidence | §Ref                            | Rationale                                                         |
|----------|-------------------------------|---------------|------------|----------------------------------|-------------------------------------------------------------------|
| LOOM-001 | Issue Grouping & Similarity   | Grouping      | CONFIRMED  | [4.1](#41-issue-grouping--similarity-ml) | 40% issue reduction — semantic dedup is the highest-leverage AI   |
| LOOM-002 | Issue Summarization           | Summarization | CONFIRMED  | [4.2](#42-issue-summarization)   | Headline + possible_cause reduces mean-time-to-understand         |
| LOOM-003 | Fixability Scoring            | Triage        | CONFIRMED  | [4.3](#43-fixability-scoring)    | 5-tier score gates automation level — prevents wasted LLM spend  |
| LOOM-004 | Root Cause Analysis           | Autofix       | CONFIRMED  | [4.4](#44-root-cause-analysis)   | 5-whys artifact feeds downstream fix pipeline                     |
| LOOM-005 | Autofix Pipeline              | Autofix       | CONFIRMED  | [4.5](#45-autofix-pipeline)      | End-to-end issue → PR automation — the core Loom value prop       |
| LOOM-006 | AI Code Review (Loom Prevent) | CodeReview    | CONFIRMED  | [4.6](#46-ai-code-review-Loom-prevent) | Shift-left: catch bugs before merge, not after deploy             |
| LOOM-007 | Explorer (Interactive Agent)  | Explorer      | CONFIRMED  | [4.7](#47-explorer-interactive-agent) | Ad-hoc debugging with multi-turn, tools, coding agent handoff    |
| LOOM-008 | Anomaly Detection             | Anomaly       | CONFIRMED  | [4.8](#48-anomaly-detection)     | 28-day baseline detects regressions missed by threshold alerts    |
| LOOM-009 | Trace Summarization           | Summarization | CONFIRMED  | [4.9](#49-trace-summarization)   | NL trace analysis surfaces performance issues without manual scan |
| LOOM-010 | Assisted Query                | Search        | CONFIRMED  | [4.10](#410-assisted-query--search-agent) | NL → Sentry query lowers the Snuba syntax learning curve          |
| LOOM-011 | Test Generation               | Testing       | CONFIRMED  | [4.11](#411-test-generation)     | PR-driven test gen improves coverage on changed code              |
| LOOM-012 | Public REST API               | API           | CONFIRMED  | [6.1](#61-public-rest-api)       | External integrations need stable programmatic access             |
| LOOM-013 | Loom Service Endpoints        | API           | CONFIRMED  | [6.2](#62-Loom-service-endpoints-sentry---Loom) | Sentry → Loom bridge carries events, traces, profiles             |
| LOOM-014 | RPC Bridge (Loom → Sentry)    | API           | CONFIRMED  | [6.3](#63-rpc-bridge-Loom---sentry) | Loom callbacks for issue updates, PR creation, status             |
| LOOM-015 | MCP Server (Sentry)           | MCP           | CONFIRMED  | [6.4](#64-mcp-server)            | Coding agent telemetry access via MCP protocol                    |

### SEER — Deep Architecture

| ID       | Capability                    | Domain        | Confidence | §Ref                             | Rationale                                                        |
|----------|-------------------------------|---------------|------------|-----------------------------------|------------------------------------------------------------------|
| SEER-001 | Autofix 5-Step LLM Chain      | Autofix       | CONFIRMED  | [20.1](#201-autofix-pipeline--5-step-llm-chain) | Artifact schemas, data gathering, billing — implementation depth |
| SEER-002 | Explorer Agent (10 tools)     | Explorer      | CONFIRMED  | [20.2](#202-explorer-agent--interactive-debugging) | Tool inventory and extensibility model for agentic debugging     |
| SEER-003 | Code Review Bug Prediction    | CodeReview    | CONFIRMED  | [20.3](#203-code-review--bug-prediction) | Trigger taxonomy and severity gating for PR comments             |
| SEER-004 | Anomaly Detection (Prophet)   | Anomaly       | CONFIRMED  | [20.4](#204-anomaly-detection--prophet-like-forecasting) | Seasonality patterns and confidence bounds for forecasting       |
| SEER-005 | Issue Similarity Embeddings   | Grouping      | CONFIRMED  | [20.5](#205-issue-similarity--embeddings-v1v2) | V1/V2 model versioning, circuit breaker, dual-write rollout     |
| SEER-006 | Breakpoint Detection          | Anomaly       | CONFIRMED  | [20.6](#206-breakpoint-detection--trend-analysis) | Statistical t-test for trend analysis — capability absent in §4  |
| SEER-007 | Fetch Issues (Context)        | Search        | CONFIRMED  | [20.7](#207-fetch-issues--context-retrieval) | Text/function/error-type retrieval — capability absent in §4     |
| SEER-008 | Issue Summary + Headline      | Summarization | CONFIRMED  | [20.11](#2011-seer-data-models)  | Data model: fixability scores, novelty, possible_cause           |
| SEER-009 | RPC Method Registry (30+)     | API           | CONFIRMED  | [20.8](#208-rpc-method-registry) | Full read-only method inventory for Seer ↔ Sentry bridge        |
| SEER-010 | API Endpoints (14)            | API           | CONFIRMED  | [20.9](#209-seer-api-endpoints)  | REST surface: explorer, anomaly, autofix, settings               |
| SEER-011 | Feature Flags (6)             | API           | CONFIRMED  | [20.10](#2010-seer-feature-flags) | Rollout gating: gen-ai-features, explorer, copilot, similarity  |
| SEER-012 | Entrypoints & Celery Tasks    | API           | CONFIRMED  | [20.12](#2012-seer-entrypoints--background-tasks) | Background orchestration: autofix polling, agent state           |
| SEER-013 | Error Handling & Monitoring   | API           | CONFIRMED  | [20.13](#2013-seer-error-handling--monitoring) | Circuit breaker, metrics, exception hierarchy                    |

### MCP — sentry-mcp Architecture

| ID      | Capability                   | Confidence | §Ref                            | Rationale                                                       |
|---------|------------------------------|------------|----------------------------------|-----------------------------------------------------------------|
| MCP-001 | Tool Catalog (27 tools)      | CONFIRMED  | [17.1](#171-tool-catalog-19-tools) | Reference inventory for qyl MCP tool parity analysis            |
| MCP-002 | Skills Authorization (5)     | CONFIRMED  | [17.2](#172-skills-based-authorization) | Tool bundling pattern adopted by qyl (`QylSkillKind`, 8 skills) |
| MCP-003 | Embedded Agent Framework     | CONFIRMED  | [17.3](#173-embedded-agent-framework) | NL → structured query via Vercel AI SDK — pattern reference     |
| MCP-004 | Dual OAuth Architecture      | CONFIRMED  | [17.4](#174-dual-oauth-architecture) | Nested token encryption — reference for remote MCP auth         |
| MCP-005 | Deployment Models (2)        | CONFIRMED  | [17.5](#175-deployment-models)   | Remote (Cloudflare) vs stdio — qyl plans similar split          |
| MCP-006 | Error Type Hierarchy         | CONFIRMED  | [17.6](#176-error-handling--rate-limiting) | 5-class error taxonomy — reference for qyl MCP error handling   |
| MCP-007 | Constraints & Capabilities   | CONFIRMED  | [17.7](#177-constraints--capabilities) | URL-scoped org/project gating with capability checks            |
| MCP-008 | Technology Stack             | CONFIRMED  | [17.8](#178-technology-stack)    | TypeScript + Hono + Zod + Vitest — contrast to qyl C# stack    |

### PLAT — Sentry Platform Features

| ID       | Capability              | Confidence | §Ref                           | qyl Status               | qyl Evidence                      |
|----------|-------------------------|------------|--------------------------------|---------------------------|-----------------------------------|
| PLAT-001 | AI Agent Monitoring     | CONFIRMED  | [16.1](#161-ai-agent-monitoring) | `IMPLEMENTED-IN-QYL`     | `AgentRunsPage.tsx`               |
| PLAT-002 | LLM Monitoring          | CONFIRMED  | [16.2](#162-llm-monitoring)    | `IMPLEMENTED-IN-QYL`     | GenAI semconv v1.40 DuckDB cols   |
| PLAT-003 | Session Replay          | CONFIRMED  | [16.3](#163-session-replay)    | Partial                   | `qyl.browser` (Web Vitals only)  |
| PLAT-004 | Uptime & Cron           | CONFIRMED  | [16.4](#164-uptime--cron-monitoring) | Partial               | `qyl.watchdog` (process-level)   |
| PLAT-005 | Release Health          | CONFIRMED  | [16.5](#165-release-health)    | `IMPLEMENTED-IN-QYL`     | `RegressionDetectionService.cs`   |
| PLAT-006 | User Feedback           | CONFIRMED  | [16.6](#166-user-feedback)     | Partial                   | Evaluation spans only             |
| PLAT-007 | Dashboards & Insights   | CONFIRMED  | [16.7](#167-dashboards--insights) | Partial                | Feature-specific pages            |
| PLAT-008 | Alerts & Notifications  | CONFIRMED  | [16.8](#168-alerts--notifications) | Partial               | SSE streaming only                |
| PLAT-009 | Logs Explorer           | CONFIRMED  | [16.9](#169-logs-explorer)     | `IMPLEMENTED-IN-QYL`     | OTLP log ingestion + DuckDB      |
| PLAT-010 | Pricing Model           | CONFIRMED  | [16.10](#1610-pricing-model-detail) | `CONTEXT-ONLY`       | — (reference only)                |

### QYL — qyl Platform Capabilities

| ID       | Capability                          | Domain    | §Ref                              | Evidence                                          | Rationale                                                       |
|----------|-------------------------------------|-----------|-------------------------------------|---------------------------------------------------|-----------------------------------------------------------------|
| QYL-001  | GenAI Semantic Conventions          | SDK       | [15.1](#151-genai-semantic-conventions-model), [21.5](#215-genai-semconv-full-reference-from-otel-semconv-referencemd) | 40+ attributes, 5-target codegen           | Single source-of-truth for GenAI telemetry vocabulary           |
| QYL-002  | DuckDB Appender Architecture        | Storage   | [15.2](#152-duckdb-storage--appender-architecture), [21.3](#213-duckdb-appender-purge-from-hades-storage-purgemd) | SpanStorageRow (26 col), LogStorageRow (16 col) | Zero-copy bulk ingestion replacing Roslyn INSERT generator |
| QYL-003  | MCP Platform (60+ tools, 8 skills)  | MCP       | [15.3](#153-mcp-platform-design), [21.4](#214-mcp-platform--extended-from-mcp-platformmd) | `QylSkillKind` enum, `QYL_SKILLS` env var | 3× Sentry tool count — deepest MCP surface in market            |
| QYL-004  | AI Chat Analytics (6 modules)       | Analytics | [15.4](#154-ai-chat-analytics-6-modules), [21.1](#211-ai-chat-analytics--extended-from-ai-chat-analyticsmd) | 6 modules, 9 API endpoints, 10 uncertainty signals | Feedback loop: what users ask, where AI fails               |
| QYL-005  | AG-UI + Declarative Workflows       | Protocol  | [15.5](#155-ag-ui--declarative-workflows), [21.8](#218-ag-ui--implementation-detail-from-ag-ui-design--impl-docs) | QylAgentBuilder, DeclarativeEngine, SSE | Sentry has no AG-UI — qyl's unique frontend protocol            |
| QYL-006  | Compile-Time Tracing (`[Traced]`)   | SDK       | [15.6](#156-compile-time-tracing-annotations), [21.6](#216-traced-annotations--stories-from-traced-annotationsmd) | TracedInterceptorEmitter, QSG005, 8 stories | Zero-overhead: no JVM agent, no runtime reflection          |
| QYL-007  | Zero-Cost Observability Contracts   | SDK       | [15.7](#157-zero-cost-observability-contracts), [21.7](#217-zero-cost-observability--phases-from-zero-cost-observabilitymd) | SubscriptionManager, 6 phases | Instrumentation at zero cost until subscriber activates     |
| QYL-008  | Port Architecture (OTLP)            | Protocol  | [15.8](#158-port-architecture-otlp-standard-compliance), [21.2](#212-port-architecture--extended-from-antipattern-remediationmd) | 5100/4317/4318 triple-port | OTel standard compliance — any OTLP client works by default |
| QYL-009  | Aspire 13.x Comparison              | —         | [15.9](#159-aspire-13x-feature-coverage), [21.9](#219-aspire-coverage--extended-matrix-from-aspire-coveragemd) | 42-feature matrix | `CONTEXT-ONLY` — competitive positioning against Aspire     |
| QYL-010  | Hosting Resource Model              | Hosting   | [15.10](#1510-hosting-resource-model) | 6 resource types, QylApp, QylRunner               | Polyglot orchestration: .NET, Node, Python, Vite, Docker        |
| QYL-011  | Agent Continuation Evaluation       | SDK       | [15.11](#1511-agent-continuation-evaluation-heuristic-first-pattern) | Heuristic-first pattern | Cost optimization: ~80% fewer LLM evaluator calls           |
| QYL-012  | AI Chat Extended (Phase 5)          | Analytics | [21.1](#211-ai-chat-analytics--extended-from-ai-chat-analyticsmd) | Semantic clustering, token economics          | Roadmap: embeddings, feedback, cross-platform                   |
| QYL-013  | MCP Platform Extended               | MCP       | [21.4](#214-mcp-platform--extended-from-mcp-platformmd) | Monolith split, Streamable HTTP, OAuth        | Roadmap: remote MCP, plugin backends, ecosystem                 |
| QYL-014  | CI/CD Improvements                  | —         | [21.10](#2110-cicd-improvements-from-suggested-improvementsyaml) | SHA-pinned actions, analyzers | Build hygiene: no suppression, no audit bypass              |
| QYL-015a | Autofix Orchestration               | Autofix   | [Evidence](#implementation-evidence-in-qyl) | `AutofixOrchestrator.cs`, `AutofixAgentService.cs` | qyl's own autofix pipeline (distinct from SEER-001)         |
| QYL-015b | Triage Pipeline                     | Triage    | [Evidence](#implementation-evidence-in-qyl) | `TriagePipelineService.cs`, `TriagePrompts.cs`     | qyl's own fixability scoring (distinct from LOOM-003)       |
| QYL-015c | Code Review Service                 | CodeReview| [Evidence](#implementation-evidence-in-qyl) | `CodeReviewService.cs`, `CodeReviewEndpoints.cs`   | qyl's own PR analysis (distinct from LOOM-006)              |

### COMP — Competitive Analysis

| ID       | Capability                   | Confidence | §Ref                          | Rationale                                              |
|----------|------------------------------|------------|-------------------------------|--------------------------------------------------------|
| COMP-001 | qodo-skills Multi-SCM Review | CONFIRMED  | [18.1](#181-qodo-skills-multi-provider-code-review) | Multi-provider pattern (4 SCMs) that Sentry lacks      |
| COMP-002 | Feature Gap Matrix           | CONFIRMED  | [18.2](#182-feature-gap-matrix-sentry-vs-qyl) | 29-row Sentry vs qyl comparison with status per feature |

---

## 23. Traceability Matrix

Maps qyl requirements to Sentry equivalents, implementation evidence, test coverage, and
verification status. Per [SCOPE-TAXONOMY.md](../decisions/SCOPE-TAXONOMY.md) Rule 4: features
moving from `CONTEXT-ONLY` to `IMPLEMENTED-IN-QYL` must attach local evidence.

### Ship Bar — qyl Verification Status

| Req ID   | Capability                  | Sentry Ref         | qyl Implementation                        | Test Evidence          | Verified |
|----------|-----------------------------|---------------------|--------------------------------------------|------------------------|----------|
| QYL-001  | GenAI Semconv               | —                   | `SemanticConventions.g.cs` (5 targets)     | Generator tests        | **Yes**  |
| QYL-002  | DuckDB Appender             | —                   | `DuckDBAppenderMap`, `SpanStorageRow`      | Ingestion integration  | **Yes**  |
| QYL-003  | MCP Platform (60+ tools)    | MCP-001 (27 tools)  | 14 tool files + 7 Loom tool files          | MCP protocol tests     | Partial  |
| QYL-004  | AI Chat Analytics           | —                   | 6 modules, 9 API endpoints planned         | —                      | No       |
| QYL-005  | AG-UI + Workflows           | —                   | `QylAgentBuilder`, `DeclarativeEngine`     | —                      | No       |
| QYL-006  | Compile-Time Tracing        | —                   | `TracedInterceptorEmitter` (QSG005)        | `TracedTests.cs`       | **Yes**  |
| QYL-007  | Zero-Cost Observability     | —                   | `SubscriptionManager`, `ObserveCatalog`    | —                      | No       |
| QYL-008  | Port Architecture           | —                   | `Program.cs` triple-port, `CollectorDiscovery` | Docker compose     | **Yes**  |
| QYL-010  | Hosting Resource Model      | —                   | 6 resource types, `QylRunner`              | —                      | No       |
| QYL-011  | Agent Continuation          | —                   | Heuristic-first hook                       | —                      | No       |
| QYL-015a | Autofix Orchestration       | LOOM-005, SEER-001  | `AutofixOrchestrator.cs`                   | —                      | No       |
| QYL-015b | Triage Pipeline             | LOOM-003            | `TriagePipelineService.cs`                 | —                      | No       |
| QYL-015c | Code Review Service         | LOOM-006, SEER-003  | `CodeReviewService.cs`                     | —                      | No       |

**Summary:** 4 verified, 1 partial, 8 unverified.

### Feature Parity — Sentry vs qyl by Domain

| Domain        | Sentry                             | qyl                                | Status                            |
|---------------|------------------------------------|------------------------------------|-----------------------------------|
| Autofix       | LOOM-005: 5-stage, 30min, GPU pod  | QYL-015a: AutofixOrchestrator      | Partial — E2E unverified          |
| Triage        | LOOM-003: 5-tier GPU scoring       | QYL-015b: TriagePipelineService    | Partial — real-issue unverified   |
| CodeReview    | LOOM-006: GitHub webhooks, A/B     | QYL-015c: CodeReviewService        | Partial — webhook flow unverified |
| Explorer      | LOOM-007: 10 tools, Slack, coding  | QYL-005: AG-UI + CopilotKit       | Different arch (SSE vs RPC)       |
| Grouping      | LOOM-001: jina-v2, pgvector        | ErrorFingerprinter (heuristic)     | **Gap** — no ML embeddings        |
| Summarization | LOOM-002: GPU pod, 7d cache        | DuckDB + IChatClient               | **Gap** — no dedicated pod        |
| Anomaly       | LOOM-008: 28-day, Prophet-like     | qyl.watchdog (process-level)       | **Gap** — no time-series forecast |
| Search        | LOOM-010: NL → Snuba               | QYL-003: AssistedQueryTools        | Partial — simpler translation     |
| Testing       | LOOM-011: PR-driven                | QYL-003: TestGenerationTools       | Partial — tool exists, untested   |
| MCP           | MCP-001: 27 tools, 5 skills        | QYL-003: 60+ tools, 8 skills      | **qyl leads** (3× tools)         |
| Insights      | PLAT-001..010: 10 categories       | Feature-specific pages             | **Gap** — no widget composition   |
| SDK           | — (runtime agents)                 | QYL-006: compile-time interceptors | **qyl only**                      |
| Protocol      | — (no AG-UI)                       | QYL-005: AG-UI SSE                 | **qyl only**                      |
| Analytics     | —                                  | QYL-004: 6 AI chat modules         | **qyl only**                      |
| Hosting       | —                                  | QYL-010: 6 resource types          | **qyl only**                      |

**Summary:** 5 qyl-only, 3 qyl-leads/different, 3 gaps, 4 partial.

### Verification Priority

| Priority | Req IDs                            | Verification Target                                          |
|----------|------------------------------------|--------------------------------------------------------------|
| P0       | QYL-015a, QYL-015b, QYL-015c      | Autofix/Triage/CodeReview full E2E with real inputs          |
| P1       | QYL-003, QYL-005                   | MCP tool invocation via protocol, AG-UI stream contract      |
| P2       | QYL-004, QYL-007, QYL-010, QYL-011| Analytics API, subscription activation, hosting, continuation|
| P3       | QYL-012, QYL-013                   | Phase 5 clustering, remote MCP, OAuth RFC 9728               |
