# Ledger/governance plane

Mission:
- store truth about what the system decided, did, and was allowed to do

Owns:
- run ledger
- artifact registry
- approval history
- policy decisions
- evaluation outcomes
- rollback lineage
- audit semantics and capability boundaries

Depends on:
- contracts
- storage primitives

Must not depend on:
- agent session state as the source of truth
- workflow checkpoint state as audit truth

Current qyl areas:
- Loom/autofix run records and policy gates
- future durable run/session ledger work

Success condition:
- an operator can reconstruct any run without reading prompts or replaying execution state.
