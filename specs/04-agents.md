# Agents Specification

`src/qyl.agents/` is the AI automation layer for qyl.

It owns Loom, autofix, triage, summarization, and other logic that requires `Microsoft.Extensions.AI` or agent runtimes. It is a library mounted by `qyl.web`, not a standalone host and not part of `qyl.collector`.

---

## 1. Overview

Design rule: `qyl.agents` depends only on `qyl.core`.

It may consume:
- `ITelemetryStore`
- `IArtifactService`
- `IIssueService`
- `IGitOpsService`
- query/result DTOs and value objects from `qyl.core`

It may not depend on:
- `qyl.collector`
- `qyl.mcp`
- `qyl.infrastructure`

The only assembly allowed to compose implementations is `qyl.web`.

## 2. Loom and Autofix

Loom is a domain within `qyl.agents`, not a separate runtime product.

Owned responsibilities:
- background investigation
- interactive handoff sessions
- root-cause analysis
- solution generation
- diff generation
- confidence scoring
- autofix orchestration

## 3. Summarization and Triage

AI-driven summarization, prioritization, and classification also live here:

- `TriagePipelineService`
- `CodeReviewService`
- `AiSummarizationService`
- `MetaAgent`
- agent-side convergence workflows that require model access

If a component needs `Microsoft.Extensions.AI`, it belongs here by default.

## 4. GitOps Boundary

`qyl.agents` may request GitHub or PR operations, but only through `IGitOpsService` from `qyl.core`.

That means:
- no direct GitHub HTTP clients in `qyl.agents`
- no dependency from agents to MCP
- no infrastructure-specific implementation details in this project

## 5. Composition via qyl.web

`qyl.web` hosts the process and wires implementations into `qyl.agents` using DI.

`qyl.agents` should therefore expose registration helpers and endpoint mappers, but never become the composition root itself.

See `specs/v2-architecture.md` for the global project graph.
