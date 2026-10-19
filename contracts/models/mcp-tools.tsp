import "./mcp-analysis.tsp";
import "../common/telemetry-query.tsp";
import "../common/errors.tsp";
import "../common/types.tsp";
import "../otel/logs.tsp";
import "../otel/span.tsp";
import "./session.tsp";
import "./metrics.tsp";
import "./metric-points.tsp";
import "./annotations.tsp";
import "./saved-queries.tsp";
import "../common/resource.tsp";

using TypeSpec.OpenAPI;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Annotations;
using Qyl.Api.Contracts.SavedQueries;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Traces;

namespace Qyl.Api.Contracts.Mcp.Tools;

@doc("Whether a qyl MCP tool read live collector data or the explicit demo dataset.")
enum McpDataMode {
  live: "live",
  demo: "demo",
}

@doc("""
  OAuth scopes of the qyl MCP server. `qyl:read` alone keeps the read-only
  tool surface; `qyl:write` adds the create and update tools; `qyl:delete`
  adds the one delete tool. No scope deletes telemetry.
  """)
enum McpScope {
  read: "qyl:read",
  write: "qyl:write",
  delete: "qyl:delete",
}

@doc("Stable failure code of a qyl MCP tool result. A model branches on the code, never on the message.")
union ToolErrorCode {
  invalidArgument: "invalid_argument",
  notFound: "not_found",
  noProjectAccess: "no_project_access",
  collectorUnreachable: "collector_unreachable",
  collectorTimeout: "collector_timeout",
  cancelled: "cancelled",
  collectorUnauthorized: "collector_unauthorized",
  collectorRateLimited: "collector_rate_limited",
  collectorRejected: "collector_rejected",
  collectorError: "collector_error",
  contractMismatch: "contract_mismatch",

  @doc("The Collector serves another contract revision than this MCP server was generated from, as during a deploy of one of the two; `collector_contract_revision` and `server_contract_revision` name both. Retry once both serve one revision.")
  collectorContractMismatch: "collector_contract_mismatch",

  @doc("One complete original record exceeds the MCP response budget. It remains available through the exact HTTP API; it was not truncated or rewritten.")
  responseTooLarge: "response_too_large",

  @doc("The token lacks the scope the tool needs: qyl:write or qyl:delete.")
  insufficientScope: "insufficient_scope",

  @doc("HTTP 409: the change conflicts with existing state, e.g. a duplicate saved-query name.")
  conflict: "conflict",

  @doc("HTTP 412: the etag is stale; re-read the resource and retry with its current etag.")
  preconditionFailed: "precondition_failed",

  @doc("HTTP 428: the request carried no etag.")
  preconditionRequired: "precondition_required",

  internal: "internal",
}

@doc("A failed tool call, carried in structuredContent.error.")
model ToolError {
  @doc("Every rule the arguments broke, as the input schema or the Collector reported them.")
  fields?: ValidationErrorDetail[];
  @encodedName("application/json", "problem_type")
  problemType?: ProblemType;

  code: ToolErrorCode;

  @doc("Human-readable description of this failure.")
  message: string;

  @doc("Whether the same call can succeed when repeated unchanged.")
  retryable: boolean;

  @doc("The argument to change, by its path such as `status`, `filter.signal` or `matchers[0].op`: the first one the input schema or the Collector rejected.")
  field?: string;

  @doc("The values `field` accepts, or for an argument the tool does not take, the arguments it does; present when they form a closed set.")
  allowed?: (string | float64 | boolean)[];

  @doc("The next step: what to change, or which tool returns the ids the call needs.")
  hint?: string;

  @doc("With not_found: the kind of record that was not found, such as `trace`, `failure`, `metric` or `saved_query`.")
  @encodedName("application/json", "resource_type")
  resourceType?: string;

  @doc("With not_found: the identifier that was not found.")
  @encodedName("application/json", "resource_id")
  resourceId?: string;

  @doc("With collector_contract_mismatch: the contract revision the Collector serves; null when its health names none.")
  @encodedName("application/json", "collector_contract_revision")
  collectorContractRevision?: string | null;

  @doc("With collector_contract_mismatch: the contract revision this MCP server was generated from.")
  @encodedName("application/json", "server_contract_revision")
  serverContractRevision?: string;
}

@doc("Pagination members of a paged tool output.")
model ToolPage {
  @doc("Whether more items exist beyond this page. False only when the result is complete; `next_cursor` is present exactly when this is true.")
  @encodedName("application/json", "has_more")
  hasMore: boolean;

  @doc("Pass as `cursor` to the same tool with the same filters for the next page. Absent on the last page.")
  @encodedName("application/json", "next_cursor")
  nextCursor?: Cursor;
}

@doc("Time-range members of a tool input. Omit all three for the tool's default window ending now; the result's `window` reports the bounds that were applied, so a follow-up call can pass them as `start_time` and `end_time` to keep the same window.")
model ToolTimeRange {
  @doc("Minutes before `end_time` (or now) when `start_time` is omitted, at most 43200 (30 days). Default 60, unless the tool's input description names another default.")
  @minValue(1) @maxValue(43200)
  @encodedName("application/json", "lookback_minutes")
  lookbackMinutes?: int32;

  @doc("Start of the window (inclusive), RFC 3339; takes precedence over `lookback_minutes`.")
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;

  @doc("End of the window (exclusive), RFC 3339; default now.")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;
}

@doc("Finds executions: one compact row per trace that started in the window, with its root service and span name, start, duration in ms, span and error counts, services, failure ids and session. Use it to get the trace ids that get_trace, compare_traces and get_failure_context take; aggregate_telemetry computes numbers per group and compare_groups explains what differs between two populations. Default: the last 60 minutes, 10 traces per page, newest first.")
model ListTracesInput {
  @doc("Keep traces with an error span of this failure class, the classification get_mcp_stats reports.")
  @encodedName("application/json", "failure_class")
  failureClass?: McpFailureClass;
  @doc("Keep traces with a span whose `mcp.server.name`, or service name, is this value.")
  @encodedName("application/json", "server_name")
  serverName?: string;
  @doc("Keep traces with a span of this tool (`gen_ai.tool.name`).")
  @encodedName("application/json", "tool_name")
  toolName?: string;
  @doc("Keep traces with a span whose `mcp.transport`, or `network.transport`, is this value.")
  transport?: string;

  @doc("Keep traces with a span in this session.")
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  @doc("Keep traces whose root span has exactly this name.")
  @encodedName("application/json", "root_span_name")
  rootSpanName?: string;
  @doc("Keep traces lasting at least this many milliseconds, from the earliest span start to the latest span end.")
  @encodedName("application/json", "min_duration_ms")
  minDurationMs?: DurationMs;
  @doc("Keep traces lasting at most this many milliseconds.")
  @encodedName("application/json", "max_duration_ms")
  maxDurationMs?: DurationMs;
  @doc("Keep traces with a span of this release (resource `service.version`). With `service_name` or `matchers`, one span must satisfy all of them.")
  release?: string;
  @doc("Keep traces in which this failure occurred, e.g. a `failure_id` from triage.")
  @encodedName("application/json", "failure_id")
  failureId?: FailureId;
  @doc("With `errors_only`: `any` (default) keeps traces with any error span, `root` only traces whose root span failed.")
  @encodedName("application/json", "error_scope")
  errorScope?: "any" | "root";
  @doc("Keep traces with an annotation carrying this label, on the trace or one of its spans.")
  @encodedName("application/json", "annotation_label")
  annotationLabel?: string;
  @doc("Order of the traces: `start_time` (default) newest first, `duration` longest first, `errors` most error spans first. Equal values keep the newest trace first.")
  @encodedName("application/json", "sort_by")
  sortBy?: "start_time" | "duration" | "errors";

  @doc("Attribute predicates one span of the trace must all satisfy, together with `service_name` and `release`.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  @doc("Traces per page, 1 to 100; default 10.")
  @minValue(1)
  @maxValue(100)
  limit?: int32 = 10;

  ...ToolTimeRange;

  @doc("Keep traces with at least one span from this service.")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Keep only traces with at least one error span, or with a failed root span under `error_scope: root`.")
  @encodedName("application/json", "errors_only")
  errorsOnly?: boolean;

  @doc("`next_cursor` of a previous list_traces call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for list_traces.")
model ListTracesOutput {
  ...ToolReadContext;

  traces: ToolTraceRow[];
  mode: McpDataMode;
  ...ToolPage;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Reads one trace: its time range, span and error counts, services, the spans `selection` picks as compact rows (service, name, start, duration in ms, status, attributes), each service's resource once, links, annotations and optionally correlated logs. Use it on a trace id from list_traces, triage or get_failure_context; get_failure_context already explains a failed trace and compare_traces compares two. The whole trace is read whatever the time range; its spans page by `max_spans`, default 100.")
model GetTraceInput {
  @doc("`next_cursor` of a previous get_trace call for the same trace and arguments; continues its spans and its other paged lists.")
  cursor?: Cursor;

  @doc("Which spans to return: `all` (default), `errors` (error status only), `errors_with_ancestors` (error spans plus every span on their path to the root) or `critical_path` (from the root, each span's longest child, each with `self_time_ms`). Start with `errors_with_ancestors` when diagnosing a failure and widen to `all` only when the error path is not enough.")
  selection?: "all" | "errors" | "errors_with_ancestors" | "critical_path";

  @doc("Attribute keys to keep on each returned span; omit for every recorded attribute.")
  attributes?: string[];

  @doc("Number of correlated log records to include, 0 to 50. Default 0, no logs; search_logs with the same `trace_id` returns the rest.")
  @minValue(0) @maxValue(50)
  @encodedName("application/json", "include_logs")
  includeLogs?: int32;

  ...ToolTimeRange;

  @doc("The trace to read, 32 lowercase hex characters, e.g. from list_traces.")
  @encodedName("application/json", "trace_id")
  traceId: TraceId;

  @doc("Maximum spans per page after `selection` is applied. When more spans match, `has_more` is true and `next_cursor` continues the same trace; `page_totals` counts every selected span.")
  @encodedName("application/json", "max_spans")
  @minValue(1)
  @maxValue(1000)
  maxSpans?: int32 = 100;

  @doc("Include each span's attributes and each service's resource attributes; default `true`. `false` returns the spans without them.")
  @encodedName("application/json", "include_attributes")
  includeAttributes?: boolean = true;
}

@doc("Structured output for get_trace.")
model GetTraceOutput {
  ...ToolPage;
  @encodedName("application/json", "missing_parent_span_ids")
  missingParentSpanIds?: SpanId[];
  links?: TraceLinkAvailability[];
  @doc("The trace's log records, `include_logs` of them, newest first.")
  logs?: ToolLogRow[];
  settled?: boolean;

  ...ToolReadContext;

  trace: ToolTrace;
  mode: McpDataMode;

  @doc("Annotations on the trace and its spans, newest first.")
  annotations?: Annotation[];

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Lists the sessions, spans sharing a session id, that started in the window: span, trace and error counts, duration, services, failure ids, agent name, models and token usage. Use it to find an agent or user session; get_agent_run reconstructs one session's steps and list_traces lists single executions. Default: the last 60 minutes, 20 sessions per page, most recent activity first.")
model ListSessionsInput {
  @doc("`true` keeps sessions with an error span, `false` sessions without one.")
  @encodedName("application/json", "has_error")
  hasError?: boolean;
  @doc("Keep sessions with at least this many error spans.")
  @encodedName("application/json", "min_errors")
  minErrors?: int64;
  @doc("Keep sessions with a span of this service; with `matchers`, one span must satisfy both.")
  @encodedName("application/json", "service_name")
  serviceName?: string;
  @doc("Keep sessions whose agent (`gen_ai.agent.name`) has this name.")
  @encodedName("application/json", "agent_name")
  agentName?: string;
  @doc("Keep sessions that used this model (`gen_ai.response.model`, else `gen_ai.request.model`).")
  `model`?: string;
  @doc("Keep sessions with at least this many input plus output tokens.")
  @encodedName("application/json", "min_tokens")
  minTokens?: int64;
  @doc("Keep sessions lasting at least this many milliseconds, from the first span start to the last span end.")
  @encodedName("application/json", "min_duration_ms")
  minDurationMs?: DurationMs;
  @doc("Keep sessions with an annotation carrying this label.")
  @encodedName("application/json", "annotation_label")
  annotationLabel?: string;
  @doc("Order of the sessions: `last_activity` (default) most recent first, `errors` most error spans first, `duration` longest first, `tokens` most tokens first.")
  @encodedName("application/json", "sort_by")
  sortBy?: "last_activity" | "errors" | "duration" | "tokens";

  @doc("Attribute predicates one span of the session must all satisfy, together with `service_name`.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  @doc("Sessions per page, 1 to 100; default 20.")
  @minValue(1)
  @maxValue(100)
  limit?: int32 = 20;

  @doc("Return only active sessions, those whose last span ended less than 5 minutes ago; omitted or false includes ended sessions.")
  @encodedName("application/json", "active_only")
  activeOnly?: boolean;

  ...ToolTimeRange;

  @doc("`next_cursor` of a previous list_sessions call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for list_sessions.")
model ListSessionsOutput {
  ...ToolReadContext;

  sessions: ToolSession[];
  mode: McpDataMode;
  ...ToolPage;

  @doc("Annotations on the listed sessions, newest first; each names its session in `target`.")
  annotations?: Annotation[];

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Searches the window's log records, newest first, by trace, span, session, service, minimum severity, body text or matchers. Use it for the logs around one execution (pass a `trace_id` from list_traces or get_trace), or with `group_by: pattern` for the repeating message templates alone; aggregate_telemetry counts logs per attribute. Default: the last 60 minutes, 50 records per page.")
model SearchLogsInput {
  @doc("Return only log records correlated with this span.")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;
  @doc("`pattern` returns message templates (numbers, hex ids, UUIDs and quoted strings replaced by `#`) with their counts, most frequent first, instead of the records.")
  @encodedName("application/json", "group_by")
  groupBy?: "pattern";
  @doc("Also return up to this many records before and after each returned record from the same service and resource, in `context_logs`; 0 to 10, default 0. Not applied with `group_by`.")
  @minValue(0) @maxValue(10)
  context?: int32;

  @doc("Attribute predicates every returned record must satisfy; a matcher tests log attributes unless its `scope` is `resource`.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  @doc("Return only log records correlated with this trace.")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Return only log records from this service.")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Minimum OpenTelemetry severity number: 9 info, 13 warn, 17 error.")
  @encodedName("application/json", "severity_min")
  @minValue(1)
  @maxValue(24)
  severityMin?: int32;

  @doc("Case-insensitive substring of the log body.")
  query?: string;

  @doc("Records per page, or patterns with `group_by`, 1 to 200; default 50.")
  @minValue(1)
  @maxValue(200)
  limit?: int32 = 50;

  @doc("Return only log records of this session.")
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;

  ...ToolTimeRange;

  @doc("`next_cursor` of a previous search_logs call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for search_logs.")
model SearchLogsOutput {
  @doc("Log records in the window that match the filters. Omitted when the read was capped (`coverage.scan_complete` false), because the total is then unknown.")
  @encodedName("application/json", "total_matched")
  totalMatched?: int64;
  patterns?: LogPattern[];

  ...ToolReadContext;

  @doc("Matching records, newest first; absent when `group_by` asks for patterns.")
  logs?: ToolLogRow[];
  @doc("With `context`, the records next to each returned one from the same service and resource.")
  @encodedName("application/json", "context_logs")
  contextLogs?: ToolLogRow[];
  mode: McpDataMode;
  ...ToolPage;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Outcome of one CI phase span, projected from its OpenTelemetry span status.")
enum CiPhaseStatus {
  ok: "ok",
  error: "error",
  unset: "unset",
}

@doc("Lists the CI runs recorded as telemetry by services whose name starts with `service_prefix`, newest first, and breaks one run into its phases per leg with status, duration, message and error logs. `run_id` picks the run to break down (default: the newest matching run), and `compare_to` compares it with the last successful run or another run, naming new failures, fixed phases and duration regressions. Use get_failure_context with `ci_run_id` to explain a failed run.")
model CiLogInput {
  @doc("`next_cursor` of a previous ci_log call with the same filters; continues the run list.")
  cursor?: Cursor;
  @doc("Keep runs with a span on this branch (`vcs.ref.head.name`).")
  branch?: string;
  @doc("Keep runs with a span of this commit (`vcs.ref.head.revision`).")
  commit?: string;
  @doc("Keep only the phases of this leg (`cicd.pipeline.task.name`, else the service name).")
  leg?: string;
  @doc("Keep only the phases with this span name.")
  phase?: string;
  @doc("`next_phase_cursor` of a previous call for the same run; continues its phases, 50 per page.")
  @encodedName("application/json", "phase_cursor")
  phaseCursor?: Cursor;
  @doc("`failed` keeps runs with an error span, `passed` runs without one, `all` (default) both.")
  outcome?: "failed" | "passed" | "all";
  @doc("Compare the selected run with `last_success`, the latest earlier run without errors, or with the run of this `run_id`.")
  @encodedName("application/json", "compare_to")
  compareTo?: string;

  @doc("Attribute predicates one span of a run in the window must satisfy.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  ...ToolTimeRange;

  @doc("The run to break into phases: a `run_id` of the run list, the run's session id; an unknown run is answered with not_found naming recent runs. Omitted, the newest matching run is broken down.")
  @encodedName("application/json", "run_id")
  runId?: SessionId;

  @doc("Case-sensitive service-name prefix identifying CI telemetry in both run lists and phase breakdowns.")
  @encodedName("application/json", "service_prefix")
  @minLength(1)
  @maxLength(256)
  servicePrefix?: string = "qyl-ci";

  @doc("Runs per page, 1 to 50; default 10.")
  @minValue(1)
  @maxValue(50)
  limit?: int32 = 10;
}

@doc("One CI run, summarized from the session its telemetry is grouped under.")
model CiRunSummary {
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  @encodedName("application/json", "provider_run_id")
  providerRunId?: string;
  @encodedName("application/json", "provider_run_url")
  providerRunUrl?: UrlString;
  attempt?: int32;
  attempts?: CiAttempt[];

  @encodedName("application/json", "run_id")
  runId: SessionId;

  state: SessionState;

  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @encodedName("application/json", "duration_ms")
  durationMs?: DurationMs;

  @doc("Spans in the run that ended with an error status.")
  @encodedName("application/json", "error_count")
  @minValue(0)
  errorCount: int32;

  @doc("Service names observed in the run, at least one of which is CI-emitted.")
  services: string[];
}

@doc("One phase span of a CI run, attributed to the leg that executed it.")
model CiPhase {
  @encodedName("application/json", "span_id")
  spanId?: SpanId;
  @encodedName("application/json", "trace_ids")
  traceIds?: TraceId[];
  @encodedName("application/json", "error_log_count")
  errorLogCount?: int64;
  @encodedName("application/json", "error_logs")
  errorLogs?: CiErrorExcerpt[];

  @doc("Value of the span's ci.leg attribute, falling back to its service name.")
  leg: string;

  @doc("Span name, which the CI emitter convention sets to the phase name.")
  phase: string;

  status: CiPhaseStatus;

  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;

  @doc("Span status message, present on a failed phase.")
  message?: string;
}

@doc("Structured output for ci_log: recent runs, or one run's phases.")
model CiLogOutput {
  @encodedName("application/json", "next_phase_cursor")
  nextPhaseCursor?: Cursor;
  ...ToolPage;
  comparison?: CiComparison;

  ...ToolReadContext;

  runs?: CiRunSummary[];

  @encodedName("application/json", "run_id")
  runId?: SessionId;

  phases?: CiPhase[];
  mode: McpDataMode;

  @doc("Set when the call failed.")
  error?: ToolError;
}

// -----------------------------------------------------------------------------
// Metric tools: list_metrics, get_metric_series, query_metric
// -----------------------------------------------------------------------------

@doc("Lists the recorded metric instruments, one compact row each: name, kind, unit, temporality, reporting services, series count, last seen, the aggregations query_metric accepts and whether its descriptors conflict. Start a metric question here; get_metric_series shows one instrument's series and attribute keys, and query_metric computes its values over time. Default: 50 instruments per page, ordered by name.")
model ListMetricsInput {
  @doc("Case-insensitive substring of the instrument name or description.")
  text?: string;
  @doc("Keep instruments this service reports.")
  @encodedName("application/json", "service_name")
  serviceName?: string;
  @doc("Keep instruments of this kind.")
  kind?: MetricKind;
  @doc("Keep instruments with exactly this unit, e.g. `s` or `By`.")
  unit?: string;

  @doc("Attribute predicates one series of the instrument must satisfy; `scope: resource` tests its resource attributes.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  ...ToolTimeRange;

  @doc("Return only names starting with this prefix (case-sensitive), e.g. `http.server.`.")
  @encodedName("application/json", "name_prefix")
  namePrefix?: string;

  @doc("Instruments per page, 1 to 1000; default 50.")
  @minValue(1)
  @maxValue(1000)
  limit?: int32 = 50;

  @doc("`next_cursor` of a previous list_metrics call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for list_metrics.")
model ListMetricsOutput {
  ...ToolReadContext;

  items: MetricCatalogRow[];
  ...ToolPage;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Lists the series (attribute combinations) of one metric instrument that recorded a point in the window, with each attribute key's cardinality and top values and suggested `group_by` keys; with `series_id`, one series and a page of its original points. Use it to choose query_metric's `group_by` and `matchers`, or for exact original values and exemplars; query_metric aggregates the values over time. Default: the last 60 minutes, 200 series per page.")
model GetMetricSeriesInput {
  ...ToolTimeRange;

  @doc("Select one exact series and include its first page of original points. Omitted, list series metadata only. Original points are never compacted; a point exceeding the MCP response budget returns response_too_large and remains available through the HTTP points API.")
  @encodedName("application/json", "series_id")
  @minLength(1)
  seriesId?: string;

  @doc("Continuation of the selected series' points page; requires series_id.")
  @encodedName("application/json", "points_cursor")
  pointsCursor?: Cursor;

  @doc("Page size for original points from the selected series.")
  @encodedName("application/json", "points_limit")
  @minValue(1)
  @maxValue(1000)
  pointsLimit?: int32 = 100;

  @doc("The instrument name, e.g. from list_metrics.")
  @encodedName("application/json", "metric_name")
  @minLength(1)
  metricName: string;

  @doc("Attribute matchers a series must all satisfy; serialized to `attr` and `attr_prefix`.")
  @maxItems(32)
  matchers?: MetricAttributeMatcher[];

  @doc("Series per page, 1 to 1000; default 200.")
  @minValue(1)
  @maxValue(1000)
  limit?: int32 = 200;

  @doc("`next_cursor` of a previous get_metric_series call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for get_metric_series.")
model GetMetricSeriesOutput {
  @doc("Original points, present only when one exact series_id was selected. Continue with points_cursor.")
  points?: CursorPage<MetricPoint>;

  keys?: MetricKeySummary[];
  @encodedName("application/json", "suggested_group_by")
  suggestedGroupBy?: string[];

  ...ToolReadContext;

  items: MetricSeries[];
  ...ToolPage;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Aggregates one metric instrument over the window: per series, the buckets that hold points and one value for the whole window, split by `group_by`, transformed by `function`, compared with an earlier window by `compare_to` or divided by another metric by `ratio_of`. Metric values are pre-aggregated in the instrument's unit, often seconds, and histogram percentiles are interpolated; aggregate_telemetry gives exact per-span durations in ms. Without `step_ms` the step is the whole seconds that split the window into at most 60 buckets; the default window is the last 60 minutes.")
model QueryMetricInput {
  @doc("Minutes before `end_time` (or now) when `start_time` is omitted; default 60.")
  @encodedName("application/json", "lookback_minutes")
  lookbackMinutes?: int32;
  @doc("Transform of each series' points before the buckets aggregate them: `none` (default) the recorded values, `delta` the change from the previous point, `increase` the changes summed per bucket, `rate` the change per second of the interval each point recorded. A histogram's points already are the changes of their intervals, so a histogram takes a function only with aggregation `count` or `sum`, and its rate is what each series recorded over the time it recorded it, summed across series. A count is in unit `1`; a rate appends `/s`.")
  function?: "none" | "rate" | "increase" | "delta";
  @doc("Also aggregate an earlier window into `comparison`: `previous_window`, the window of equal length just before, or `{offset_ms}`, the window shifted back by that many milliseconds.")
  @encodedName("application/json", "compare_to")
  compareTo?: MetricComparison;
  @doc("Divide each series, per bucket and for the whole window, by the series with the same `group_by` values of another metric; the result's unit is `1`.")
  @encodedName("application/json", "ratio_of")
  ratioOf?: MetricRatio;

  @doc("The instrument to query, e.g. from list_metrics.")
  @encodedName("application/json", "metric_name")
  @minLength(1)
  metricName: string;

  @doc("Start of the window (inclusive), RFC 3339; takes precedence over `lookback_minutes`.")
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;

  @doc("End of the window (exclusive), RFC 3339; default now.")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Bucket width in integer milliseconds; the window may hold at most 10000 buckets. Omit for whole seconds that split the window into at most 60 buckets.")
  @encodedName("application/json", "step_ms")
  @minValue(1)
  @maxValue(86400000)
  stepMs?: int64;

  @doc("Reducer of the points in each bucket and in the whole window: `avg` (default), `min`, `max`, `sum`, `count`, `last`, or `p50`, `p90`, `p95` and `p99` for histograms. list_metrics shows each instrument's `supported_aggregations`.")
  aggregation?: MetricAggregation = MetricAggregation.avg;

  @doc("Select one exact metric series; disambiguates scopes and conflicting instrument descriptors.")
  @encodedName("application/json", "series_id")
  @minLength(1)
  seriesId?: string;

  @doc("Attribute keys to split the result by: one series per distinct combination of their values.")
  @encodedName("application/json", "group_by")
  @maxItems(16)
  groupBy?: string[];

  @doc("Attribute matchers a series must all satisfy; serialized to `attr` and `attr_prefix`.")
  @maxItems(32)
  matchers?: MetricAttributeMatcher[];

  @doc("Maximum result series, 1 to 500; default 50. `truncated` reports that more existed.")
  @encodedName("application/json", "series_limit")
  @minValue(1)
  @maxValue(500)
  seriesLimit?: int32 = 50;
}

@doc("Structured output for query_metric: the collector's range-query result.")
model QueryMetricOutput {
  method?: string;
  @encodedName("application/json", "exemplar_trace_ids")
  exemplarTraceIds?: TraceId[];
  comparison?: MetricComparisonResult;

  ...ToolReadContext;

  ...MetricQueryResult;

  @doc("Set when the call failed.")
  error?: ToolError;
}

// -----------------------------------------------------------------------------
// Write tools (scope qyl:write; delete needs qyl:delete)
// -----------------------------------------------------------------------------

@doc("Attaches a note to a trace, span, session or failure in the project; the telemetry is not changed. get_trace and list_sessions return the notes on traces, spans and sessions, list_failures counts the notes on a failure, and `annotation_label` filters list_traces and list_sessions by label. Needs the qyl:write scope; update_annotation changes a note with its etag.")
model CreateAnnotationInput {
  @doc("Retry key, 1 to 255 characters. Omitted, an identical create within 24 hours returns the first annotation; send a new key to create another identical one. A key reused with a different body is refused.")
  @minLength(1) @maxLength(255)
  @encodedName("application/json", "idempotency_key")
  idempotencyKey?: string;

  ...AnnotationCreateRequest;
}

@doc("Changes an annotation's text, labels or classification; its target stays fixed and omitted fields keep their value. Pass its current `etag`, from create_annotation or the annotation as get_trace or list_sessions returned it; a stale etag is refused, so re-read and retry. Needs the qyl:write scope.")
model UpdateAnnotationInput {
  @doc("The annotation's id, from create_annotation or a returned annotation.")
  id: ResourceId;

  @doc("The annotation's current `etag`, sent as If-Match.")
  etag: ETag;

  ...AnnotationUpdateRequest;
}

@doc("Structured output for create_annotation and update_annotation.")
model AnnotationOutput {
  annotation?: Annotation;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Saves a named filter over traces, logs or metrics so a question can be asked again; it stores the filter, never results. run_saved_query evaluates it against current telemetry with a window ending at the time of the run, and list_saved_queries lists the saved queries. Names are unique per project; needs the qyl:write scope.")
model CreateSavedQueryInput {
  @doc("Retry key, 1 to 255 characters. Omitted, an identical create within 24 hours returns the first saved query. A key reused with a different body is refused.")
  @minLength(1) @maxLength(255)
  @encodedName("application/json", "idempotency_key")
  idempotencyKey?: string;

  ...SavedQueryCreateRequest;
}

@doc("Changes a saved query's name, description or filter; omitted fields keep their value and a new `filter` replaces the old one whole. Pass its current `etag` from list_saved_queries or create_saved_query; a stale etag is refused, so re-read and retry. Needs the qyl:write scope; names stay unique per project.")
model UpdateSavedQueryInput {
  @doc("The saved query's id, from list_saved_queries or create_saved_query.")
  id: ResourceId;

  @doc("The saved query's current `etag`, sent as If-Match.")
  etag: ETag;

  ...SavedQueryUpdateRequest;
}

@doc("Structured output for create_saved_query and update_saved_query.")
model SavedQueryOutput {
  @encodedName("application/json", "saved_query")
  savedQuery?: SavedQuery;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Lists the project's saved queries ordered by name, each with its id, current etag and filter. Use it to choose the id for run_saved_query or the etag for update_saved_query and delete_record; the time range and `matchers` do not filter this list. Default: 20 saved queries per page.")
model ListSavedQueriesInput {
  @doc("Not applied: the list is not filtered by attributes.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];

  ...ToolTimeRange;

  @doc("Keep only saved queries of this signal.")
  signal?: SavedQuerySignal;

  @doc("Saved queries per page, 1 to 100; default 20.")
  @minValue(1)
  @maxValue(100)
  limit?: int32 = 20;

  @doc("`next_cursor` of a previous list_saved_queries call with the same filters.")
  cursor?: Cursor;
}

@doc("Structured output for list_saved_queries.")
model ListSavedQueriesOutput {
  ...ToolReadContext;

  @encodedName("application/json", "saved_queries")
  savedQueries: SavedQuery[];

  ...ToolPage;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("Runs a saved query against current telemetry: a traces query answers like list_traces, a logs query like search_logs and a metrics query like query_metric. The window is the saved lookback ending now, 60 minutes when none was saved, unless `lookback_minutes`, `start_time` or `end_time` override it. An unknown id is answered with not_found; list_saved_queries lists the ids.")
model RunSavedQueryInput {
  @doc("Minutes before `end_time` (or now) for this run, overriding the saved look-back window.")
  @encodedName("application/json", "lookback_minutes")
  lookbackMinutes?: int32;

  @doc("The saved query to run, from list_saved_queries or create_saved_query.")
  id: ResourceId;
  ...SavedQueryRunRequest;
}

@doc("Structured output for run_saved_query.")
model RunSavedQueryOutput {
  ...ToolReadContext;

  result?: SavedQueryRunResult;
  mode?: McpDataMode;

  @doc("Set when the call failed.")
  error?: ToolError;
}

@doc("The user-created resource kinds the delete tool accepts.")
enum DeletableKind {
  annotation: "annotation",
  savedQuery: "saved_query",
}

@doc("Deletes one annotation or saved query, named by kind, id and current etag; traces, logs and metrics are never deletable. Needs the qyl:delete scope; a stale etag is refused, so re-read the record for its current etag and retry.")
model DeleteInput {
  @doc("What `id` names: `annotation` or `saved_query`.")
  kind: DeletableKind;
  @doc("The id of the annotation or saved query.")
  id: ResourceId;

  @doc("The resource's current `etag`, sent as If-Match.")
  etag: ETag;
}

@doc("Structured output for the delete tool.")
model DeleteOutput {
  deleted: boolean;

  @doc("Set when the call failed.")
  error?: ToolError;
}
