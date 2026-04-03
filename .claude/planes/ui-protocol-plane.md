# UI/protocol plane

Mission:
- project platform state and workflows to humans and external clients

Owns:
- dashboard surfaces
- AG-UI state projection
- approvals UI
- workflow progress and artifact views
- client-facing session/state sync semantics

Depends on:
- serving plane
- ledger/governance plane
- agent/control plane outputs

Must not depend on:
- hidden domain logic in components
- direct storage coupling

Current qyl areas:
- `src/qyl.dashboard`
- AG-UI hosting/projection work
- operator-facing issue, trace, cost, and fix-run surfaces

Success condition:
- operators can inspect, approve, reject, and diff system behavior without needing chat transcripts as the UI model.
