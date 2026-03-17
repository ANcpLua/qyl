---
paths:
  - "src/qyl.loom/**"
---

# Loom Rules

## Architecture

- `src/qyl.loom/` is the qyl Loom product. Originally transpiled from Sentry's Seer reference (now deleted).
- Reference knowledge extracted to `docs/reference/seer-knowledge-base.md`. External source at
  `~/sentry-seer-sourcepack/`.
- qyl.loom has its own standalone project at `~/RiderProjects/qyl.loom/` with its own `.slnx`.
- Loom uses IChatClient (Microsoft.Extensions.AI) for LLM calls and AIAgent (Microsoft.Agents.AI) for agent orchestration. No qyl wrappers.

## Ownership Boundaries

- qyl.loom references collector, contracts, and instrumentation via ProjectReference. qyl.agents and qyl.workflows are deleted.
- Collector must NOT reference qyl.loom — the dependency arrow goes one way only.
- MCP tools for Loom live in `src/qyl.mcp/Tools/` and access Loom via collector HTTP endpoints.

## Non-Negotiable

- **Do NOT decompose qyl.loom into collector.** It is a standalone product.
- **Do NOT delete qyl.loom.** It is actively developed.
- **Do NOT merge Loom services into collector services.** Keep the product boundary clean.
