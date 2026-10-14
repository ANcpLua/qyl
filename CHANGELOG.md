# Changelog

## 13.0.0 — unreleased

- Pin `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 25.0.0 and semantic conventions
  10.0.0. Their analyzers now reach every project that references `Qyl.Telemetry.Hosting`.
- Collector: the ingest deny list is SemConv's generated `RegistryPolicy`. The catalog generator reads it,
  `eng/config/collector-semantic-policy.json` loses `deniedExactPrefixes`, `deniedExactKeys`,
  `deniedKeyTokens`, `deniedTokenExemptKeys` and `httpHeaderAttributePrefixes` (a denied template key such
  as `http.request.header` denies its children), and generation fails when the file names a key for
  persistence that the registry does not persist.
- Collector: `gen_ai.agent.name` is persisted, so `get_agent_run`'s `agent_name` and `list_sessions`
  name the agent; `gen_ai.agent.id` and `gen_ai.agent.description` stay denied. The five
  `gen_ai.usage.*_tokens` counts are token-exempt and are kept as attributes beside their columns.
- Collector: business attributes `qyl.app.<key>` (written by `SetAppTag`) are captured: at most 16 keys
  per span, each a string of at most 256 characters, a 64-bit integer, a double or a bool, and no key
  containing a denied token.
- Collector: a metric range query with a percentile aggregation (`p50`, `p90`, `p95`, `p99`)
  on a gauge or sum answers 400 `aggregation.requires_histogram` on field `aggregation`
  instead of 500; the contract already limits percentiles to histogram kinds.
- Collector: the analysis `query_metric` operation (MCP `query_metric`) rejects a percentile on a gauge or
  sum with 400 `aggregation.requires_histogram` instead of answering with the average labelled as the
  percentile; an aggregation the metric kind does not support answers `aggregation.unsupported`.

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
