# AGENTS.md (agent layer)

## Scope
These instructions govern only files under `qyl.codexversion/src/agents`.

## Agent-layer contract

- Treat `prompt metadata` and `model requirements` as registries.
  - Do not encode model-selection logic inline in tasks.
  - Resolve behavior through registry lookups (prompt descriptors, model keys, requirements policies).
- Route investigation and triage categories through `delegate-task`.
  - Category identifiers must be mapped only via the delegate-task path.
  - Avoid ad-hoc branching on category strings inside task code.
- Respect background limits as hard constraints.
  - Enforce every documented timeout, retry budget, parallelism cap, and token/size limit.
  - Do not bypass or ignore background scheduler constraints in flow control.
- Keep session continuity first-class.
  - Persist and restore session state (identity, intent, constraints, progress, and outcomes) at each checkpoint.
  - Continue work from existing checkpoint context before creating new sessions or handing off.
- Keep instructions and dependencies one-way from this layer into lower layers.
  - The agent layer should orchestrate via registries and delegation, not re-implement core platform logic.
