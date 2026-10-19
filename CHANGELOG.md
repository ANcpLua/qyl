# Changelog

## 17.0.2

- An analysis window keeps its exact boundaries: a boundary finer than the 100 ns recorded times
  hold is rejected with `time.precision_unsupported` instead of being rounded, recorded times are
  reported to 100 ns instead of milliseconds, and failure-regression and release checks compare
  nanoseconds.
- `run_saved_query` reports the coverage of the operation its saved filter ran.
- `release.sh` creates the GitHub release `v<version>` from the version's changelog section.

## 17.0.1

- verify_fix scopes requests and failures by the same matchers and never passes on an incomplete scan.
- get_failure_context keys spans by trace and span id; compare_groups applies the requested window to failure cohorts.
- query_metric answers only what recorded points establish.

## 17.0.0

- One repository: the MCP server (formerly ANcpLua/qyl.mcp) is `services/qyl.mcp`, the TypeSpec
  API contracts (formerly ANcpLua/qyl-api-schema) are `contracts/`, beside the Collector, the qyl
  tool, Qyl.Api.Sdk and Qyl.Orchestration. Earlier history is behind the tag
  `archive/pre-monorepo-2026-10-19`.
- One version: `Version.props` `QylVersion` versions every package; `qyl-mcp-server` moves from
  11.1.4 to 17.0.0 with the rest. `release.sh` packs the whole set once into
  `artifacts/release/<version>/` with a SHA-512 manifest, publishes only what nuget.org and npm do
  not have yet, and tags `v<version>`; a rerun resumes without repacking.
- qyl-mcp-server: the viewers (`display_traces`, `display_mcp_dashboard`, the app-only
  `fetch_telemetry`) and their `ui://` resources, the Workbench and the landing page are gone; `/`
  answers 404 while `/mcp`, `/healthz`, the OAuth discovery documents and the OpenAI domain
  challenge remain. `delete` is renamed `delete_record`, without an alias. A completed or failed
  execution record that cannot be written no longer replaces the tool's outcome. The library
  exports only `qyl-mcp-server` and `qyl-mcp-server/auth`.
- The MCP answer key and project-isolation smoke run against the Collector built from the same
  commit, in one `Check` with the .NET tests and the contract checks.
- Contracts are generated in this repository: `Qyl.Api.Contracts` is built from
  `internal/qyl.contracts` and released at `QylVersion` (14.0.1 before), and the MCP reads its
  types, header names and tool schemas from `services/qyl.mcp/src/contracts/generated` instead of
  `@ancplua/qyl-api-schema`. `./eng/build.sh Contracts` regenerates them; `Check` fails when a
  generated file differs from the TypeSpec, and `check.yml` runs `CheckFull` on a manual dispatch.
- API: the Workbench routes (`/workbench/*`), `BadGatewayError` and the viewer analysis routes
  (`display_traces`, `display_mcp_dashboard`, `fetch_telemetry`) are gone from the contract and
  the Collector.
- `get_mcp_stats` adds `bucket_ms` with 60 `buckets` of requests and errors, `by_server`,
  `by_transport`, `by_method`, per-tool `tools` (requests, errors, error rate, average and p95
  latency), `span_count_analyzed`, `truncated` and `mode`.
- qyl-mcp-server: every tool registers its generated JSON Schema with one strict Ajv, and
  arguments travel as given; the Collector applies the advertised defaults, so `search_logs`
  returns 50 records without a `limit` (100 before) and a saved-query run 100 (20 for traces
  before). A Collector serving another `contract_revision` no longer stops the server: tools answer
  `collector_contract_mismatch` with both revisions and `/healthz` answers `degraded` until they
  match.
