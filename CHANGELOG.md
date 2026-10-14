# Changelog

## 12.2.0 — 2026-10-14

- Collector: spans keep the instrumentation scope name, version and schema URL apart. Ingestion
  stores `ScopeSpans.scope.name` and `.version` in two new span columns (`scope_name`,
  `scope_version`), the API serves `instrumentation_scope` from them instead of from the schema
  URL, and an absent schema URL is stored as NULL instead of `""`; `check_telemetry_health` no
  longer lists an empty schema URL.
- Storage: opening an existing database appends the two nullable span columns in place with
  `ALTER TABLE ADD COLUMN`, leaving the existing indexes untouched; any non-additive schema
  difference still fails closed.
- Build: the gate is split. `mise run check` (`./eng/build.sh Check`) runs before every push:
  compile, tests, verification, SDK and SemConv pins and the dashboard's lint, tests and build.
  `mise run check-full` (`CheckFull`) adds NativeAOT, the pack smoke, the SDK proof, the
  dashboard end-to-end run and the vocabulary smoke. The `Ci` target is gone.
- Build: `VerifySemanticConventionsPin` (in `Check`) fails when the restored
  `Qyl.Telemetry.AutoInstrumentation` was compiled against another
  `Qyl.Telemetry.SemanticConventions` version than `QylSemanticConventionsVersion`. The
  generated collector semantic attribute catalog names the two packages it is generated from.
  Fallout is back on 10.4.0.
- Pin `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 24.1.0, semantic
  conventions 9.9.2, API contracts 12.1.2 and `ANcpLua.Roslyn.Utilities` 2.2.48.

## 12.1.1 — 2026-10-12

- Complete the 32-tool analysis contracts: stable failure identities, continuation cursors,
  validated writes, histogram deltas, exemplars and honest coverage. Share histogram
  calculations, cache immutable protocol inputs by verified digest, and keep the product API
  routes the dashboard uses.
- Viewer refreshes report `mode: live` and embedded trace comparisons are complete; a requested
  trace that does not exist answers 404 instead of an empty view. Invalid analysis responses,
  exceeded deadlines and exhausted scan budgets return contract problem bodies.
- Pin API contracts 12.1.1 and semantic conventions 9.9.1; generate semantic metadata through
  Fallout.

## 12.1.0 — 2026-10-12

- Add the collector analysis engine for qyl.mcp's 32-tool catalog: failure triage,
  evidence and comparison, fix verification, aggregate queries, agent runs, releases,
  service maps, telemetry health, MCP diagnostics and viewer refreshes.
- Apply one project-scoped matcher/window/pagination implementation across direct and
  saved queries. Return measured coverage, denominators and metric units, with signed
  continuation and verification handles.
- Persist failure status and annotation labels/classification atomically, using ETags.
  Add only the new analysis-state table when opening an existing compatible database.
- Pin API contracts 12.1.0; include real OTLP answer-key and HTTP regression coverage.

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).
