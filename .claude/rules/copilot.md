---
paths:
  - "src/qyl.copilot/**"
---

# Copilot Rules

## Architecture

- `qyl.copilot` is a reusable library layer for embedded agents, adapter auth, AG-UI primitives, and workflow execution.
- The collector host composes it via `AddQylCopilot`, `AddQylAgui`, and `AddQylCopilotTelemetry`.
- `QylCopilotAdapter`, `WorkflowEngine`, and `QylAgentBuilder` are the core seams.

## Constraints

- Keep this project host-agnostic; do not move collector hosting concerns into `qyl.copilot`.
- Prefer changes here for agent behavior, adapter auth, workflow parsing, and execution flow.
- AG-UI HTTP endpoints belong to the collector host, not this library.
- Workflow persistence should stay behind `IExecutionStore`; avoid concrete storage coupling in library code.
