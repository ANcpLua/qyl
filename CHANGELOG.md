# Changelog

## 12.2.2 — unreleased

- Collector: a metric range query with a percentile aggregation (`p50`, `p90`, `p95`, `p99`)
  on a gauge or sum answers 400 `aggregation.requires_histogram` on field `aggregation`
  instead of 500; the contract already limits percentiles to histogram kinds.

## 12.2.1 — 2026-10-14

- Security: .NET runtime packages 10.0.13, System.Security.Cryptography.Pkcs and .Xml pinned to it (High advisories of 2026-10-13).

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

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).
