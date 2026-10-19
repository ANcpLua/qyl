import "./mcp-tools.tsp";
import "../common/telemetry-query.tsp";
using TypeSpec.OpenAPI;
using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Annotations;
using Qyl.Api.Contracts.OTel.Traces;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
namespace Qyl.Api.Contracts.Mcp.Tools;

@doc("Persisted failure triage status.")
enum FailureStatus { open, resolved, ignored }

@doc("Filter for failure status.")
union FailureStatusFilter { status: FailureStatus, all: "all" }

@doc("Failure trend in the selected comparison window.")
enum FailureChange { new, worsening, ongoing, recovering, regressed }

@doc("Comparison window for change detection. Omit for `previous_window`: the window of the same length immediately before the selected one. A `window` object sets explicit `start_time` and `end_time`; an `offset_minutes` object shifts the selected window back by that many minutes.")
union AnalysisBaseline { previous: "previous_window", window: ToolWindow, offset: BaselineOffset }

@doc("MCP comparison window or deployed server version.")
union McpStatsComparison { windowOrVersion: string, offset: McpComparisonOffset, version: McpComparisonVersion }

@doc("Metric comparison preceding the selected window.")
union MetricComparison { previous: "previous_window", offset: MetricComparisonOffset }

@doc("AnalysisInput contract.")
model AnalysisInput {
  ...ToolTimeRange;
  @doc("Attribute predicates that every matched span, resource or log must satisfy; up to 32. Omit for no filtering. discover_attributes lists the keys and values that are recorded.")
  @maxItems(32)
  matchers?: TelemetryMatcher[];
  @doc("Items per page of every collection the tool returns; aggregate_telemetry and triage default lower. `has_more` and `next_cursor` continue the same query.")
  @minValue(1) @maxValue(200)
  limit?: int32 = 100;
  cursor?: Cursor;
}

@doc("ServiceAnalysisInput contract.")
model ServiceAnalysisInput {
  ...AnalysisInput;
  @encodedName("application/json", "service_name")
  serviceName?: string;
  environment?: string;
}

@doc("BaselineOffset contract.")
model BaselineOffset {
  @encodedName("application/json", "offset_minutes")
  offsetMinutes: int32;
}

@doc("FailureRelease contract.")
model FailureRelease {
  service: string;
  version: string;
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  revision?: string;
}

@doc("FailureOccurrence contract.")
model FailureOccurrence {
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  @encodedName("application/json", "span_id")
  spanId: SpanId;
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  timestamp: utcDateTime;
  message?: string;
  release?: string;
}

@doc("FailureReleaseCount contract.")
model FailureReleaseCount {
  release: string;
  count: int64;
}

@doc("AttributeAssociation contract.")
model AttributeAssociation {
  key: string;
  value: MatcherValue;
  @encodedName("application/json", "share_a")
  shareA: float64;
  @encodedName("application/json", "share_b")
  shareB: float64;
  @encodedName("application/json", "count_a")
  countA: int64;
  @encodedName("application/json", "count_b")
  countB: int64;
  lift: float64 | null;
}

@doc("FailureSummary contract.")
model FailureSummary {
  @encodedName("application/json", "failure_id")
  failureId: FailureId;
  etag: ETag;
  signature: string;
  @encodedName("application/json", "exception_type")
  exceptionType?: string;
  @encodedName("application/json", "normalized_message")
  normalizedMessage: string;
  operation: string;
  service: string;
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
  count: int64;
  @encodedName("application/json", "affected_traces")
  affectedTraces: int64;
  @encodedName("application/json", "affected_sessions")
  affectedSessions: int64;
  @encodedName("application/json", "releases_seen")
  releasesSeen: string[];
  status: FailureStatus;
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
  @encodedName("application/json", "ignore_until")
  ignoreUntil?: utcDateTime;
  assignee?: string;
  @encodedName("application/json", "annotation_count")
  annotationCount: int64;
  @maxItems(3)
  @encodedName("application/json", "example_trace_ids")
  exampleTraceIds: TraceId[];
}

@doc("FailureDetail contract.")
model FailureDetail {
  ...FailureSummary;
  @encodedName("application/json", "counts_per_release")
  countsPerRelease?: FailureReleaseCount[];
  discriminators?: AttributeAssociation[];
  @encodedName("application/json", "recent_occurrences")
  recentOccurrences?: FailureOccurrence[];
  @encodedName("application/json", "raw_messages")
  rawMessages?: string[];
}

@doc("TriageInput contract.")
model TriageInput {
  ...OmitProperties<ServiceAnalysisInput, "limit">;
  @doc("Ranked failures to return, 1 to 50; new failures keep reserved slots ahead of ongoing ones.")
  @minValue(1) @maxValue(50)
  limit?: int32 = 10;
  baseline?: AnalysisBaseline;
}

@doc("TriageFailure contract.")
model TriageFailure {
  @encodedName("application/json", "failure_id")
  failureId: FailureId;
  signature: string;
  change: FailureChange;
  count: int64;
  @encodedName("application/json", "baseline_count")
  baselineCount: int64;
  @encodedName("application/json", "affected_traces")
  affectedTraces: int64;
  services: string[];
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  @encodedName("application/json", "example_trace_id")
  exampleTraceId?: TraceId;
  @doc("The release the failing spans run on (their most common `service.version`), when it was first seen in the window.")
  @encodedName("application/json", "coinciding_release")
  coincidingRelease?: FailureRelease;
  status: FailureStatus;
  @doc("Current state version; pass it to update_failure_status.")
  etag: ETag;
  @doc("The failure this one propagated from: in most of its traces, the failure of the earliest error span without a failing child. Equal to `failure_id` when the failure is its own cause.")
  @encodedName("application/json", "root_cause_failure_id")
  rootCauseFailureId?: FailureId;
}

@doc("TriageRegression contract.")
model TriageRegression {
  service: string;
  operation: string;
  measure: "error_rate" | "p95_ms" | "throughput";
  value: float64;
  @encodedName("application/json", "baseline_value")
  baselineValue: float64;
  denominator: int64;
  unit: string;
}

@doc("A service whose last span in the baseline and the window is older than the window's end minus 15 minutes or five of its usual intervals between spans, whichever is longer.")
model SilentService {
  service: string;
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
  @encodedName("application/json", "usual_rate")
  usualRate: float64 | null;
  unit: string;
}

@doc("TriageOutput contract.")
model TriageOutput {
  ...ToolReadContext;
  ...ToolPage;
  failures: TriageFailure[];
  regressions: TriageRegression[];
  releases: FailureRelease[];
  @encodedName("application/json", "silent_services")
  silentServices: SilentService[];
  error?: ToolError;
}

@doc("ListFailuresInput contract.")
model ListFailuresInput {
  ...ServiceAnalysisInput;
  release?: string;
  status?: FailureStatusFilter;
  @encodedName("application/json", "sort_by")
  sortBy?: "count" | "last_seen" | "first_seen" | "affected_traces";
  @encodedName("application/json", "failure_id")
  failureId?: FailureId;
}

@doc("ListFailuresOutput contract.")
model ListFailuresOutput {
  ...ToolReadContext;
  ...ToolPage;
  failures: FailureDetail[];
  error?: ToolError;
}

@doc("Input for get_failure_context. Give exactly one of trace_id, session_id, failure_id or ci_run_id. A request with none or several of them is rejected.")
model GetFailureContextInput {
  ...AnalysisInput;
  @doc("The failing trace, 32 lowercase hex characters. Give exactly one of trace_id, session_id, failure_id or ci_run_id.")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;
  @doc("A session whose traces hold the failure. Give exactly one of trace_id, session_id, failure_id or ci_run_id.")
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  @doc("A failure_id from triage or list_failures; the trace of its latest occurrence is examined. Give exactly one of trace_id, session_id, failure_id or ci_run_id.")
  @encodedName("application/json", "failure_id")
  failureId?: FailureId;
  @doc("A CI run: its session id or its provider run id. Give exactly one of trace_id, session_id, failure_id or ci_run_id.")
  @encodedName("application/json", "ci_run_id")
  ciRunId?: SessionId;
  @doc("Maximum error spans in `observed.error_spans`, or `limit` when lower; `next_cursor` continues.")
  @minValue(1) @maxValue(1000)
  @encodedName("application/json", "max_spans")
  maxSpans?: int32 = 100;
  @doc("Maximum correlated log records in `observed.logs`, or `limit` when lower; `next_cursor` continues.")
  @minValue(1) @maxValue(200)
  @encodedName("application/json", "max_logs")
  maxLogs?: int32 = 100;
}

@doc("EvidenceSpan contract.")
model EvidenceSpan {
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  @encodedName("application/json", "span_id")
  spanId: SpanId;
  @encodedName("application/json", "parent_span_id")
  parentSpanId?: SpanId;
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  service: string;
  name: string;
  status: string;
  @encodedName("application/json", "status_message")
  statusMessage?: string;
  exception?: RecordedException;
  @encodedName("application/json", "key_attributes")
  keyAttributes: Attribute[];
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  role: "matched" | "context";
}

@doc("RecordedException contract.")
model RecordedException {
  type?: string;
  message?: string;
  stacktrace?: string;
}

@doc("FailurePropagation contract.")
model FailurePropagation {
  from: EvidenceSpan;
  to: EvidenceSpan;
}

@doc("RetryEvidence contract.")
model RetryEvidence {
  name: string;
  attempts: int32;
  spans: EvidenceSpan[];
}

@doc("FanoutEvidence contract.")
model FanoutEvidence {
  name: string;
  count: int32;
  @encodedName("application/json", "parent_span_id")
  parentSpanId?: SpanId;
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
}

@doc("FailureObserved contract.")
model FailureObserved {
  @encodedName("application/json", "root_cause_span")
  rootCauseSpan: EvidenceSpan | null;
  @encodedName("application/json", "error_spans")
  errorSpans: EvidenceSpan[];
  propagation: FailurePropagation[];
  logs: LogRecord[];
  retries: RetryEvidence[];
  fanout: FanoutEvidence[];
}

@doc("TraceDivergence contract.")
model TraceDivergence {
  @encodedName("application/json", "a_span")
  aSpan: EvidenceSpan | null;
  @encodedName("application/json", "b_span")
  bSpan: EvidenceSpan | null;
  kind: "missing" | "extra" | "status" | "attribute" | "duration";
  attribute?: string;
}

@doc("FailureComparison contract.")
model FailureComparison {
  @encodedName("application/json", "nearest_success_trace_id")
  nearestSuccessTraceId?: TraceId;
  @encodedName("application/json", "first_divergence")
  firstDivergence: TraceDivergence | null;
}

@doc("SimilarFailure contract.")
model SimilarFailure {
  @encodedName("application/json", "failure_id")
  failureId: FailureId;
  count: int64;
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  releases: string[];
}

@doc("FailureContext contract.")
model FailureContext {
  @encodedName("application/json", "similar_failures")
  similarFailures: SimilarFailure[];
  changes: TriageRegression[];
  annotations: Annotation[];
}

@doc("GetFailureContextOutput contract.")
model GetFailureContextOutput {
  ...ToolReadContext;
  ...ToolPage;
  observed: FailureObserved;
  comparison: FailureComparison;
  context: FailureContext;
  settled: boolean;
  unknowns: string[];
  error?: ToolError;
}

@doc("CompareGroup contract.")
model CompareGroup {
  @maxItems(32)
  matchers?: TelemetryMatcher[];
  window?: ToolWindow;
  @encodedName("application/json", "failure_id")
  failureId?: FailureId;
}

@doc("CompareGroupsInput contract.")
model CompareGroupsInput {
  ...AnalysisInput;
  signal: "spans" | "logs";
  @encodedName("application/json", "group_a")
  groupA: CompareGroup;
  @encodedName("application/json", "group_b")
  groupB: CompareGroup | "rest" | "before";
  @encodedName("application/json", "attribute_scope")
  attributeScope?: "span" | "resource" | "both";
}

@doc("GroupStructure contract.")
model GroupStructure {
  name: string;
  @encodedName("application/json", "share_a")
  shareA: float64;
  @encodedName("application/json", "share_b")
  shareB: float64;
}

@doc("DurationPercentiles contract.")
model DurationPercentiles {
  @encodedName("application/json", "p50_ms")
  p50Ms: float64 | null;
  @encodedName("application/json", "p95_ms")
  p95Ms: float64 | null;
  @encodedName("application/json", "p99_ms")
  p99Ms: float64 | null;
}

@doc("GroupDurations contract.")
model GroupDurations {
  a: DurationPercentiles;
  b: DurationPercentiles;
}

@doc("GroupComposition contract.")
model GroupComposition {
  changed: boolean;
  @encodedName("application/json", "traffic_mix_changed")
  trafficMixChanged: boolean;
  @encodedName("application/json", "same_traffic_changed")
  sameTrafficChanged: boolean;
  summary: string;
}

@doc("CompareGroupsOutput contract.")
model CompareGroupsOutput {
  ...ToolReadContext;
  ...ToolPage;
  discriminators: AttributeAssociation[];
  structural: GroupStructure[];
  durations: GroupDurations;
  composition: GroupComposition;
  @encodedName("application/json", "size_a")
  sizeA: int64;
  @encodedName("application/json", "size_b")
  sizeB: int64;
  @encodedName("application/json", "excluded_keys")
  excludedKeys: string[];
  error?: ToolError;
}

@doc("ExecutionReference contract.")
model ExecutionReference {
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
}

@doc("CompareTracesInput contract.")
model CompareTracesInput {
  ...AnalysisInput;
  a: ExecutionReference;
  b: ExecutionReference;
}

@doc("AlignedSpans contract.")
model AlignedSpans {
  @encodedName("application/json", "a_span")
  aSpan: EvidenceSpan;
  @encodedName("application/json", "b_span")
  bSpan: EvidenceSpan;
  differences: TraceDivergence[];
}

@doc("CompareTracesOutput contract.")
model CompareTracesOutput {
  ...ToolReadContext;
  ...ToolPage;
  @encodedName("application/json", "first_divergence")
  firstDivergence: TraceDivergence | null;
  aligned: AlignedSpans[];
  @encodedName("application/json", "after_divergence")
  afterDivergence: TraceDivergence[];
  unmatched: EvidenceSpan[];
  comparable: boolean;
  reason?: string;
  error?: ToolError;
}

@doc("VerifyFixInput contract.")
model VerifyFixInput {
  ...AnalysisInput;
  @encodedName("application/json", "failure_id")
  failureId?: FailureId;
  since?: utcDateTime;
  release?: string;
  @encodedName("application/json", "min_requests")
  minRequests?: int32;
  @encodedName("application/json", "min_minutes")
  minMinutes?: int32;
  @encodedName("application/json", "verify_id")
  verifyId?: VerifyId;
}

@doc("One side of a verification. `before` runs from the failure's first retained occurrence, or the request's lookback when matchers select the requests, up to the change; `after` runs from the change to now.")
model VerificationPeriod {
  window: ToolWindow;
  occurrences: int64;
  requests: int64;
  rate: float64 | null;
  unit: "requests";
}

@doc("What a pending verification still needs before it can pass.")
model VerificationNeeds {
  @doc("Requests still to observe after the change.")
  requests: int64;
  @doc("Whole minutes still to wait, rounded up.")
  minutes: int64;
}

@doc("VerifyFixOutput contract.")
model VerifyFixOutput {
  ...ToolReadContext;
  ...ToolPage;
  verdict: "passed" | "failed" | "pending" | "inconclusive";
  before: VerificationPeriod;
  after: VerificationPeriod;
  needs?: VerificationNeeds;
  @maxItems(3)
  recurrences: TraceId[];
  guardrails: TriageFailure[];
  @encodedName("application/json", "verify_id")
  verifyId: VerifyId;
  error?: ToolError;
}

@doc("AggregateTelemetryInput contract.")
model AggregateTelemetryInput {
  ...OmitProperties<AnalysisInput, "limit">;
  @doc("Groups per page, largest first by `order_by`; `other` sums the groups after this page.")
  @minValue(1) @maxValue(200)
  limit?: int32 = 20;
  @doc("Which records to aggregate: `spans` (duration measures apply) or `logs`.")
  signal: "spans" | "logs";
  @doc("Attribute keys that define the groups, up to 4; omit for one overall group. Each group's `key` echoes the values.")
  @maxItems(4)
  @encodedName("application/json", "group_by")
  groupBy?: string[];
  @doc("Measures to compute per group; at least one. Accepted values: `count`, `error_count`, `error_rate` (share from 0 to 1), `duration_avg`, `duration_p50`, `duration_p90`, `duration_p95`, `duration_p99`, `duration_max` (milliseconds, spans only) and `sum:<attribute key>` for a numeric attribute. The result's `units` names the unit of each measure. Example: [\"count\", \"error_rate\", \"duration_p95\"].")
  measures: string[];
  @doc("Bucket width in milliseconds for a per-group time series in `buckets`; omit for group totals only. Equal to the window length, it yields one bucket per group.")
  @encodedName("application/json", "step_ms")
  stepMs?: int64;
  @doc("One of the requested measures, optionally followed by ` asc` or ` desc`. Default: the first measure, descending. Examples: `error_rate`, `duration_p95 desc`, `count asc`.")
  @encodedName("application/json", "order_by")
  orderBy?: string;
}

@doc("AggregateBucket contract.")
model AggregateBucket {
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;
  measures: Record<float64 | null>;
}

@doc("AggregateGroup contract.")
model AggregateGroup {
  key: Record<MatcherValue>;
  measures: Record<float64 | null>;
  buckets?: AggregateBucket[];
}

@doc("AggregateOther contract.")
model AggregateOther {
  groups: int64;
  count: int64;
}

@doc("AggregateTelemetryOutput contract.")
model AggregateTelemetryOutput {
  ...ToolReadContext;
  ...ToolPage;
  groups: AggregateGroup[];
  other: AggregateOther;
  units: Record<string>;
  error?: ToolError;
}

@doc("DiscoverAttributesInput contract.")
model DiscoverAttributesInput {
  ...AnalysisInput;
  signal: "spans" | "logs" | "resources";
  @encodedName("application/json", "key_prefix")
  keyPrefix?: string;
  key?: string;
}

@doc("AttributeValueCount contract.")
model AttributeValueCount {
  @doc("The recorded value in the lossless attribute form: an int64 is a tagged string that keeps every digit.")
  value: AttributeValue;
  count: int64;
  share: float64;
}

@doc("AttributeSemconv contract.")
model AttributeSemconv {
  brief: string;
  stability: string;
  @encodedName("application/json", "requirement_level")
  requirementLevel: string;
}

@doc("DiscoveredAttribute contract.")
model DiscoveredAttribute {
  key: string;
  scope: MatcherScope;
  type: string;
  @encodedName("application/json", "coverage_share")
  coverageShare: float64;
  cardinality: int64;
  @doc("False when the read was capped (`coverage.scan_complete` false): `cardinality` then counts only the values of the records the read loaded.")
  @encodedName("application/json", "cardinality_exact")
  cardinalityExact: boolean;
  @maxItems(10)
  @encodedName("application/json", "top_values")
  topValues: AttributeValueCount[];
  semconv: AttributeSemconv | null;
}

@doc("DiscoverAttributesOutput contract.")
model DiscoverAttributesOutput {
  ...ToolReadContext;
  ...ToolPage;
  attributes: DiscoveredAttribute[];
  error?: ToolError;
}

@doc("GetAgentRunInput contract.")
model GetAgentRunInput {
  ...AnalysisInput;
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;
  @doc("Maximum steps in `steps`, or `limit` when lower; `next_cursor` continues.")
  @minValue(1) @maxValue(1000)
  @encodedName("application/json", "max_steps")
  maxSteps?: int32 = 100;
}

@doc("AgentTokens contract.")
model AgentTokens {
  input: int64 | null;
  output: int64 | null;
  @encodedName("application/json", "cache_read")
  cacheRead: int64 | null;
  @encodedName("application/json", "cache_write")
  cacheWrite: int64 | null;
  reasoning: int64 | null;
}

@doc("AgentRun contract.")
model AgentRun {
  @encodedName("application/json", "agent_name")
  agentName?: string;
  models: string[];
  started: utcDateTime;
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  tokens: AgentTokens;
  errors: int64;
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;
}

@doc("AgentStep contract.")
model AgentStep {
  index: int32;
  kind: "llm_call" | "tool_call" | "agent" | "retrieval";
  name: string;
  @encodedName("application/json", "mcp_method")
  mcpMethod?: string;
  @encodedName("application/json", "mcp_server")
  mcpServer?: string;
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  tokens: AgentTokens;
  status: string;
  error?: string;
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  @encodedName("application/json", "span_id")
  spanId: SpanId;
}

@doc("RepeatedCall contract.")
model RepeatedCall {
  tool: string;
  @encodedName("application/json", "argument_hash")
  argumentHash: string;
  count: int64;
}

@doc("AgentTokenShare contract.")
model AgentTokenShare {
  name: string;
  tokens: int64;
  share: float64;
}

@doc("AgentFindings contract.")
model AgentFindings {
  @encodedName("application/json", "repeated_calls")
  repeatedCalls: RepeatedCall[];
  @encodedName("application/json", "failed_tool_calls")
  failedToolCalls: AgentStep[];
  @encodedName("application/json", "slowest_steps")
  slowestSteps: AgentStep[];
  @encodedName("application/json", "token_share")
  tokenShare: AgentTokenShare[];
}

@doc("GetAgentRunOutput contract.")
model GetAgentRunOutput {
  ...ToolReadContext;
  ...ToolPage;
  run: AgentRun;
  steps: AgentStep[];
  findings: AgentFindings;
  unknowns: string[];
  error?: ToolError;
}

@doc("ListReleasesInput contract.")
model ListReleasesInput {
  ...ServiceAnalysisInput;
}

@doc("ReleasePerformance contract.")
model ReleasePerformance {
  @encodedName("application/json", "error_rate")
  errorRate: float64 | null;
  @encodedName("application/json", "p95_ms")
  p95Ms: float64 | null;
}

@doc("ReleaseSummary contract.")
model ReleaseSummary {
  service: string;
  environment?: string;
  version: string;
  revision?: string;
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
  current: boolean;
  concurrent: boolean;
  @encodedName("application/json", "traffic_share")
  trafficShare: float64;
  before: ReleasePerformance;
  after: ReleasePerformance;
  @encodedName("application/json", "new_failure_ids")
  newFailureIds: FailureId[];
}

@doc("ListReleasesOutput contract.")
model ListReleasesOutput {
  ...ToolReadContext;
  ...ToolPage;
  releases: ReleaseSummary[];
  error?: ToolError;
}

@doc("GetServiceMapInput contract.")
model GetServiceMapInput {
  ...ServiceAnalysisInput;
  @minValue(1) @maxValue(3)
  depth?: int32;
  @encodedName("application/json", "include_external")
  includeExternal?: boolean;
}

@doc("ServiceMapNode contract.")
model ServiceMapNode {
  name: string;
  kind: "service" | "database" | "external" | "messaging";
  calls: int64;
  @encodedName("application/json", "error_rate")
  errorRate: float64;
  @encodedName("application/json", "p95_ms")
  p95Ms: float64 | null;
  unit: "spans";
}

@doc("ServiceMapEdge contract.")
model ServiceMapEdge {
  from: string;
  to: string;
  calls: int64;
  @encodedName("application/json", "error_rate")
  errorRate: float64;
  @encodedName("application/json", "p95_ms")
  p95Ms: float64 | null;
  @encodedName("application/json", "example_operations")
  exampleOperations: string[];
  unit: "spans";
}

@doc("GetServiceMapOutput contract.")
model GetServiceMapOutput {
  ...ToolReadContext;
  ...ToolPage;
  nodes: ServiceMapNode[];
  edges: ServiceMapEdge[];
  @encodedName("application/json", "unknown_callers")
  unknownCallers: float64;
  error?: ToolError;
}

@doc("CheckTelemetryHealthInput contract.")
model CheckTelemetryHealthInput {
  ...ServiceAnalysisInput;
}

@doc("Spans whose parent span qyl does not retain. A span whose producer marked its parent remote (OTLP span flags) is not counted: its caller runs in another process.")
model OrphanSpans {
  count: int64;
  share: float64;
  services: string[];
  unit: "spans";
}

@doc("SemconvGap contract.")
model SemconvGap {
  service: string;
  operation: string;
  attribute: string;
  @encodedName("application/json", "requirement_level")
  requirementLevel: string;
  @encodedName("application/json", "missing_share")
  missingShare: float64;
}

@doc("TelemetryRetention contract.")
model TelemetryRetention {
  oldest: utcDateTime | null;
  days: float64 | null;
}

@doc("The conflicting points of one metric series.")
model MetricConflictSeries {
  @encodedName("application/json", "metric_name")
  metricName: string;
  @encodedName("application/json", "series_id")
  seriesId: string;
  service: string;
  @doc("Timestamps of this series in the window that hold different measurements.")
  points: int64;
  @encodedName("application/json", "first_time")
  firstTime: utcDateTime;
  @encodedName("application/json", "last_time")
  lastTime: utcDateTime;
}

@doc("Metric points at which a producer recorded different measurements for one series timestamp. Each point keeps the measurement recorded first, get_metric_series with the series_id returns every observation, and query_metric refuses a window that holds one.")
model MetricConflicts {
  points: int64;
  unit: "points";
  series: MetricConflictSeries[];
}

@doc("CheckTelemetryHealthOutput contract.")
model CheckTelemetryHealthOutput {
  ...ToolReadContext;
  ...ToolPage;
  @encodedName("application/json", "silent_services")
  silentServices: SilentService[];
  @encodedName("application/json", "orphan_spans")
  orphanSpans: OrphanSpans;
  @encodedName("application/json", "metric_conflicts")
  metricConflicts: MetricConflicts;
  @encodedName("application/json", "semconv_gaps")
  semconvGaps: SemconvGap[];
  @encodedName("application/json", "unknown_service_share")
  unknownServiceShare: float64;
  @encodedName("application/json", "schema_urls")
  schemaUrls: string[];
  retention: TelemetryRetention;
  @encodedName("application/json", "ingestion_lag_ms")
  ingestionLagMs?: float64;
  error?: ToolError;
}

@doc("McpComparisonOffset contract.")
model McpComparisonOffset {
  @encodedName("application/json", "offset_minutes")
  offsetMinutes: int32;
}

@doc("McpComparisonVersion contract.")
model McpComparisonVersion {
  @encodedName("application/json", "server_version")
  serverVersion: string;
}

@doc("GetMcpStatsInput contract.")
model GetMcpStatsInput {
  ...AnalysisInput;
  @encodedName("application/json", "server_name")
  serverName?: string;
  @encodedName("application/json", "server_version")
  serverVersion?: string;
  @encodedName("application/json", "tool_name")
  toolName?: string;
  transport?: string;
  @encodedName("application/json", "compare_to")
  compareTo?: McpStatsComparison;
  @encodedName("application/json", "group_by")
  groupBy?: "server" | "tool" | "method" | "transport" | "failure_class";
}

@doc("McpTrafficTotals contract.")
model McpTrafficTotals {
  requests: int64;
  errors: int64;
  @encodedName("application/json", "error_rate")
  errorRate: float64;
  @encodedName("application/json", "p50_ms")
  p50Ms: float64 | null;
  @encodedName("application/json", "p95_ms")
  p95Ms: float64 | null;
  unit: "requests";
}

@doc("McpTrafficGroup contract.")
model McpTrafficGroup {
  ...McpTrafficTotals;
  name: string;
  @maxItems(3)
  @encodedName("application/json", "example_trace_ids")
  exampleTraceIds: TraceId[];
}

@doc("McpFailureClasses contract.")
model McpFailureClasses {
  transport: int64;
  protocol: int64;
  @encodedName("application/json", "tool_result")
  toolResult: int64;
  authentication: int64;
  cancellation: int64;
  timeout: int64;
}

@doc("McpStatsComparisonResult contract.")
model McpStatsComparisonResult {
  window: ToolWindow;
  totals: McpTrafficTotals;
  groups: McpTrafficGroup[];
  @encodedName("application/json", "failure_classes")
  failureClasses: McpFailureClasses;
}

@doc("Requests and errors of one bucket of the window.")
model McpTrafficBucket {
  @doc("Start of the bucket (inclusive); it ends `bucket_ms` later, or at the window's end.")
  start: utcDateTime;

  @minValue(0)
  requests: int32;

  @minValue(0)
  errors: int32;
}

@doc("Requests counted under one name of a breakdown.")
model McpNameRequestCount {
  name: string;

  @minValue(0)
  requests: int32;
}

@doc("Requests, errors and latency of one tool.")
model McpToolStats {
  @doc("The tool's `gen_ai.tool.name`.")
  name: string;

  @minValue(0)
  requests: int32;

  @minValue(0)
  errors: int32;

  @encodedName("application/json", "error_rate")
  @minValue(0)
  @maxValue(1)
  errorRate: float64;

  @encodedName("application/json", "avg_ms")
  @minValue(0)
  avgMs: float64;

  @encodedName("application/json", "p95_ms")
  @minValue(0)
  p95Ms: float64;
}

@doc("MCP traffic of the window, computed by the Collector from the recorded MCP spans that match the filters: totals, the `group_by` groups, failure classes, time buckets, per-tool statistics and simultaneous server, transport and method breakdowns.")
model GetMcpStatsOutput {
  ...ToolReadContext;
  ...ToolPage;
  totals: McpTrafficTotals;
  groups: McpTrafficGroup[];
  @encodedName("application/json", "failure_classes")
  failureClasses: McpFailureClasses;
  comparison?: McpStatsComparisonResult;

  @doc("Width of every entry in `buckets` in integer milliseconds: the window split into 60 buckets of at least one second.")
  @encodedName("application/json", "bucket_ms")
  @minValue(1000)
  bucketMs: int64;

  @doc("Requests and errors per bucket from the window's start, oldest first; a bucket without requests counts zero.")
  buckets: McpTrafficBucket[];

  @doc("Requests per MCP server: `mcp.server.name`, else the service name. Most requests first.")
  @encodedName("application/json", "by_server")
  byServer: McpNameRequestCount[];

  @doc("Requests per transport: `mcp.transport`, else `network.transport`; empty when neither is recorded. Most requests first.")
  @encodedName("application/json", "by_transport")
  byTransport: McpNameRequestCount[];

  @doc("Requests per MCP method (`mcp.method.name`). Most requests first.")
  @encodedName("application/json", "by_method")
  byMethod: McpNameRequestCount[];

  @doc("Per tool (`gen_ai.tool.name`), the requests that name one: most requests first, paged with `groups`.")
  tools: McpToolStats[];

  @doc("MCP spans the statistics were computed from.")
  @encodedName("application/json", "span_count_analyzed")
  @minValue(0)
  spanCountAnalyzed: int32;

  @doc("True when the Collector stopped reading before every matching span was read (`coverage.scan_complete` is false); every statistic then describes only the spans read.")
  truncated: boolean;

  mode: McpDataMode;
  error?: ToolError;
}

@doc("UpdateFailureStatusInput contract.")
model UpdateFailureStatusInput {
  @encodedName("application/json", "failure_id")
  failureId: FailureId;
  etag: ETag;
  status: FailureStatus;
  @encodedName("application/json", "ignore_until")
  ignoreUntil?: utcDateTime;
  @maxLength(256)
  assignee?: string;
}

@doc("UpdateFailureStatusOutput contract.")
model UpdateFailureStatusOutput {
  failure: FailureSummary;
  error?: ToolError;
}

@doc("ToolFieldError contract.")
model ToolFieldError {
  field: string;
  code: string;
  message: string;
  change: string;
}

@doc("TraceLinkAvailability contract.")
model TraceLinkAvailability {
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  @encodedName("application/json", "span_id")
  spanId: SpanId;
  @doc("Whether this project retains the linked span, in this trace or in another one.")
  available: boolean;
}

@doc("The service and name of a trace's root span.")
model TraceRoot {
  service: string;
  name: string;
}

@doc("One trace of list_traces.")
model ToolTraceRow {
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  root: TraceRoot;
  @doc("Start of the trace's earliest span.")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;
  @doc("From the earliest span's start to the latest span's end.")
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  @encodedName("application/json", "span_count")
  spanCount: int32;
  @doc("Spans with error status.")
  @encodedName("application/json", "error_count")
  errorCount: int64;
  @doc("Services with a span in the trace.")
  services: string[];
  @doc("Failures of the trace's error spans; get_failure_context explains one.")
  @maxItems(3)
  @encodedName("application/json", "failure_ids")
  failureIds: FailureId[];
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;
}

@doc("ToolSession contract.")
model ToolSession {
  ...SessionEntity;
  @maxItems(5)
  @encodedName("application/json", "failure_ids")
  failureIds?: FailureId[];
  @encodedName("application/json", "agent_name")
  agentName?: string;
  models?: string[];
  @encodedName("application/json", "tokens_by_model")
  tokensByModel?: Record<AgentTokens>;
  annotations?: Annotation[];
}

@doc("LogPattern contract.")
model LogPattern {
  template: string;
  count: int64;
  severity: int32;
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
  @maxItems(3)
  @encodedName("application/json", "example_ids")
  exampleIds: string[];
}

@doc("MetricKeySummary contract.")
model MetricKeySummary {
  key: string;
  cardinality: int64;
  @doc("False when the read was capped (`coverage.scan_complete` false): `cardinality` then counts only the values of the records the read loaded.")
  @encodedName("application/json", "cardinality_exact")
  cardinalityExact: boolean;
  @encodedName("application/json", "top_values")
  topValues: AttributeValueCount[];
  @encodedName("application/json", "coverage_share")
  coverageShare: float64;
}

@doc("MetricComparisonOffset contract.")
model MetricComparisonOffset {
  @encodedName("application/json", "offset_ms")
  offsetMs: int64;
}

@doc("MetricRatio contract.")
model MetricRatio {
  @encodedName("application/json", "metric_name")
  metricName: string;
  aggregation?: MetricAggregation;
  @maxItems(32)
  matchers?: TelemetryMatcher[];
}

@doc("MetricComparisonResult contract.")
model MetricComparisonResult {
  window: ToolWindow;
  series: MetricSeriesResult[];
}

@doc("CiAttempt contract.")
model CiAttempt {
  @encodedName("application/json", "run_id")
  runId: SessionId;
  attempt: int32;
  @encodedName("application/json", "provider_run_id")
  providerRunId?: string;
  @encodedName("application/json", "provider_run_url")
  providerRunUrl?: UrlString;
}

@doc("CiPhaseChange contract.")
model CiPhaseChange {
  leg: string;
  phase: string;
  @encodedName("application/json", "current_duration_ms")
  currentDurationMs?: DurationMs;
  @encodedName("application/json", "baseline_duration_ms")
  baselineDurationMs?: DurationMs;
  ratio?: float64;
  @encodedName("application/json", "trace_ids")
  traceIds?: TraceId[];
}

@doc("CiComparison contract.")
model CiComparison {
  @encodedName("application/json", "baseline_run_id")
  baselineRunId?: SessionId;
  @encodedName("application/json", "new_failures")
  newFailures: CiPhaseChange[];
  fixed: CiPhaseChange[];
  @encodedName("application/json", "duration_regressions")
  durationRegressions: CiPhaseChange[];
}

@doc("CiErrorExcerpt contract.")
model CiErrorExcerpt {
  id: string;
  body: string;
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;
  @encodedName("application/json", "span_id")
  spanId?: SpanId;
}


@doc("One span of get_trace; its service's resource is in the trace's `resources`.")
model TraceSpanRow {
  @encodedName("application/json", "span_id")
  spanId: SpanId;
  @doc("Absent for a root span.")
  @encodedName("application/json", "parent_span_id")
  parentSpanId?: SpanId;
  service: string;
  name: string;
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  status: "unset" | "ok" | "error";
  @doc("The status description, when the producer set one.")
  message?: string;
  @doc("Span attributes; omitted when `include_attributes` is false.")
  attributes?: Attribute[];
  @doc("With `selection: critical_path`, the time not covered by the span's children.")
  @encodedName("application/json", "self_time_ms")
  selfTimeMs?: DurationMs;
  @encodedName("application/json", "self_time_share")
  selfTimeShare?: float64;
}

@doc("The resource attributes one service reported for this trace.")
model TraceResource {
  service: string;
  attributes: Attribute[];
}

@doc("One trace: its range, size and errors, the selected spans and each service's resource once.")
model ToolTrace {
  @encodedName("application/json", "trace_id")
  traceId: TraceId;
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;
  @doc("Spans of the trace, before `selection` and paging.")
  @encodedName("application/json", "span_count")
  spanCount: int32;
  @encodedName("application/json", "error_count")
  errorCount: int64;
  services: string[];
  spans: TraceSpanRow[];
  @doc("Omitted when `include_attributes` is false.")
  resources?: TraceResource[];
}

@doc("Canonical classification shared by MCP aggregates and their trace drilldowns.")
enum McpFailureClass { transport, protocol, tool_result, authentication, cancellation, timeout }
