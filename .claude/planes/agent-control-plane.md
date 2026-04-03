# Agent/control plane

Mission:
- run bounded investigations and repair workflows over structured evidence

Owns:
- specialist agents
- workflow orchestration
- approvals and handoffs
- bounded repair loops
- report synthesis

Execution law:
- `function` for deterministic work
- `agent` for bounded reasoning and tool choice
- `workflow` for orchestration, durability, and approvals

Depends on:
- intelligence plane
- serving plane
- ledger/governance plane
- compiler plane outputs

Must not depend on:
- raw storage internals as its primary API
- implicit shared memory assumptions
- unbounded autonomy

Current qyl areas:
- Loom and autofix flows
- MAF integration points
- issue investigation and repair orchestration

Success condition:
- every autonomous action is capability-bounded, explainable, restartable, and approval-aware.
