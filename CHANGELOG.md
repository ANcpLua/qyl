# Changelog

## 17.0.13

- Codex sessions: the Collector files `conversation.id` (from any producer) under the session, and `thread.id` when it holds a UUID on a record whose instrumentation scope name or `service.name` starts with `codex`. The id is kept as received and the original attribute stays on the record. A .NET `thread.id` such as 12 never becomes a session. Behavior change: a blank `session.id`, `mcp.session.id`, `gen_ai.conversation.id` or `conversation.id` no longer counts as a session.
- A span whose `gen_ai.operation.name` is `fetch_response` keeps it in the operation column and in its attributes. The operation and output-type sets now come from the generated catalog instead of a hand-written list that missed it.
- An OTLP enum value outside its definition is stored as zero with a warning in the partial-success message, and the record is kept: a log `severity_number` outside 0–24 becomes SEVERITY_NUMBER_UNSPECIFIED, a span kind outside 0–5 SPAN_KIND_UNSPECIFIED, a status code outside 0–2 STATUS_CODE_UNSET. Before, severity 25 was clamped to FATAL4, and the span fields fell to 0 without notice.
- Sessions: a session is active by one definition in `GET /api/v1/sessions` and `list_sessions`, and a session listed under `start_time`/`end_time` keeps its whole totals.
- Log search (`GET /api/v1/logs?query=`) finds text with non-ASCII characters, double quotes, backslashes and control characters, in the body and in attributes, and matches `%` and `_` literally.
- `GET /api/v1/metrics/{metric_name}/query` rejects an aggregation that is not one of the contract's names (for example `aggregation=3`), leaves a point at `end_time` out, and answers no bucket past its range. The MCP `query_metric` tool already behaved this way.
- Contract times keep their 100 ns precision instead of whole milliseconds.
- An exception message with an XPath locator is kept instead of `[SANITIZATION_FAILED]`.
- The MCP server calls a Collector behind a path prefix under that prefix.
- The Collector's options bind and validate through `IOptions<T>`; an invalid setting stops the start naming the setting.
- `Qyl.Orchestration`: a `StartAsync` queued behind `QylApplication.DisposeAsync` now fails with `ObjectDisposedException` instead of never completing.
- Page cursors and analysis handles are read as unpadded base64url only. A cursor spelled in the standard base64 alphabet, which no Collector issued, is now refused.
- The analysis `settled` flag compares the newest span's end with the clock at 100 ns precision instead of whole milliseconds, so a span that ended less than the settle window ago is no longer reported as settled.
- Storage reads fail closed: a NULL in a required column throws naming the column instead of reading as `""` or 0, and a HUGEINT aggregate above about 7.9e28 reads as a double instead of overflowing through decimal.
- A GenAI token count written as a string (`"120"`) no longer becomes a number: the token columns stay empty and the string stays on the record. The registry types these counts as int.
- Internal cleanup from a code review, with no change in behavior beyond the three entries above:
  - one OTLP/HTTP ingest handler instead of three copies
  - one canonical segment writer and one entity-ref serializer
  - `Base64Url` for page cursors and analysis handles
  - shared paging and integer-query helpers
  - disposal of the test port reservation
  - analyzer fixes across the tests
  - decoded bool, long and double attribute values stored unboxed
  - decoded values mapped straight to storage's record, so key-value lists are sorted once
  - resource entity refs written without a copy
  - `DuckDbReaderExtensions.cs` renamed to `StorageRecords.cs`

  The JWT header check in the exception sanitizer keeps its lenient base64 decode, so a token with non-zero trailing bits is still redacted.
- The hosted MCP keeps no data: the start no longer deletes the retired MCP Events store, which 17.0.12 removed on its first start, and the service has no volume.
- Qyl.Telemetry.SemanticConventions 15.0.5: the Codex vendor attribute `openai_codex.thread.id` (wire key `thread.id`) and the analyzer fixes of 15.0.4 and 15.0.5.
- The Qyl.Telemetry producer family 28.2.4 (Qyl.Telemetry.Hosting and the AutoInstrumentation packages), built against SemanticConventions 15.0.5.
- Repository: the documentation pages under `docs/` and the plugin skills' reference Markdown are removed (the repository keeps README, AGENTS, SKILL files, notices and the changelog), Rider indexes every root file, AGENTS.md names the skill each test project follows, `.railway/railway.ts` describes deploys without a push trigger, and `eng/auth0/tenant.json` records the first-party consent setting and the DCR security mode.

## 17.0.12

- The Collector's HTTP routes refuse what the MCP refuses, and both name the same field:
  - A value that breaks its contract pattern (an id in the wrong case or shape) is answered with 400 naming the field and the pattern. This covers bodies and path and query parameters; the checks come from the generated `Patterns` and `RouteParameters`. Before, an analysis route or `/api/v1/traces/{trace_id}` answered 404.
  - The write routes (annotations, saved queries, failure status, delete) read their bodies by their contract, as the analysis routes do. A member the contract does not declare, a value of another type, an invalid value and a missing required member are refused naming the field: `<field>.unknown`, `<field>.type`, `<field>.invalid` or `<field>.required` replaces the former `body.invalid`.
  - New refusal codes over HTTP: `<field>.pattern` for body members and path and query parameters, `<param>.length` for path and query ids, and `function.mixed_temporality` (below).
  - With several arguments wrong, both sides name the field the contract declares first.
- Both sides read times by one rule, the RFC 3339 §5.6 date-time:
  - `T` and `Z` in either case
  - a numeric offset with its colon
  - no space for `T`
  - no leap second

  The MCP replaces Ajv's more lenient format.
- `list_metrics` lists one row per metric descriptor (name, kind, unit, temporality, monotonicity) across the resources and scope versions that report it, and says whether a sum is monotonic. Before, every restart or deploy of a service added a row with the same name.
- `query_metric` reads a metric whose series differ in temporality, per series; kind, unit and monotonicity must still agree. A sum's recorded values over mixed temporality (`function` none) are refused naming `function`, with `delta`, `increase` and `rate`.
- `ci_log` takes `phase_limit`, the phases per page of the broken-down run (1 to 50, default 50), beside `phase_cursor`. When a page exceeds the MCP's 24 KB budget, the MCP asks the Collector for fewer phases, and the `next_phase_cursor` resumes at the first phase not returned. Before, the MCP dropped phases from the page and the next page started after them, so they were never shown.
- Storage: the idempotency keys no longer keep a stored response, which nothing has read since 17.0.10. The column is dropped when the database is opened, so a database 17.0.12 has opened can no longer be opened by 17.0.11.
- The OpenAI plugin description names no AI vendor or model.
- MCP Events are removed. The hosted server no longer advertises an `events` capability, stores webhook
  subscriptions or posts to callbacks, and on start it deletes the retired subscription store. Its Auth0
  management client is retired. No supported client used the experimental extension.
- The qyl tool's console drops the open-browser key, whose page no longer exists.
- The README, AGENTS, SECURITY, the MCP server's README (the npm README), the threat model, the
  configuration reference and the package READMEs are rewritten against the code. qyl is free, under the
  MIT license; the plugin bundle is MIT AND Apache-2.0 for its bundled ancplua-dotnet skills.

## 17.0.11

- `query_metric` and a saved metric query's run read only the series and points of the instruments
  they measure: the metric and a ratio's denominator. Before, they read the project's newest 25,000
  metric points across all instruments. A busy instrument could push the measured one out of the
  read. The answer then came back with empty buckets and `scan_complete: false`.
- `check_telemetry_health` reads only the metric points with a conflicting measurement, the ones it
  reports. A project's metric volume no longer makes the report partial or hides an older conflict.
- When `query_metric` finds no series, it says why:
  - the metric was never recorded in the project
  - it has no points in the window, with the time of its last point
  - no series in the window matches `series_id` and `matchers`
- The qyl MCP server exports its `mcp.server.operation.duration` histogram with delta temporality,
  once a minute. Before, it resent every attribute set every 5 seconds, hundreds of thousands of
  points a day in the caller's project. An idle server now exports nothing.

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
