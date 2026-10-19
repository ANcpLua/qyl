# OpenAI plugin review information

Use these sections for the corresponding review fields. Supply reviewer credentials only in the portal's private access fields.

## What the plugin does

One developer built all of it using Anthropic and OpenAI models to the fullest.

qyl connects ChatGPT to telemetry already recorded by the user's qyl Collector. Users can investigate traces, logs, metric streams, sessions, CI runs, agent executions and MCP calls; compare recorded behavior across windows or releases; and check the evidence for a fix. They can save questions, add annotations and record a failure's status.

Answers use the returned telemetry and its coverage information. An empty result means no matching evidence was returned; it does not establish that the system is healthy.

## Server and access

- MCP endpoint: https://mcp.qyl.at/mcp, using Streamable HTTP.
- Sign-in uses OAuth. Read tools require `qyl:read`; creating or updating annotations, saved queries and failure status also requires `qyl:write`; `delete_record` requires `qyl:read` and `qyl:delete`.
- The server selects the Collector project available to the connected account. Users do not supply an arbitrary project identifier.
- Results are returned as text and structured data with the tool's declared schema.

## Tool catalog

The catalog follows `services/qyl.mcp/tool-manifest.snapshot.json`.

Read operations carry `readOnlyHint: true`:

- Failure investigation: `triage`, `list_failures`, `get_failure_context`, `verify_fix`.
- Execution records: `list_traces`, `get_trace`, `list_sessions`, `get_agent_run`, `ci_log`.
- Logs and metrics: `search_logs`, `list_metrics`, `get_metric_series`, `query_metric`.
- Comparisons and discovery: `aggregate_telemetry`, `compare_groups`, `compare_traces`, `discover_attributes`.
- Operational context: `list_releases`, `get_service_map`, `check_telemetry_health`, `get_mcp_stats`.
- Saved questions: `list_saved_queries`, `run_saved_query`.

Write operations carry `readOnlyHint: false`:

- `create_annotation` and `update_annotation` record or revise a finding.
- `create_saved_query` and `update_saved_query` store or revise a reusable filter.
- `update_failure_status` records a failure's status or assignee.
- `delete_record` deletes an annotation or saved query using its identifier, kind and current `etag`.

The update operations and `delete_record` carry `destructiveHint: true`. The tools do not edit or delete the underlying traces, logs or metrics.

## Skills

The server exposes `qyl-investigate` through `io.modelcontextprotocol/skills`. The client discovers it with `skills/list`, obtains its definition with `skills/get`, and reads the advertised files through `resources/read`. Its main resource is `skill://qyl/qyl-investigate/SKILL.md`.

The skill starts a fresh connection with `triage` and no arguments. If sign-in is requested, it lets the host sign in once and retries that call once before stopping on another authentication request. It then explains how to select and sequence the telemetry tools, preserve identifiers from returned records, and report missing evidence. The plugin bundle carries the same canonical investigation skill.

## Events

The hosted endpoint exposes `events/list`, `events/subscribe` and `events/unsubscribe` for `trace.error`.

The `trace.error` event reports a newly observed trace containing an error span. Delivery uses signed webhooks to the subscribing client's public HTTPS callback. A subscription can filter by `service_name`. The user explicitly chooses whether to subscribe and what ChatGPT should do when a matching event arrives. Event data identifies the trace so ChatGPT can use `get_trace` for the recorded detail. Unsubscribing stops that subscription. New subscriptions establish a baseline rather than replaying old traces; events are not replayed after downtime. Local stdio does not advertise Events.

## How to test

For a fresh connection, first call `triage` with no arguments as directed by the skill; complete sign-in and retry once if requested. Then use the dedicated reviewer project described in the private access fields. It should contain representative traces, error logs, metrics, sessions, CI runs and observed releases.

1. Ask, "What changed in the last 24 hours?" Expect `triage`, with the returned failures, regressions, releases and coverage reflected in the answer.
2. Ask, "List my five most recent traces and inspect the newest one." Expect `list_traces`, followed by `get_trace` using an identifier actually returned by the list. If the list is empty, no identifier should be invented.
3. Ask, "Investigate a recurring failure and show its recorded logs." Expect `list_failures` followed by `get_failure_context` for a returned failure identifier.
4. Ask, "Which metric instruments are recorded, and what does one selected stream show?" Expect `list_metrics`, followed by `get_metric_series` or `query_metric` using the selected instrument and supported aggregation. Original point values, buckets and exemplars are available through `get_metric_series`; `query_metric` returns float64 projections.
5. Ask, "Compare these two observed releases." Expect `compare_groups` using the selected release filters, with the returned group sizes and time windows stated.
6. Ask, "Add a note to this trace saying 'review test', then show it." Expect `create_annotation` and a read that returns the annotation. Ask to remove that exact test note; expect `delete_record` with `kind`, `id` and the current `etag`.
7. Ask, "Save this telemetry question, list my saved questions, then run it." Expect `create_saved_query`, `list_saved_queries` and `run_saved_query` using the returned identifier. Remove the reviewer-created saved query with `delete_record` when finished.
8. Refresh the server's skill catalog. Expect `qyl-investigate` through `io.modelcontextprotocol/skills`, with readable files whose digests match the catalog.
9. Ask, "Notify me when a new error trace appears for this test service." Expect an explicit `trace.error` subscription for that service. Produce the agreed test event in the reviewer project, inspect its trace after delivery, then stop the subscription.

A missing record, unsupported aggregation or insufficient evidence must be reported as such. Neither an empty result nor a pending `verify_fix` verdict establishes that a fix succeeded.

## Data handling

Read operations access the telemetry already stored in the connected account's Collector project. Tool responses return the requested records, aggregates and evidence to ChatGPT. Write operations change annotations, saved queries or failure status; event subscriptions maintain the subscription's own state and deliver matching event data to the subscribing client.

Telemetry retention and attribute handling follow the Collector's configured policy.

Privacy policy: https://qyl.at/privacy/
Documentation: https://qyl.at/docs/mcp/

## Reviewer access — private fields only

Provide a dedicated account and an isolated, populated project. Put the login URL, credentials or access mechanism, and exact sign-in steps in the portal's private fields, never in this document. Include the sample service, recorded release identifiers, a representative metric, and the procedure for producing the test event. Reviewer access must remain available throughout review.
