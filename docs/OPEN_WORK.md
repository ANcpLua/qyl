# qyl — Open Work Items

**Date:** 2026-04-13
**Scope:** Consolidated list of open work carried over from the old `specs/` directory and from the plan/roadmap docs that were deleted on the same day. Architectural decisions already live in `AGENTS.md`, `docs/ARCHITECTURE.md`, and `docs/THREAT_MODEL.md` — this file holds only open items and the non-obvious motivation behind them.

Everything marked **DONE** in the old specs has been deleted without ceremony. If you need historical context beyond what this file preserves, `git log --all --source -- specs/` has the full set.

---

## 1. Data plane — `qyl.collector`

### 1.1 Cost engine (was `specs/cost.md`)

- **Budget alert firings.** Cost thresholds need to write to the `alert_firings` table and fan out via the existing SSE hub. Today cost is computed but silently. Next step: hook a periodic check into the cost aggregation pipeline, reuse the existing `alert_firings` schema, wire a new SSE event type on `/api/v1/live`.
- **Historical cost recompute job.** When pricing changes, `gen_ai_cost_usd` for past spans stays wrong. Next step: an admin endpoint that queues a recompute over a date range, using the pricing table already in place.

### 1.2 Issue fingerprinting split-brain (was `specs/issue-fingerprinting.md`)

- The collector still has **two writer paths** for error ingestion: the legacy `errors` table (via `ErrorLifecycleService`) and the newer `error_issues` + `error_issue_events` (via `IssueService`). They race on `UpsertIssueAsync`.
- Next step: merge into one transactional `RecordOccurrenceAsync()` on `IssueService`, cut the old writer path, delete the `errors` table once no reader depends on it. Sequence matters — schema first, writer second, reader migration third, delete last.

### 1.3 Retention policy (referenced in `specs/00-architecture.md`)

- `QYL_RETENTION_DAYS=0` is hard-coded to "disabled". No automatic TTL, no archival, no VACUUM scheduling. DuckDB grows forever.
- Next step: add a background sweeper that deletes rows older than `QYL_RETENTION_DAYS` for each time-indexed table. Guard with DuckDB `PRAGMA` checkpoint before and after.

### 1.4 Autofix debt (already in `AGENTS.md` known-debt section)

- `collector/Autofix/` still contains embedded Loom intelligence (`LoomOrchestrator`, `LoomDiagnostician`, `LoomStrategist`, `LoomPrompts`). These should live in `qyl.loom`. Collector should only expose `fix_runs` as data endpoints.
- `collector/AgentRuns/` is correct as-is — pure read-only DuckDB queries for agent-run observability.

---

## 2. Serving plane — `qyl.mcp`

### 2.1 Pagination + response size (was `specs/mcp.md`)

- **Cursor pagination missing** on `list_services`, `list_metrics`, `list_error_issues`, `list_triage`. Today they use offset/limit which breaks under concurrent writes.
- **25K token hard truncation** not enforced. `ResponseFormatter.AppendResultCap` coaches the LLM but doesn't actually truncate. Add a real size gate in the MCP middleware pipeline before the response leaves the server.

### 2.2 Interactive apps verification

- `ErrorExplorerTools`, `QueryStudioTools`, `TraceExplorerTools` expose MCP apps (tool + resource pair). They have never been smoke-tested in Claude Desktop. Open one in Claude Desktop, confirm the resource renders, fix whatever breaks.

### 2.3 Entity-ID consistency audit

- When `search_traces` returns a `trace_id`, can `get_trace_details` find it by the same string? When `qyl.list_error_issues` emits a `fingerprint`, does `qyl.get_error_issue` accept that exact shape? Needs one audit pass + one test per tool pair.

### 2.4 Evidence citations in analysis tools

- `qyl.root_cause_analysis`, `qyl.summarize_trace`, `qyl.summarize_error` return prose. They should return prose **plus** an array of `{tool_name, args, result_digest}` citations so the calling agent can verify the claim.

### 2.5 OTel semconv cleanup (carried over from the deleted `docs/convergence-plan.md`)

The collector and qyl.mcp emit some OTel attributes that are either deprecated or not part of semconv 1.40:

- `gen_ai.system` is **deprecated** in semconv 1.40. Replace with `gen_ai.provider.name` where applicable, or drop entirely on `execute_tool` spans (it's not defined there).
- `server.name` is **not a semconv attribute**. Drop it.
- **Missing** on tool-call spans: `gen_ai.tool.call.id`, `gen_ai.tool.description`.
- **Missing** on MCP protocol handler spans: `mcp.protocol.version`, `mcp.session.id`, `jsonrpc.protocol.version`, and `rpc.response.status_code` on error.

Find-and-fix pass: grep the collector + qyl.mcp for `gen_ai.system` and `server.name`, remove. Then add the missing attributes in the MCP message/request filters in `QylMcpServerRegistration.cs`.

### 2.6 Claude-quality generator diagnostics (also from the convergence-plan archive)

Three warnings to add to `qyl.mcp.generators` to enforce tool-description quality at compile time:

| ID | Severity | Message |
|---|---|---|
| QA0011 | Warning | Tool description is fewer than 50 characters — Claude needs 3-4 sentences for reliable tool selection |
| QA0012 | Warning | Parameter description missing or fewer than 10 characters |
| QA0013 | Info | Consider adding input examples for complex tool schemas |

These are additive — the generator already reads `[Description]` from methods and parameters.

---

## 3. UI plane — `qyl.dashboard` (was `specs/dashboard.md`)

- **TanStack Table migration**: `TracesPage`, `LogsPage`, `IssuesPage`, `ErrorsOutagesPage` still use ad-hoc table layouts. Port them to TanStack Table + TanStack Virtual incrementally. The others already use it.
- **Keyboard navigation audit**: partial coverage today (~29 files touch `aria`/`onKeyDown`). No systematic audit has been done.
- **Accessibility semantics audit**: Base UI 1.3.0 gives correct primitives; the app composition on top has not been verified.
- **AI / fact visual distinction**: AI-generated analysis must look visibly different from raw telemetry so operators can tell them apart at a glance. Design decision pending.
- **Realtime handoff streams**: SSE reconnect works. A dedicated handoff event type on `ObservationSubscription` is missing and needed for Loom pipelines to show progress.

---

## 4. Contracts — `qyl.contracts` (was `specs/contracts.md`)

- **Delete hand-written drift.** `SpanRecord.cs`, `OTelEnums.cs`, `PagedResult.cs`, everything under `Copilot/`, and stale `Intelligence/*.cs` duplicate generated equivalents from TypeSpec. Sequence: generate replacements → flip consumers → delete.
- **Copilot namespace.** Rename or delete. The product is Loom; `Copilot` is stale naming that leaks into serializer contexts.
- **Publish `qyl.contracts` as NuGet.** Open question inherited from `specs/00-architecture.md`. External consumers (downstream .NET apps using qyl.mcp contracts) need shared types. Decision still pending.

---

## 5. Telemetry intelligence (was `specs/telemetry-intelligence.md`)

- **Pattern engine before LLM.** Stage 0 of every Loom pipeline should invoke the deterministic `PatternEngine` first and feed its structured evidence pack into the LLM prompt. Today the LLM gets raw telemetry. Wire-up is in `qyl.loom/AutofixAgentService`.
- **`docs/intelligence-model.md`** should be generated from TypeSpec (the same source the DuckDB schema and contracts come from). Today it doesn't exist.

---

## 6. API contract (was `specs/api.md`)

- **RFC 7807 ProblemDetails everywhere.** The collector still returns `{error, message}` on some paths. Replace the error middleware with `Results.Problem(...)` across the board, delete the legacy error DTOs.
- **Canonical collection shape.** Converge on `{items, total, hasMore?, nextCursor?}` for every list endpoint. Audit, then enforce via one JSON contract in `qyl.contracts`.
- **Runtime endpoint inventory.** Derive the endpoint table from ASP.NET metadata at test time instead of the hand-maintained table the old `api.md` kept. A single `EnumerateEndpoints()` test replaces the drift risk.

---

## 7. SDK — `qyl.instrumentation` (was `specs/instrumentation.md`)

- `[Traced]` emits `code.filepath`, `code.function.name`, line number — good.
- **`[GenAi]` and `[Db]` do not.** They need the same code-context enrichment so stack-level filtering works on GenAI and database spans.
- Blocker today: the runtime wrapper for those two attribute families doesn't accept a `CodeContext` struct yet. One change in the generator + one new API in the runtime.

---

## 8. Platform / ops

- **qyl.loom default deployment shape** is still undecided. It runs today as a developer-local exe. It needs a documented "run this as a scheduled job" or "run this as a long-running container" recipe in `eng/compose.yaml`.
- **`NUKE_ENTERPRISE_TOKEN`** is passed to `dotnet nuget add source --store-password-in-clear-text` in `eng/build.sh`. Replace with a NuGet credential provider plugin or env-var-only auth so the token doesn't hit `NuGet.Config` on disk.
- **Secret rotation procedures** are not documented anywhere. One page in `docs/` covering `QYL_*` secrets, their origin, and how to rotate them.

---

## 9. Generator convergence (was `docs/convergence-plan.md`)

**Status:** largely superseded by the 2026-04-12 `[QylSkill]` + `[QylCapability]` refactor. We chose the other direction from the convergence plan: we standardized on `[McpServerToolType]` + `[McpServerTool]` (the official SDK attributes) and layered `[QylSkill]` / `[QylCapability]` / `[QylCapabilityDefinition]` on top. `Qyl.Agents.Generator` from the netagents merge is the convergence target, but the plan's "migrate to custom `[McpServer]` + `[Tool]`" recommendation does not apply anymore.

What **does** carry over:

- The OTel attribute bugs in §2.5 (`DispatchEmitter` emits deprecated/invalid attrs).
- The Claude-quality diagnostics in §2.6 (QA0011/12/13).
- The convergence target itself: `qyl.mcp.generators` should be folded into `Qyl.Agents.Generator` once parity is reached.

---

## 10. Planned features with execute-ready prompts (new, 2026-04-18)

Three planned items from the 2026-04-17 MCP partner-grade work, each captured as a standalone file under `docs/planned/` with outcome, scope, prerequisites, an execute-ready prompt for the next agent or dev, and a DoD checklist. These are **not** prioritized — pull when the prerequisites are met.

| File | Blocked on | Unlocks |
|---|---|---|
| [`docs/planned/2026-04-18-lsp-phase2-typescript.md`](./planned/2026-04-18-lsp-phase2-typescript.md) | None — can start today | TypeScript navigation for `src/qyl.dashboard/` |
| [`docs/planned/2026-04-18-lsp-phase3-loom-bridge.md`](./planned/2026-04-18-lsp-phase3-loom-bridge.md) | Phase 1 in production ≥ 1 week with real usage | Loom uses deterministic code intelligence instead of grep |
| [`docs/planned/2026-04-18-oauth-playwright-e2e.md`](./planned/2026-04-18-oauth-playwright-e2e.md) | All 11 items in `.claude/handoffs/2026-04-17-spec2-ops-followup.md` complete, `mcp.qyl.info` live | Regression-proofs the OAuth handshake; gates MCP-registry listing |

---

## Note on priority

Items in this file are **not** ordered by priority. Cross-reference `docs/THREAT_MODEL.md` §7 for security-driven priorities (5 P0 items there, all of which override anything here). Operational work (§1, §2, §3) is P2 unless tied to a security finding.
