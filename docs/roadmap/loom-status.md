# Loom Status

This document is the canonical source for Loom capability readiness, MCP verification status, and Loom-specific known issues.

Use related docs for their distinct roles:

- `docs/roadmap/loom-design.md` for architecture and design rationale
- `docs/plans/2026-03-06-qyl-ship-workflow.md` for execution phases and ship gates
- `docs/plans/2026-03-10-qyl-to-qyl-loom-migration.md` for repo migration and boundary enforcement

## Capability Status Matrix

| Capability | Current Status | Verified Evidence | Remaining Work | Last Verified Date |
|---|---|---|---|---|
| Autofix pipeline | ` IMPLEMENTED-IN-QYL.LOOM` | Real 5-step LLM pipeline, PR creation with hunk matching, and policy gating are implemented. Verified code fixes include the COALESCE intermediate-status update, JSON extractor hardening, and schema column alignment. | Run a live end-to-end flow with a real LLM and GitHub target. | 2026-03-09 |
| Triage/fixability scoring | ` IMPLEMENTED-IN-QYL.LOOM` | Dual-path scoring is present with LLM and heuristic fallback, auto-routing at the `0.8` threshold, and the POST endpoint bug was fixed so it triages the requested issue. | Verify scoring quality and routing against real issues. | 2026-03-09 |
| Code review | ` IMPLEMENTED-IN-QYL.LOOM` | PR diff fetch, LLM review analysis, and inline comment posting are implemented. The per-request authorization fix removed shared `HttpClient` auth mutation. | Verify the live webhook-to-comment flow against GitHub. | 2026-03-09 |
| GitHub webhook ingestion | ` IMPLEMENTED-IN-QYL.LOOM` | HMAC-SHA256 validation, event routing, and full payload audit storage are present. The `github_events` schema wiring is in place. | Verify signed payload handling with a real webhook source. | 2026-03-09 |
| Agent handoff | ` IMPLEMENTED-IN-QYL.LOOM` | Full pending → accepted → completed/failed lifecycle is implemented with context assembly. The accept-path TOCTOU race was fixed with status-guarded updates. | Verify live agent polling, accept, submit, and fail flows. | 2026-03-09 |
| Regression detection | ` IMPLEMENTED-IN-QYL.LOOM` | Deployment-based fingerprint matching, query access, and re-triage triggering are implemented. Verified fixes include LIKE ESCAPE handling and checkpoint advancement on failure. | Verify live regression detection against real deployments. | 2026-03-09 |
| Dashboard UI | ` IMPLEMENTED-IN-QYL.LOOM` | `LoomDashboardPage`, `IssueTriagePage`, and `CodeReviewPage` load with real API hooks, loading states, and error states wired. | Run Playwright end-to-end coverage against a live instance. | 2026-03-09 |
| MCP tooling | ` IMPLEMENTED-IN-QYL.LOOM` | Loom MCP tool classes are registered and a Phase 4 audit has already been completed. See the **MCP Verification** section below for the detailed audit result and broken-tool list. | Fix the broken tools and complete protocol-level remote invocation validation. | 2026-03-09 |

## MCP Verification

### Tool Inventory Summary

- Audit snapshot date: `2026-03-09`
- Total tool inventory: `78` tools across `27` classes
- Audit outcome: `68 OK`, `3 BROKEN`, `6 INDIRECT`, `2 LOCAL`
- Registration is confirmed; protocol-level remote invocation remains an open readiness step

### Broken Tools

| Tool | Failure | Follow-up |
|---|---|---|
| `qyl_genai_models` | Missing collector endpoint | Add or restore the backing collector endpoint, then rerun MCP validation. |
| `qyl_genai_timeseries` | Missing collector endpoint | Add or restore the backing collector endpoint, then rerun MCP validation. |
| `qyl_generate_test` | Wrong endpoint path | Correct the endpoint path and rerun MCP validation. |

### Indirect and Local Notes

- The source audit recorded `6 INDIRECT` and `2 LOCAL` results but did not enumerate each tool in the design doc snapshot.
- Preserve those counts here until the audit is expanded with per-tool detail.

### Protocol Transport Readiness

- Tool registration is not enough for ship readiness.
- Remote MCP access still requires protocol-level invocation validation against the deployed connector surface.
- The execution plan for that work lives in `docs/plans/2026-03-06-qyl-ship-workflow.md`, Phase 3.

## Known Issues

Only open issues or issues needed to explain current verification gaps belong here. Resolved items are intentionally omitted.

| Issue | Scope/Layer | Owner or Fix Location | Status |
|---|---|---|---|
| TypeSpec-generated tables create duplicate `created_at` columns | Layer 1: schema generation | `eng/build/SchemaGenerator.cs` | Open |
| `errors` table uses `affected_user_ids` in one place and `affected_users` in another | Layer 1 schema generation vs store alignment | `DuckDbSchema.g.cs` and `DuckDbStore.cs` | Open |
