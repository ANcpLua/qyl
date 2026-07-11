# qyl repair plan — execution prompt (phases 1–6)

> Hand-off prompt for the session that executes the SSOT's open-work phases 1–6.
> Written 2026-07-11. Companion to `CLAUDE.md` (the SSOT — its Invariants and
> Open-work sections are binding and auto-loaded; this file adds only ordering,
> evidence targets, and gotchas). Phase 7 (test coverage) is explicitly OUT of
> scope. When a phase lands, append its progress-log entry (with evidence) to
> the SSOT and strike the phase from the Open-work list.

## === QYL REPAIR PROMPT (execute) ===

You are the sole dev repairing qyl toward beta. Work on `main`, single
consolidated commit(s) per phase, push immediately. Evidence rule: report only
what you can point to real command output for — words are claims, tool output
is proof; a fresh-context verifier that *re-executes* beats self-critique.

**Phase-gated. Do NOT batch phases.** Finish a phase (build green +
`./eng/build.sh Verify --Configuration Release` all targets green + CI green on
the pushed commit + SSOT entry) before opening the next. Stop cleanly between
phases if budget runs out — every phase is independently shippable.

### Phase 1 — Data integrity (three real bugs, collector)

1. **DuckDB startup migration.** The schema is source-generated
   (`internal/qyl.collector.storage.generators/DuckDbEmitter.cs`). Emit
   `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` per column, executed at startup,
   from the SAME generator — never hand-edit `*.g.cs`. Fixture ready:
   `services/qyl.collector/qyl.duckdb.pre-selftel.bak` is a June-era DB whose
   stale schema 500s `GET /api/v1/traces` on current code. Evidence: copy the
   .bak in as the live DB, boot, `/api/v1/traces` → 200 with data. Delete the
   .bak afterwards (it's served its purpose as the fixture).
2. **OTLP/JSON hex IDs.** The JSON ingest path decodes trace/span IDs as
   base64 (protojson default for `bytes`) but the OTLP spec mandates **hex**
   for JSON; spec-compliant exporters currently get mangled, unjoinable IDs.
   Fix in the ingest conversion layer (`services/qyl.collector/Ingestion/`),
   add 16-byte/8-byte length validation. Mind the flip side: qyl.mcp's
   `telemetry.ts` deliberately sends base64 today (its comment says "switch to
   hex if pointing at a strict-OTLP collector") — decide ONE contract
   (spec-hex, with or without a base64 tolerance window), fix both ends in the
   same phase, and record the decision. Evidence: POST a spec-hex JSON span →
   stored, joinable by ID via the API; wrong-length ID → clean reject, not 500.
3. **Real `/health`.** `MapQylEndpoints` is never called and
   `DuckDbHealthCheck` is never registered — the SPA fallback returns
   `index.html` 200 for `/health`, fooling Railway's `healthcheckPath` AND
   `Qyl.Run`'s readiness probe. Map a real health endpoint BEFORE the SPA
   fallback. Evidence: `/health` returns health JSON; with the DB broken it
   returns unhealthy/503; an unknown route still serves the SPA.

### Phase 2 — Product surface (decision already made: shrink to verified)

Shrink the dashboard to the verified vertical — **traces, sessions, logs,
GenAI cost** — and DELETE pages with no backing endpoint. No adapters, no
stubs: missing values stay missing; pages return only when a real endpoint
ships. Ground truth: the collector serves exactly 13 `/api/v1` routes (traces,
sessions, logs, profiles + stream/logs) — enumerate from source, not README.
Update the root README "Product surface" section to match the shrunk reality.
Evidence: dashboard `npm run build` + tests green; no nav link leads to a
deleted page; grep for the deleted pages' route names → 0 hits.

### Phase 3 — One topology

Embedded single-origin collector: `QylEmbedDashboard=true` for release builds;
delete the standalone dashboard Docker path; fix compose. Evidence: a release
build serves the dashboard from `:5100` (one origin); `docker compose up`
(or the surviving compose file) works end-to-end; the deleted path stays
deleted (add to `removedCollectorTokens` only if it had build-surface tokens).

### Phase 4 — Qyl.Host convergence (remaining steps, in order)

Per `docs/design/qyl-host/DESIGN.md` §Migration path. Steps 1 (`AddCommand`)
and 4 (`WaitFor`; `withReference` deliberately cut) shipped 2026-07-11 (#510).
Remaining, each independently shippable:
- **Step 2 — `IReadinessProbe`**: extract today's `GET /health` poll behind an
  interface (`HttpHealthProbe` default, behaviour unchanged). One method:
  `Task<bool> IsReadyAsync(QylResourceState, CancellationToken)`.
- **Step 3 — `Qyl.Host.Mcp`**: `McpHandshakeProbe` (initialize + tools/list),
  `/runner/mcp` passthrough, and a C# port of qyl.mcp's `telemetry.ts`
  (one CLIENT span per passthrough call; `mcp.*` + `gen_ai.tool.name` keys).
  The TS reference is `qyl-workspace/qyl.mcp/runner/src/` — port the DESIGN,
  don't wrap the TS. qyl.mcp's `inproc` kind maps to `Func<IMcpServer>`.
- **Step 5 — Console convergence**, **Step 6 — rename `Qyl.Run`→`Qyl.Host`**
  (mechanical, LAST — rename cost is zero externally: not on nuget.org, only
  consumer is `Qyl.Run.Host`).
Keep `IsAotCompatible=true` on the engine. qyl.mcp stays the TS MCP surface —
this phase is the C# engine only.

### Phase 5 — Auth + scoping

- gRPC ingest has NO API-key boundary (HTTP does) — mirror the HTTP behaviour
  as a gRPC interceptor; same `QYL_OTLP_AUTH_MODE` modes.
- The read API (`/api/v1/*`) is unauthenticated — add auth consistent with the
  OTLP key model; decide and record the scheme.
- `ProjectScope.cs` hardcodes `"default"` — make scoping real.
Constraints: local dev must keep working (Qyl.Run defaults loopback children to
`QYL_OTLP_AUTH_MODE=Unsecured`; the dedicated diagnostics sink FORCES Unsecured
— do not break `QylSelfTelemetryBuilder`'s wiring); production is fail-closed.
Evidence: matrix of {gRPC, HTTP} × {Unsecured, ApiKey} × {key, no key} runs
with the expected accept/reject for each cell; `Qyl.Run.Host` composition still
boots both collectors healthy.

### Phase 6 — Release coherence

`0.1.0-beta.1` is not stamped — the only pack artifact is
`Qyl.Run.1.0.0.nupkg`. One version owner (`Version.props`), stamp flows to
every packable project. Pipeline: verify → pack → (publish gate STAYS CLOSED —
SSOT: nothing ships publicly until experimental public beta) → local-feed
index → clean-restore-verify. Trap: `nuget.config` `<clear/>`s to a single
source — a local feed must be added WITH a matching `packageSourceMapping`
entry or restore fails (NU1100/NU1507). Evidence: pack output shows
`*.0.1.0-beta.1.nupkg` for exactly the intended shippables; a clean restore
from the local feed resolves them.

### Standing constraints (beyond the SSOT invariants)

- Stage **explicit paths** only — never `git add -A`: parallel sessions share
  this checkout and leave uncommitted work.
- Local collector runs need `QYL_OTLP_AUTH_MODE=Unsecured` (children
  crash-loop as "health probe timed out" otherwise) — until Phase 5 changes
  the defaults deliberately.
- Contracts stay single-sourced: if a phase needs a new public API shape, it
  goes through `qyl-api-schema` (TypeSpec) → `Qyl.Api.Contracts`, not a model
  in the collector.
- Phase 7 (test coverage) is out of scope, BUT if a small test project is the
  cheapest honest evidence for a phase (Phase 1 migration, Phase 5 auth
  matrix), creating one on Microsoft.Testing.Platform is allowed — coverage
  goals are not.
- Anything set aside instead of committed: tag it
  (`archive/stash-...` pattern), never leave a bare stash.

Sequencing inside each phase is yours. Stop only on a blocker your tools
cannot resolve. When done (or out of budget), the SSOT progress log must let
the next session resume without this file's session context.

## === END PROMPT ===
