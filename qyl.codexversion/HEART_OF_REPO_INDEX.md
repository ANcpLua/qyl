# HEART OF REPO INDEX

## Core thesis
`qyl` is a multi-plane orchestrator whose “heart” is the delegation pipeline: a task is received, decomposed, dispatched, executed under constrained concurrency, observed, and then summarized back into structured state that other planes consume.

## 1) Delegate task as the orchestration anchor
- `delegate-task` is the control trigger used by the system to launch delegated work with clear ownership and lifecycle.
- A delegated unit carries enough metadata to allow traceability (caller, intent, constraints, expected artifacts).
- Delegation is intentionally separate from execution details; it only defines what to run, not how execution is wired.

## 2) `call-omo-agent` as execution bridge
- `call-omo-agent` is the active execution boundary to external intelligence/runtime agents.
- It normalizes call shape (prompt, plugin context, limits, and model contract) so downstream handlers receive a consistent contract.
- It is the point where raw task intent becomes actionable run steps.

## 3) Background manager as reliability layer
- The background manager owns long-lived and asynchronous flows that outlive a single request.
- It schedules deferred work, handles retries/timeouts, and persists/retrieves execution state.
- It is also the guardrail for avoiding request-thread coupling in delegated pipelines.

## 4) Spawn limits and quotas
- Spawn limits cap recursive/parallel fan-out and protect platform stability.
- Limits govern:
  - active delegation count per session/worktree
  - agent spawn per delegate chain depth/branch
  - queued work pressure thresholds
- These limits are enforced before dispatch so failures are explicit and deterministic instead of cascading later.

## 5) Concurrency model
- Concurrency is controlled, not permissive: task intake can be broad, but execution is throttled by policies.
- Main pattern is bounded parallelism with bounded queueing.
- The model is “structured saturation”: keep throughput by tuning concurrency windows and queue size instead of unbounded task fan-out.

## 6) Plugin configuration as extension map
- Plugin config is the capability registry for what agents, tools, and runtime integrations are allowed.
- `HEART` logic reads plugin config as policy, then runtime behavior (which call targets exist, what they can do) follows.
- This keeps domain logic out of core delegation and makes behavior replaceable without changing control-plane mechanics.

## 7) Model requirements
- Model requirements are explicit per delegate path (capability, token budget, context window expectations, and tool-use behavior).
- The system chooses/validates model profile before dispatch to avoid runtime mismatches.
- Fail-fast rules apply when requirements are under-specified or incompatible with the delegate payload.

## 8) Prompt metadata as memory substrate
- Prompt metadata is stored and threaded through the lifecycle (request origin, lineage, constraints, policy tags).
- It enables deterministic review/audit and better routing decisions in later stages.
- Every delegated action should be explainable by metadata alone: who asked, why, with what constraints, and by which policy.

## What makes this repository distinct
- Deterministic delegation contracts over ad-hoc agent calling.
- Explicit concurrency + spawn governance baked into orchestration.
- Plugin-driven capabilities with explicit model contract enforcement.
- Strong metadata plumbing so agent actions remain observable, replayable, and governable.
