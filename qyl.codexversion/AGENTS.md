# AGENTS.md

This repository is governed by these core principles:

1. TypeScript + Zod are the canonical source of truth for all structured definitions.
2. Tooling orchestrates, it does not own domain decisions.
3. Configuration schema is the control plane contract.
4. Background processing boundaries, limits, and concurrency are first-class concerns.
5. Keep files small, typed, and explicit.

## Global standards

- All runtime models, request/response shapes, and state transitions must be represented in TypeScript and validated with Zod schemas.
- Zod validators and inferred TS types are the primary truth source for compatibility, docs, and inter-module contracts.
- Tool behavior must be described through deterministic orchestration inputs and outputs, not ad-hoc business logic.
- Any behavior implemented in a tool should be transparent, configurable, and bounded by schema.

## Tool layer

- Treat tools as orchestrators only.
  - Resolve context, dispatch tasks, aggregate results, and enforce sequencing.
  - Do not encode product policy, domain policy, or workflow policy inside tool logic.
- Tool interfaces must be schema-driven and strictly typed.
- Tool boundaries should be narrow and composed via composition instead of centralized procedural logic.

## Configuration / Control plane

- Configuration is control-plane owned state.
  - Place configuration schema definitions in dedicated control-plane files.
  - Changes to behavior must happen through config changes and schema evolution, not by hardcoding constants in data paths.
- Validate all config reads/writes through Zod before use.
- Config evolution must remain backward compatible where possible; when breaking changes are required, provide explicit migration strategy.

## Background processing and concurrency

- Treat background work as a first-class subsystem, not an implementation detail.
- Define explicit limits and concurrency bounds in configuration:
  - max concurrent jobs
  - retry budgets and backoff windows
  - queue thresholds and dead-letter handling
  - per-stage timeout and cancellation policy
- Every background path must:
  - expose clear lifecycle states
  - be cancellable
  - emit structured progress and completion signals
  - fail safely with bounded resource impact
- Avoid unbounded parallelism, unbounded queues, and implicit retries.

## Type and file hygiene

- Prefer small, composable files with single responsibilities.
- Keep modules explicit: if a file grows beyond a single cohesive concern, split it.
- Use strict TypeScript with inferred types from Zod where practical.
- Avoid `any` and dynamic, schema-untagged payloads.
- Surface invariants with typed guards and explicit runtime validation.

## Documentation and review expectations

- Each touched area should include:
  - current behavior
  - schema used to govern it
  - concurrency/limits semantics
- If a change alters contract semantics, update the corresponding Zod schema and generated TS types together.
