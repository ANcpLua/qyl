# OpenAI plugin review information

Use these sections for the corresponding review fields. Supply reviewer credentials only in the portal's private access fields.

## What the plugin does

Built over ten months by one developer, starting with Claude Opus 4.5, using Anthropic and OpenAI models to the fullest.

qyl connects ChatGPT to telemetry already recorded by the user's qyl Collector. Users can investigate traces, logs, metric streams, sessions, CI runs, agent executions and MCP calls; compare recorded behavior across windows or releases; and check the evidence for a fix. They can save questions, add annotations and record a failure's status.

Answers use the returned telemetry and its coverage information. An empty result means no matching evidence was returned; it does not establish that the system is healthy.

## Server and access

- MCP endpoint: https://mcp.qyl.at/mcp, using Streamable HTTP.
- Sign-in uses OAuth. Read tools require `qyl:read`; creating or updating annotations, saved queries and failure status also requires `qyl:write`; `delete_record` requires `qyl:read` and `qyl:delete`.
- The server selects the Collector project available to the connected account. Users do not supply an arbitrary project identifier.
- Each result is a one-line headline naming the top rows, their ids and the next tool to call, followed by the compact JSON, which is also returned as structured content. Tools declare no output schema. An error names the rejected `field`, the `allowed` values and a `hint`, or the missing record and the tool that lists it.

## Tool catalog

The catalog follows `services/qyl.mcp/tool-manifest.snapshot.json`.

Read operations carry `readOnlyHint: true`:

- Failure investigation: `triage`, `get_failure_context`, `verify_fix`.
- Execution records: `list_traces`, `get_trace`, `list_sessions`, `get_agent_run`, `ci_log`.
- Logs and metrics: `search_logs`, `list_metrics`, `query_metric`.
- Comparisons and discovery: `aggregate_telemetry`, `compare_groups`, `compare_traces`, `discover_attributes`.
- Operational context: `list_releases`, `get_service_map`, `check_telemetry_health`, `get_mcp_stats`.
- Saved questions: `run_saved_query`, which lists the saved queries when called without an id.

Write operations carry `readOnlyHint: false` and `destructiveHint: true`:

- `annotate` records a finding, or revises one with its identifier and current `etag`.
- `save_query` stores a reusable filter, or revises one with its identifier and current `etag`.
- `update_failure_status` records a failure's status or assignee.
- `delete_record` deletes an annotation or saved query using its identifier, kind and current `etag`.

The tools do not edit or delete the underlying traces, logs or metrics.

## Skills

The server exposes `qyl-investigate` through `io.modelcontextprotocol/skills`. The client discovers it with `skills/list`, obtains its definition with `skills/get`, and reads the advertised files through `resources/read`. Its main resource is `skill://qyl/qyl-investigate/SKILL.md`.

The skill starts a fresh connection with `triage` and a `lookback_minutes` that covers the incident (1440 for today), because without it triage reads only the last 60 minutes. If sign-in is requested, it lets the host sign in once and retries that call once before stopping on another authentication request. It then explains how to select and sequence the telemetry tools, preserve identifiers from returned records, and report missing evidence. The plugin bundle carries the same canonical investigation skill.

## How to test

Use the dedicated reviewer account and seeded `review` project supplied in the private access fields. Use `lookback_minutes: 1440`; the sample data is deliberately historical, and the default one-hour window can miss it. The tools that filter by window, such as `list_traces`, `search_logs`, `list_sessions` and `aggregate_telemetry`, take a result's `window` as `start_time` and `end_time`, so one investigation keeps one window; `get_trace` reads the whole trace by its identifier whatever the window.

The project is reseeded every 12 hours, at 00:00 and 12:00 UTC, so a 1440-minute window always holds the latest reseed whole. Each reseed names its checkout releases by its UTC date and hour, 00 or 12: `YYYY.MM.DD.HH.1` introduces the checkout failure and `YYYY.MM.DD.HH.2` fixes it. Ask about the latest checkout release, which `list_releases` lists first, rather than a fixed version: `verify_fix` with it answers `pending` until a hundred checkouts ran it, then `passed`.

The ZIP's five positive cases cover checkout trace discovery and detail, `qyl-mcp` error logs, metric instrument discovery, the five most recent sessions, and telemetry health. The three negative cases check that qyl cannot delete raw telemetry, roll back a deployment, or search the public web. A read-only capability question checks the raw-telemetry deletion boundary.

The demo also creates an annotation on the inspected checkout trace, reads it back, and removes only that new annotation using its current `etag`. The underlying trace remains unchanged.

Demo recording: https://github.com/ANcpLua/qyl/releases/download/v17.0.1/qyl-reviewer-demo-17.0.1.mp4

The reviewer rehearsal used the real hosted server and ChatGPT Work. Native connector readbacks also verified the sample data and annotation cleanup. The initial development connection needed **Refresh tools** after OAuth sign-in; a new conversation then returned the seeded results. ChatGPT Work exposes user-visible results but not raw tool arguments in its activity panel, so the evidence does not claim an argument-by-argument trace.

In 17.0.1, `get_mcp_stats` does not yet receive hosted tool-call telemetry; a zero count does not establish that no calls occurred. It is excluded from the demo and review cases. The engine owner is separately correcting histogram-rate and cumulative-predecessor comparison cases. Original data remains inspectable through `query_metric` with `raw`; do not treat the affected aggregate calculations as verified.

A missing record, unsupported aggregation or insufficient evidence must be reported as such. Neither an empty result nor a pending `verify_fix` verdict establishes that a fix succeeded.

## Data handling

Read operations access the telemetry already stored in the connected account's Collector project. Tool responses return the requested records, aggregates and evidence to ChatGPT. Write operations change annotations, saved queries or failure status.

Telemetry retention and attribute handling follow the Collector's configured policy.

Privacy policy: https://qyl.at/privacy/
Documentation: https://qyl.at/docs/mcp/

## Reviewer access — private fields only

Provide a dedicated account and an isolated, populated project. Put the login URL, credentials or access mechanism, and exact sign-in steps in the portal's private fields, never in this document. Include the sample service, the latest checkout release rather than a fixed version (each reseed names it `YYYY.MM.DD.HH.2` by its UTC date and hour, 00 or 12) and a representative metric. Reviewer access must remain available throughout review.
