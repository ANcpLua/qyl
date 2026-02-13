# ADR-0001: Workspace Identity and Isolation

- Status: Accepted
- Date: 2026-02-13
- Owners: platform, infra, product, ux

## Context

Parallel agentic development with multiple worktrees and local environments fails when identity,
origin routing, enforcement, attention policy, cost policy, and secrets are not explicit.
The target is zero customer decisions beyond "what to observe".

## Decision

### 1) Workspace Identity

- Choice: `registry_uuid`
- Storage: `.qyl/workspace.json`
- Generation: auto-generate on first `docker compose up`

Rationale:
- Path hashes break on rename, move, and symlink scenarios.
- UUID is stable and portable across path changes.
- Hybrid (UUID + path-derived primary identity) adds an avoidable decision point.

### 2) Canonical Local Origin Strategy

- Choice: `loopback_ip`
- Strategy: assign deterministic `127.0.0.x` per workspace

Rationale:
- Avoid browser/OS variance of `*.localhost` handling.
- Zero DNS setup.
- Deterministic OAuth redirect behavior and natural cookie scope partitioning.

Implementation note:
- Keep URL complexity invisible to users via dashboard and Copilot routing.

### 3) Phase-2 Enforcement Policy

Fail-closed actions:
- `deploy_to_shared_remote`
- `token_issuance`
- `cross_workspace_secret_access`
- `outbound_webhook_registration`

Warn-only actions:
- `local_docker_deploy`
- `read_only_cross_workspace_spans`
- `token_refresh`
- `generator_output_overwrite`

Rationale:
- Fail-closed for actions that leave the machine or mutate shared state.
- Warn-only for local and reversible operations.

### 4) Notification Interruption Policy

- `p0`: immediate
- `p1`: 5m digest
- `p2_p3`: digest
- Hard cap: `6` interruptions/hour

Rationale:
- Protect flow state and bound context-switch cost.
- Always break through for critical incidents.
- Prevent alert storms under cascading failures.

### 5) Cost Guardrails

- Mode: `adaptive`
- Latency-sensitive override: `allow`
- Hard-budget behavior: `degrade` (never hard-stop by default)

Rationale:
- Strict budget stop near completion wastes work.
- Degraded fidelity completion is preferred over aborted execution.

### 6) Secrets v1

- Choice: `workspace_scoped_lifecycle`
- External dependency: none required for v1

Rationale:
- Keep setup to "docker compose up and everything works".
- Avoid forced external secret-manager setup for local-first workflows.

## Consequences

Positive:
- Stable identity across moves and renames.
- Deterministic local origin model for auth and cookies.
- Clear and enforceable safety boundaries in phase 2.
- Predictable interruption and cost behavior.
- Built-in secrets lifecycle aligned with local-first UX.

Negative / Risks:
- Loopback IP strategy requires robust local IP allocation and conflict handling.
- Built-in secrets lifecycle needs strong encryption, rotation, and recovery design.
- Enforced fail-closed actions may initially increase perceived friction.

## Follow-up

- Implement policy files:
  - `.qyl/policy/workspace-control-plane.yaml`
  - `.qyl/policy/enforcement-matrix.yaml`
- Instrument QYL metrics for policy outcomes and regressions.
