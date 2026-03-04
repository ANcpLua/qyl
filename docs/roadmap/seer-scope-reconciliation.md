# Seer Scope Reconciliation (qyl)

Date: 2026-03-04
Owner: qyl roadmap

## Purpose

Clarify why sections `12` and `13` from the Seer design specification should be
kept in our documentation, and why the statement

`Most Seer-specific backend requirements are intentionally missing due product scope`

is no longer accurate for qyl.

This note separates two concerns:

1. Comparative research context (Sentry Seer lifecycle, closed-source unknowns).
2. qyl implementation scope (what is actually implemented in this repository).

## Decision

Do not omit sections `12` and `13`. Keep them, but classify them as
`comparative context` rather than `local acceptance criteria`.

### Section-by-section position

| Section | Previous label | Updated label | Why |
|---|---|---|---|
| `12. Known Closed-Source Gaps` | OUT-OF-SCOPE | KEEP (context-only) | Useful to document closed-source boundaries and avoid false certainty. |
| `13. In Active Development` | OUT-OF-SCOPE | KEEP (context-only) | Useful for competitive tracking and planning, not a local ship gate. |
| `13. Recently Graduated` | OUT-OF-SCOPE | KEEP (context-only) | Useful for market/reference timeline awareness. |
| `13. Deprecated / Removed` | OUT-OF-SCOPE | KEEP (context-only) | Useful to avoid implementing already-removed external patterns. |

## Why "missing due product scope" is stale

qyl now implements a substantial Seer-like backend surface. The repo is no
longer missing most of the previously listed capabilities.

## Implementation evidence in qyl

| Capability | Status in qyl | Evidence |
|---|---|---|
| Endpoint families for Seer-like workflows | Implemented | `src/qyl.collector/Program.cs` maps `MapAutofixEndpoints`, `MapRegressionEndpoints`, `MapAgentHandoffEndpoints`, `MapCodeReviewEndpoints`, `MapGitHubWebhookEndpoints`, `MapSeerSettingsEndpoints`, `MapTriageEndpoints`. |
| Autofix pipeline | Implemented | `src/qyl.collector/Autofix/AutofixAgentService.cs`, `AutofixOrchestrator.cs`, `AutofixEndpoints.cs`, storage in `DuckDbStore.Autofix*.cs`. |
| Fixability scoring and triage | Implemented | `src/qyl.collector/Autofix/TriagePipelineService.cs`, `TriagePrompts.cs`, `TriageEndpoints.cs`, `DuckDbSchema.Triage.cs`. |
| Code review endpoints and service | Implemented | `src/qyl.collector/Autofix/CodeReviewEndpoints.cs`, `CodeReviewService.cs`, `CodeReviewPrompt.cs`. |
| GitHub webhook ingestion + signature validation | Implemented | `src/qyl.collector/Autofix/GitHubWebhookEndpoints.cs` (`X-Hub-Signature-256`, `HMACSHA256`). |
| Agent handoff lifecycle | Implemented | `src/qyl.collector/Autofix/AgentHandoffEndpoints.cs`, `AgentHandoffService.cs`, `DuckDbStore.Handoff.cs`. |
| Regression detection and querying | Implemented | `src/qyl.collector/Autofix/RegressionDetectionService.cs`, `RegressionEndpoints.cs`, `DuckDbStore.Regressions.cs`. |
| Dashboard/UI for Seer flows | Implemented | `src/qyl.dashboard/src/pages/SeerDashboardPage.tsx`, `IssueTriagePage.tsx`, `IssueFixRunsPage.tsx`, `CodeReviewPage.tsx`. |
| MCP tooling for Seer flows | Implemented | `src/qyl.mcp/Tools/AutofixMcpTools.cs`, `TriageTools.cs`, `RegressionTools.cs`, `GitHubMcpTools.cs`, `AgentHandoffTools.cs`, `AssistedQueryTools.cs`, `TestGenerationTools.cs`. |

## Clarification on HMAC RPC and feature flags

Some external-Seer wording still does not map 1:1 to qyl:

1. HMAC RPC (Seer internal RPC bridge): not a required qyl architecture piece.
   qyl uses direct local services and REST endpoints; webhook HMAC validation is
   implemented where relevant.
2. Sentry feature-flag topology/lifecycle tables: these remain external product
   lifecycle data. qyl keeps local configuration and telemetry support, but does
   not mirror Sentry's internal rollout governance.

## Recommended wording update

Replace broad omission language with:

`Closed-source Sentry internals remain non-inspectable, but qyl implements its own`
`open Seer-like backend surface (triage, autofix, code review, handoff, regression,`
`webhook ingestion, dashboard, and MCP tooling).`

## Scope model going forward

Use these labels consistently in Seer docs:

1. `IMPLEMENTED-IN-QYL` - shipped in this repository.
2. `CONTEXT-ONLY` - kept for comparative awareness; not a qyl acceptance gate.
3. `EXTERNAL-CLOSED` - known unknowns in closed-source systems.
4. `NOT-PLANNED` - explicitly excluded from qyl architecture.

This removes the false "omit vs include" binary and preserves useful context
without mislabeling delivered qyl functionality as out-of-scope.

