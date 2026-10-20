# Changelog

## 17.0.10

- The Collector's HTTP analysis routes now refuse the same input the MCP refuses, with the same
  typed `invalid_argument` naming the field. That covers:
  - a value outside the contract's range or length
  - an argument the contract does not declare
  - a value of another type, or outside its closed set
  - an object that no form of a union declares (`group_b`, a `baseline` window)

  Ranges, lengths and defaults are generated from the contract (`Ranges` and `Lengths` per request
  body), so the engine keeps no bounds of its own. A refusal names the accepted range and the
  default that applies when the argument is omitted.
- New contract bounds:
  - `baseline.offset_minutes` 1–43200
  - `aggregate_telemetry` `step_ms` 1–86400000, like `query_metric`'s
  - `verify_fix` `min_requests` and `min_minutes` at least 0, with their defaults 100 and 30
    declared
  - `list_sessions` `min_tokens` at least 0

  Values that used to answer a 500, or were accepted silently, are refused.
- `POST /api/v1/saved-queries/{id}:run` refuses `lookback_minutes`, which its contract does not
  declare.
- A repeated create answers with its record as it is now, including the current etag, and creates
  the record again if it was deleted. Deleting a record removes its idempotency keys. This covers
  `save_query`, `annotate` and an HTTP create with the same `Idempotency-Key`. Before, saving a
  query within 24 hours of deleting the same query returned the deleted record.
- `check_telemetry_health`'s `data_span` follows the report's service scope, which excludes `qyl.mcp`
  by default.
- `ci_log` pages a run's phases 50 at a time, as documented, whatever `limit` pages the runs.
- A window that starts at the Unix epoch no longer answers a 500 in `triage`,
  `check_telemetry_health`, `get_mcp_stats` or `compare_groups`.

## 17.0.9

- `triage` and `check_telemetry_health` leave out the qyl MCP server's own telemetry (service
  `qyl.mcp`) unless `service_name` is `qyl.mcp`, so a caller's own tool errors and idle periods
  no longer show up as failures or a silent service; `get_mcp_stats` and the reads still show it.
- `query_metric` and `run_saved_query` bound `lookback_minutes` to 1–43200 like every other tool.
  `query_metric` no longer writes `start_time` and `end_time` outside its contract; the HTTP
  saved-query run takes a metric answer's bounds from the run's window.
- Contracts: triage rows and failure summaries share one fingerprint and state, `query_metric`
  and a saved metric run share one metric answer, and the silent-service rule is stated once.
  The shared analysis arguments are TypeSpec aliases, so `AnalysisInput` and
  `ServiceAnalysisInput` are no longer tool documents, C# classes or TS types; every string enum
  has a `<Enum>Values` constants class in C#, as in TypeScript.
- The hosted reviewer project is reseeded every 12 hours, at 00:00 and 12:00 UTC, and its releases
  are named by the slot, `YYYY.MM.DD.HH.N`, so a 1440-minute window always holds the latest
  reseed.
- The answer key covers saved queries, `delete_record`, repeated `verify_id` calls and baselines,
  checks the seed's expected values and probes the reviewer answers across a whole reseed slot.

## 17.0.8

- `verify_fix` with `release` anchors the change on the first recorded span of that version in
  the failure's service, found with a bounded query, so a release older than what a read loads
  is still found; without a recurrence the verdict stays inconclusive while spans after the
  change were not all read.
- `get_trace` returns the newest `max_logs` log records, as documented. `list_traces` with
  `failure_id` reads its own window instead of all retained data. `get_failure_context` without
  a root cause no longer runs a whole triage for its changes.
- A saved metric query runs with a limit up to 1000, and the HTTP saved-query run reads through
  the same read plan as the tool.
- Errors: an unknown argument's hint names the accepted argument names; a rejected etag names
  the field `etag`; a verifier's OAuth failure keeps its own status (429, 503; an unknown code
  is a 500) without a sign-in challenge.
- The plugin listings no longer claim MCP Events, which mcp.qyl.at does not expose, describe the
  24-tool 17.0.x surface and carry current release notes; annotation targets include a failure.
- The answer-key traffic calls only tools that exist.

## 17.0.7

- While the Collector does not answer `/health` (a deploy or an outage), tools answer
  `collector_unavailable` at once with `retry_after_seconds` instead of waiting out the request
  timeout, and calls already waiting are released.
- The answer-key seed names the failing path, the status and the Collector's answer when an
  export is refused.
- `release.sh` gives each NuGet push 30 minutes, so a slow upload no longer stops a release.

## 17.0.6

- A rejected value of an argument with alternatives (`compare_groups` `group_b`, every `baseline`)
  lists the accepted values and object form in `allowed`, with a valid example in `hint`; a
  JSON-quoted allowed string such as `"\"rest\""` is accepted as `rest`.
- `verify_fix` resolves `release` within the failure's own service (a dependency's failure takes
  the release of the service calling it) and reports the anchor it used as `change`.
- The MCP server records a call it rejects as the caller's mistake (invalid arguments, unknown ids
  or tools, missing access, a stale etag) without error status, so such calls no longer rank as
  failures in the caller's triage; Collector outages, timeouts, 5xx and contract mismatches stay
  errors. A JSON-RPC error's `error.type` is its code.
- Answer-key seed: checkout ships its fix as release 1.4.2 after the last orders-db timeout and
  later checkouts run it, and seeded durations vary around their values (the routes' p95 stay
  1.84 s and 0.21 s). With `QYL_ANSWER_KEY_FRESH_IDS=1` a reseed names its releases by its UTC
  date (`YYYY.MM.DD.n`), so each day's incident and fix are their own releases, and
  `QYL_ANSWER_KEY_MODE=traffic` sends healthy traffic on the latest fix release.

## 17.0.5

- 24 tools instead of 29, without aliases. `triage` takes over `list_failures` with a `status`
  filter (default `open`, which also covers regressed failures and ignored ones past
  `ignore_until`). `list_metrics` shows each instrument's attribute keys, their cardinality and
  `suggested_group_by`, and `query_metric` with `raw` returns one series' recorded points
  (`get_metric_series` is gone). `run_saved_query` lists the saved queries when `id` is omitted
  (`list_saved_queries` is gone). `save_query` and `annotate` create without an `id` and update
  with `id` and `etag` (`create_saved_query`, `update_saved_query`, `create_annotation` and
  `update_annotation` are gone).
- One name per concept across the tools: `query` for substring search, `*_prefix` for prefixes,
  `baseline` for every comparison window (`previous_window`, `{start_time, end_time}` or
  `{offset_minutes}`; `ci_log` takes `last_success`), `group_by` as a list, `order_by`,
  `errors_only`, `limit` and `ci_run_id`. Answers use `duration_ms` and RFC 3339 times.
- Coverage reports only what an answer read: `scan_complete` is true when the read was
  complete, and `truncated` only when results were cut.
- Empty and repeated fields are gone from the answers, including `mode`, which was always `live`.

## 17.0.4

- Results read the way an agent reads them: a one-line headline naming the top rows, their ids
  and the next tool, then the compact JSON, also as `structuredContent`. No tool advertises an
  `outputSchema` (tools/list 425 KB → 132 KB), so a client that cached the catalog no longer
  rejects a result. A rejected argument names its `field`, the `allowed` values and a `hint`; a
  404 names the missing record (`resource_type`, `resource_id`) and the tool that lists it.
- Compact shapes, no aliases: `list_traces` rows carry trace id, root service and name, start,
  `duration_ms`, span and error counts, services, failure ids and session (10 per page,
  `group_by` removed); `get_trace` spans are compact rows with `duration_ms` and each service's
  resource once (`errors_only` and `span_roles` removed); log records are `{time, severity,
  service, body, trace_id, span_id, attributes}`; `search_logs` with `group_by: pattern` returns
  patterns only; `list_metrics` rows are compact (50 per page); `get_failure_context` lists each
  span once and refers to it by id; `compare_traces` differences are `{kind, key, a, b}`;
  `get_mcp_stats` returns buckets only with `buckets: true`.
- `triage` takes a failure's release from its failing spans, reports a service as silent by its
  usual interval, and gives each failure its `status`, `etag` and `root_cause_failure_id`.
  `verify_fix` compares the change with the failure's earlier history. `query_metric` picks at
  most 60 buckets when `step_ms` is omitted and returns only occupied buckets plus one value per
  series. `check_telemetry_health` measures ingestion lag from receipt time, reports `data_span`
  instead of `retention`, and answers an unknown service with `service.not_found`.
- Corrected: `list_traces` sorts by duration and errors; release cohorts stay within their
  environment; a capped read no longer claims an exact total or cardinality; `get_trace` finds
  linked spans of other traces; a histogram rate divides each series' deltas by its recorded
  intervals (`function.unsupported` for other combinations); a previous-window comparison keeps
  the cumulative point before the window; a span whose parent is remote is not an orphan;
  `compare_groups` leaves out a failure cohort's own keys and rejects an empty group;
  `aggregate_telemetry` rejects an unrecorded `group_by` key.
- Every tool and every parameter is described: when to use the tool instead of its neighbour,
  which arguments go together, and the defaults.
- The hosted MCP records a span for every call in the caller's project, also when the caller's
  trace is unsampled, so `get_mcp_stats` counts hosted callers; spans carry
  `mcp.protocol.version`, `jsonrpc.request.id` and the client identity.
- Auth: a request without credentials gets the bare Bearer challenge (RFC 6750 §3.1); a rejected
  token gets 401 `invalid_token`, never 500; tokens are checked against the resource.
  `skills/list` carries `ttlMs` and `cacheScope` for 2026-07-28 clients, and the server
  instructions are eight routing lines.
- Ingestion keeps registry enum attributes whose names hold a redacted token, such as
  `cicd.pipeline.result` (Qyl.Telemetry.SemanticConventions 15.0.3).

## 17.0.3

- Paging follows the contract: every tool declares its page size (20 groups for
  `aggregate_telemetry`, 10 for `triage`, 100 elsewhere), `aggregate_telemetry.other` counts the
  groups after the page and their records, and a continuation whose earlier items changed fails
  with `cursor.stale` instead of skipping or repeating one.
- Matcher `eq`, `neq` and `in` compare typed values, so 1 does not equal "1".
  `AttributeValueCount.value` is an `AttributeValue`, so discovered integers keep every digit.
- A metric timestamp that receives a different measurement keeps the first one and is marked
  conflicting; both observations stay readable, an identical retry changes nothing, and
  `check_telemetry_health` reports `metric_conflicts`.
- The REST reads reject a window boundary finer than 100 ns with `time.precision_unsupported`,
  as the analysis tools do.
- Exception text is sanitized without a wall-clock deadline, so a loaded machine no longer turns
  valid text into `[SANITIZATION_FAILED]`.
- `get_failure_context` states that it takes exactly one of `trace_id`, `session_id`,
  `failure_id` or `ci_run_id`.

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
