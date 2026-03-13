---
paths:
  - "src/qyl.agents/**"
  - "src/qyl.workflows/**"
---

# Copilot Rules

## Context
- OAuth and connector flow overview: `.agents/qyl-workflows/README.md#section-1-architektur-ueberblick`

## Architecture
- `src/qyl.agents` owns Copilot auth, adapter setup, provider selection, and telemetry wiring.
- `src/qyl.workflows` owns workflow discovery, workflow execution, and declarative workflow loading.
- Collector hosts the `/api/v1/copilot/*` runtime surface and workflow state, but embedded-agent semantics should stay in the agent/workflow layer unless persistence or HTTP behavior is the issue.
- MCP is a client of the collector copilot endpoints; it must not become a second workflow engine.

## Ownership Boundaries
- Put provider/auth/instruction/adapter behavior in `src/qyl.agents`.
- Put workflow parsing and execution behavior in `src/qyl.workflows`.
- Put persisted run state, SSE/API shape, and side effects in `src/qyl.collector`.
- If the same behavior appears in workflow docs, MCP, and collector, fix the source layer rather than patching three copies.

## Constraints
- Keep workflow definitions external to runtime code; discover them through the configured workflow directory.
- Preserve the separation between Markdown workflow discovery and declarative YAML workflow loading.
- Do not couple agent/workflow code directly to MCP transport concerns.
- Prefer `CopilotOptions`, `WorkflowEngine`, and `DeclarativeEngine` as the composition seams instead of inventing parallel orchestration paths.
