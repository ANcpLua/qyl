# qyl Ship Workflow

**Date:** 2026-03-06
**Goal:** Ship qyl to production quality, implement Loom, validate MCP across all Claude surfaces.

---

## Phase 1 — Fix qyl Core (Frontend + Backend)

**Rule:** Everything visible in the frontend works. No exceptions. If it's duplicate or half-baked, delete it. No stubs,
no mocks, no suppressed warnings. Every merged branch is flawless.

### 1.1 Backend: Kill the 500s

The console errors tell the full story:

| Broken Endpoint                        | Root Cause Hypothesis                                                                              | Action                                                               |
|----------------------------------------|----------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| `GET /api/v1/issues/undefined`         | Frontend passes `undefined` as issue ID — routing/state bug in React, not backend                  | Fix frontend: guard against undefined IDs, don't fetch               |
| `GET /api/v1/issues/undefined/events`  | Same — cascading from above                                                                        | Same fix                                                             |
| `GET /api/v1/analytics/users?period=*` | DuckDB query layer, missing schema, or unimplemented handler returning 500 instead of empty result | Implement or return `{ data: [], total: 0 }` — no 500 for empty data |

**Verification:** `curl` every endpoint that the frontend calls. No 500s. Period.

### 1.2 Frontend: React State Bugs

| Bug                                                  | Fix                                                                                                                                |
|------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| `Select is changing from uncontrolled to controlled` | Initialize Select state with a default value (empty string, not undefined/null)                                                    |
| `undefined` issue IDs in API calls                   | Trace the navigation/routing — likely a `useParams()` or store selector returning undefined before data loads. Add loading guards. |
| Duplicate/shitty features                            | Audit every page. If a feature is half-implemented, gut it. Ship fewer things that work over more things that don't.               |

**Verification:** Playwright E2E on every page that has a route. Load → no console errors → data renders → interactions
work.

### 1.3 Architecture Verification

Per PRINCIPLES.md: no partial refactors, no suppressed diagnostics.

- `dotnet build qyl.sln` — 0 errors, 0 warnings that aren't explicitly justified
- NUKE build pipeline passes end-to-end
- DuckDB schema is consistent with what the frontend expects
- Every API endpoint the frontend calls exists and returns the correct shape

### 1.4 Done Criteria (Phase 1)

- [ ] Zero 500s on any frontend-reachable endpoint
- [ ] Zero React console errors/warnings
- [ ] Every visible page loads, shows data (or a clean empty state), and navigates correctly
- [ ] Playwright smoke suite passes on: Issues list, Issue detail, Issue events, Analytics dashboard, any Loom dashboard
  pages that exist
- [ ] `dotnet build` clean, NUKE clean

---

## Phase 2 — Loom Implementation

**Prereq:** Phase 1 complete. The platform is stable.

### 2.1 Read & Internalize Seer Source

Source pack location: `/Users/ancplua/sentry-seer-sourcepack/sources/`

Key directories to study:

| Directory                        | Maps to qyl Loom Feature                                  |
|----------------------------------|-----------------------------------------------------------|
| `seer/autofix/`                  | `AutofixAgentService.cs`, `AutofixOrchestrator.cs`        |
| `seer/code_review/`              | `CodeReviewService.cs`, `CodeReviewEndpoints.cs`          |
| `seer/anomaly_detection/`        | `RegressionDetectionService.cs`                           |
| `seer/similarity/`               | Issue grouping (CONTEXT-ONLY for now unless implementing) |
| `seer/assisted_query/`           | `AssistedQueryTools.cs`                                   |
| `seer/explorer/`                 | Interactive debugging agent concept                       |
| `seer/endpoints/`                | API surface reference                                     |
| `seer/entrypoints/`              | Operator/lifecycle pattern reference                      |
| `seer/services/test_generation/` | `TestGenerationTools.cs`                                  |

Web source analysis (already uploaded):

- Sentry blog post — shift-left strategy, MCP server, code review, production RCA
- TechIntelPro — runtime telemetry differentiation, $40/contributor pricing
- Business Wire — CEO quotes, market positioning
- Oreate AI — Seer as copilot framing, privacy guarantees
- Ichizoku JP — Japanese market localization of the same messaging

### 2.2 Reconcile loom-design.md

Current `loom-design.md` status per the Implementation Evidence table:

| Capability                | qyl Status         | What's Left                                                 |
|---------------------------|--------------------|-------------------------------------------------------------|
| Autofix pipeline          | IMPLEMENTED-IN-QYL | Verify E2E flow works, not just endpoint registration       |
| Triage/fixability scoring | IMPLEMENTED-IN-QYL | Verify scoring logic, test with real issues                 |
| Code review               | IMPLEMENTED-IN-QYL | Verify GitHub webhook flow, PR comment generation           |
| GitHub webhook ingestion  | IMPLEMENTED-IN-QYL | Verify HMAC-SHA256 validation, event routing                |
| Agent handoff             | IMPLEMENTED-IN-QYL | Verify lifecycle transitions                                |
| Regression detection      | IMPLEMENTED-IN-QYL | Verify detection + query layer                              |
| Dashboard UI              | IMPLEMENTED-IN-QYL | Covered by Phase 1 frontend fixes                           |
| MCP tooling               | IMPLEMENTED-IN-QYL | Verify all tools work via MCP protocol, not just registered |

**Key action:** For each "IMPLEMENTED-IN-QYL" row, run the actual flow end-to-end. Registration ≠ working. Update
`loom-design.md` with real test results.

### 2.3 Fill Gaps from Seer Analysis

After reading the source pack, identify which Seer capabilities are:

- Already in qyl and working → label `IMPLEMENTED-IN-QYL`, move on
- Already in qyl but stubbed → either implement fully or delete + label `NOT-PLANNED`
- Missing but valuable → design, implement, label `IMPLEMENTED-IN-QYL`
- Missing and not needed → label `NOT-PLANNED` with decision note

Apply SCOPE-TAXONOMY.md labels rigorously. No ambiguous "out of scope" language.

### 2.4 Done Criteria (Phase 2)

- [ ] Every Loom endpoint returns real data (not stubs/mocks)
- [ ] `loom-design.md` updated with verified implementation status per feature
- [ ] Autofix, Triage, Code Review flows tested E2E
- [ ] MCP tools (`AutofixMcpTools`, `TriageTools`, `RegressionTools`, `GitHubMcpTools`, `AgentHandoffTools`,
  `AssistedQueryTools`, `TestGenerationTools`) callable and returning real results
- [ ] Loom dashboard pages pass Playwright

---

## Phase 3 — MCP Connector + Claude Surface Validation

**Prereq:** Phase 2 complete. Loom works. MCP tools return real data.

### 3.1 MCP Server (mcp.qyl.info)

Current state: deployed on Railway, GoDaddy DNS, SSE transport. Needs:

- [ ] Verify all Loom MCP tools are exposed via the SSE endpoint
- [ ] Update `qyl-mcp` skill in `/mnt/skills/user/qyl-mcp/SKILL.md` to reflect current tool inventory
- [ ] Test connection from a raw MCP client (curl SSE, send tool calls, verify responses)

### 3.2 Claude Code (CLI)

- [ ] Configure `.mcp.json` or equivalent to point at `mcp.qyl.info/sse`
- [ ] Verify tool discovery (Claude Code sees all qyl tools)
- [ ] Run a real debugging workflow: trigger an error → use qyl MCP tools to find it → verify the chain works

### 3.3 Claude Web (claude.ai Custom Connector)

Two paths:

1. **Custom connector** via Settings → Connectors → "Add custom connector" pointing at `mcp.qyl.info/sse`
2. **Marketplace submission** (future — requires OAuth 2.1 + Dynamic Client Registration)

For now: custom connector is the ship target.

- [ ] Add custom connector in claude.ai pointing at MCP endpoint
- [ ] Verify tool discovery in conversation
- [ ] Run a qyl query from within a claude.ai chat

### 3.4 Claude Cowork (Desktop)

- [ ] Configure the elegance-pipeline plugin (`ancplua-claude-plugins/plugins/elegance-pipeline`)
- [ ] Verify plugin loads, MCP tools available
- [ ] Run a multi-agent workflow through the pipeline (scout → planner → implementer → verifier → judge)

### 3.5 Done Criteria (Phase 3)

- [ ] MCP server at mcp.qyl.info responds to tool discovery and tool calls
- [ ] Claude Code CLI can use qyl tools in a real session
- [ ] Claude Web (custom connector) can use qyl tools in a real session
- [ ] Claude Cowork runs elegance-pipeline with qyl MCP tools
- [ ] `qyl-mcp` skill file is accurate and current

---

## Worktree Strategy

4 git worktrees, each agent gets their own:

| Worktree      | Focus                                                           | Branch                   |
|---------------|-----------------------------------------------------------------|--------------------------|
| `wt-backend`  | API endpoints, DuckDB, 500 fixes                                | `fix/backend-stability`  |
| `wt-frontend` | React state bugs, undefined IDs, Select controlled/uncontrolled | `fix/frontend-stability` |
| `wt-loom`     | Loom E2E verification, stub removal, seer gap analysis          | `feat/loom-verification` |
| `wt-mcp`      | MCP server, connector setup, skill update                       | `feat/mcp-integration`   |

**Merge rule:** Each branch is 100% tested before merge. No one knows the branch existed because the code is clean.

---

## What NOT to Touch

- AG-UI / CopilotKit integration (Phase 2+ per design doc, not ship-blocking)
- A2A multi-agent routing (explicitly out of scope in design doc)
- Agent-as-MCP-tool surface (phase 2 per design doc)
- Unit test infrastructure (Playwright + Docker is the verification strategy per PRINCIPLES.md)
- Connector marketplace OAuth 2.1 / DCR (custom connector first)
