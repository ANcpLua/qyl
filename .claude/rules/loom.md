---
paths:
  - "src/qyl.loom/**"
  - "src/loom/**"
---

# Loom Rules

## Architecture

- `src/loom/` is the Sentry Loom reference implementation (TypeScript + Python). Read-only — never modify.
- `src/qyl.loom/` is the C# transpile of that reference. This IS the product.
- qyl.loom has its own standalone project at `~/RiderProjects/qyl.loom/` with its own `.slnx`.
- Loom uses AIAgent (via QylAgentBuilder), not raw IChatClient calls.

## Ownership Boundaries

- qyl.loom references collector, agents, workflows, contracts, and instrumentation via ProjectReference.
- Collector must NOT reference qyl.loom — the dependency arrow goes one way only.
- MCP tools for Loom live in `src/qyl.mcp/Tools/` and access Loom via collector HTTP endpoints.

## Non-Negotiable

- **Do NOT decompose qyl.loom into collector.** It is a standalone product.
- **Do NOT delete qyl.loom.** It is actively developed.
- **Do NOT merge Loom services into collector services.** Keep the product boundary clean.
