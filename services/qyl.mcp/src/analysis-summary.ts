/**
 * The one-line headline every analysis result opens with (`content[0]`): what came back,
 * the top rows with the ids a follow-up call takes, and the tool to call next. The whole
 * result follows as JSON, so the headline names; it never stands in for the evidence.
 */
import type {
  AggregateTelemetryOutput,
  AttributeValue,
  CheckTelemetryHealthOutput,
  CiLogOutput,
  CompareGroupsOutput,
  CompareTracesOutput,
  DiscoverAttributesOutput,
  EvidenceSpan,
  ExecutionReference,
  GetAgentRunOutput,
  GetFailureContextOutput,
  GetMcpStatsOutput,
  GetServiceMapOutput,
  GetTraceOutput,
  ListMetricsOutput,
  ListReleasesOutput,
  ListSessionsOutput,
  ListTracesOutput,
  QueryMetricOutput,
  RunSavedQueryOutput,
  SearchLogsOutput,
  ToolCoverage,
  ToolLogRow,
  ToolTraceRow,
  TriageFailure,
  TriageOutput,
  TriageRegression,
  VerifyFixOutput,
} from "./contracts/generated/api.js";

const TOP = 3;
const TEXT = 80;

type Args = Readonly<Record<string, unknown>>;

function short(value: unknown, limit = TEXT): string {
  const text = (typeof value === "string" ? value : JSON.stringify(value) ?? "").replace(/\s+/gu, " ").trim();
  return text.length <= limit ? text : `${text.slice(0, limit - 1)}…`;
}

/** Numbers as a reader quotes them: integers whole, the rest to three significant digits. */
function num(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return "unknown";
  return Number.isInteger(value) ? String(value) : String(Number(value.toPrecision(3)));
}

function pct(share: number | null | undefined): string {
  return share === null || share === undefined ? "unknown" : `${num(share * 100)}%`;
}

function ms(value: number | null | undefined): string {
  return `${num(value)} ms`;
}

function attributeText(value: AttributeValue): string {
  if (value === null || typeof value !== "object" || Array.isArray(value)) return short(value);
  return "value" in value ? short(value.value) : short(value);
}

function top<T>(rows: readonly T[] | undefined, render: (row: T, index: number) => string): string {
  return (rows ?? []).slice(0, TOP).map(render).join("; ");
}

function span(item: EvidenceSpan): string {
  return `${item.service}/${short(item.name, 60)} (span ${item.span_id}, trace ${item.trace_id})`;
}

function regression(row: TriageRegression): string {
  return `${row.service}/${short(row.operation, 50)} ${row.measure} ${num(row.value)} vs ${num(row.baseline_value)} ${row.unit}`;
}

function logLine(log: ToolLogRow): string {
  return `${log.severity ?? log.severity_number} ${log.service}: "${short(log.body, 70)}"${log.trace_id ? ` (trace ${log.trace_id})` : ""}`;
}

function traceRow(trace: ToolTraceRow): string {
  return `${trace.trace_id} ${trace.root.service}/${short(trace.root.name, 50)} ${ms(trace.duration_ms)}` +
    `${trace.error_count > 0 ? `, ${trace.error_count} errors` : ""}${trace.failure_ids[0] ? ` (failure_id ${trace.failure_ids[0]})` : ""}`;
}

/** A failed trace is explained by get_failure_context, any other one read by get_trace. */
function traceNext(traces: readonly ToolTraceRow[]): string {
  const target = traces.find((trace) => trace.error_count > 0) ?? traces[0];
  if (target === undefined) return "widen lookback_minutes or relax the filters";
  return target.error_count > 0 ? `get_failure_context trace_id=${target.trace_id}` : `get_trace trace_id=${target.trace_id}`;
}

/** Partial scans and empty answers are stated, never left for the reader to infer. */
function coverageNote(coverage: ToolCoverage | undefined): string {
  if (coverage === undefined) return "";
  if (coverage.matched === 0) return " Nothing matched in this window; empty does not mean healthy.";
  const parts = [
    coverage.scan_complete ? "" : `scan_complete:false — a partial answer over ${num(coverage.examined)} examined records`,
    coverage.truncated ? "truncated" : "",
  ].filter(Boolean);
  return parts.length === 0 ? "" : ` ${parts.join(", ")}.`;
}

function more(output: { has_more?: boolean; next_cursor?: string }): string {
  return output.has_more === true && output.next_cursor !== undefined ? " More: repeat with next_cursor." : "";
}

/** A failure as service/operation, its error type and its message, the way its fingerprint reads. */
function failureName(row: TriageFailure): string {
  return `${row.service}/${short(row.operation, 50)}${row.exception_type ? ` ${short(row.exception_type, 40)}` : ""}${row.normalized_message ? `: "${short(row.normalized_message, 60)}"` : ""}`;
}

function triage(output: TriageOutput): string {
  const head = `triage: ${output.failures.length} failures, ${output.regressions.length} regressions, ${output.releases.length} releases, ${output.silent_services.length} silent services.`;
  const failures = top(output.failures, (row, index) => {
    const release = row.coinciding_release === undefined ? "" : ` with ${row.coinciding_release.service}@${row.coinciding_release.version}`;
    const cause = row.root_cause_failure_id === undefined || row.root_cause_failure_id === row.failure_id ? "" : `, caused by ${row.root_cause_failure_id}`;
    return `${index + 1}. ${row.change} ×${row.count} ${failureName(row)}${release}${cause} (failure_id ${row.failure_id}${row.status === "open" ? "" : `, ${row.status}`})`;
  });
  const regressions = top(output.regressions, regression);
  const silent = top(output.silent_services, (row) => `${row.service} last seen ${row.last_seen}`);
  const [first] = output.failures;
  // A failure that propagated from another is explained by that one.
  const next = first !== undefined ? `get_failure_context failure_id=${first.root_cause_failure_id ?? first.failure_id}`
    : output.regressions[0] !== undefined ? `aggregate_telemetry for ${output.regressions[0].service}/${output.regressions[0].operation}`
      : output.silent_services[0] !== undefined ? `check_telemetry_health service_name=${output.silent_services[0].service}`
        : "widen lookback_minutes to cover the incident";
  return `${head}${failures ? ` Top failures: ${failures}.` : ""}${regressions ? ` Regressions: ${regressions}.` : ""}${silent ? ` Silent: ${silent}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function failureContext(input: Args, output: GetFailureContextOutput): string {
  const { observed } = output;
  // The root cause is a reference into the evidence spans, which carry each span once.
  const reference = observed.root_cause_span;
  const cause = reference === null ? undefined
    : observed.spans.find((item) => item.span_id === reference.span_id && item.trace_id === reference.trace_id);
  const reason = cause?.exception?.message ?? cause?.status_message;
  const head = reference === null ? "get_failure_context: no root-cause span was recorded."
    : cause === undefined ? `get_failure_context: root cause span ${reference.span_id} (trace ${reference.trace_id}).`
      : `get_failure_context: root cause ${span(cause)}${cause.exception?.type ? ` ${cause.exception.type}` : ""}${reason ? `: ${short(reason)}` : ""}.`;
  const changes = top(output.context.changes, regression);
  const evidence = ` ${observed.error_spans.length} error spans, ${observed.logs.length} logs, ${output.context.similar_failures.length} similar failures; ${output.settled ? "settled" : "not settled"}.`;
  const unknowns = output.unknowns.length === 0 ? "" : ` Unknowns: ${short(output.unknowns.join("; "), 160)}.`;
  const nearest = output.comparison.nearest_success_trace_id;
  const trace = reference?.trace_id ?? observed.error_spans[0]?.trace_id;
  const next = typeof input.failure_id === "string" ? `verify_fix failure_id=${input.failure_id} since=<when the fix shipped>`
    : nearest !== undefined && trace !== undefined ? `compare_traces a={trace_id:${trace}} b={trace_id:${nearest}}`
      : trace !== undefined ? `get_trace trace_id=${trace} selection=errors_with_ancestors`
        : "triage over a wider lookback_minutes";
  return `${head}${evidence}${changes ? ` Changed: ${changes}.` : ""}${unknowns}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function compareGroups(output: CompareGroupsOutput): string {
  const rows = top(output.discriminators, (row) =>
    `${row.key}=${short(row.value, 40)} (A ${pct(row.share_a)} vs B ${pct(row.share_b)}, lift ${num(row.lift)})`);
  const first = output.discriminators[0];
  const next = first === undefined ? "discover_attributes to choose other keys" : `list_traces matchers=[{key:${first.key},op:eq,value:${short(first.value, 40)}}]`;
  return `compare_groups: A ${output.size_a} vs B ${output.size_b}; p95 ${ms(output.durations.a.p95_ms)} vs ${ms(output.durations.b.p95_ms)}.${rows ? ` Top discriminators: ${rows}.` : " No discriminating attribute."} ${short(output.composition.summary, 120)}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function compareTraces(input: Args, output: CompareTracesOutput): string {
  if (!output.comparable) return `compare_traces: not comparable${output.reason ? `: ${short(output.reason, 160)}` : ""}. → list_traces to pick two executions of the same operation`;
  const divergence = output.first_divergence;
  // A divergence names its span by id on the side that has it; the traces are the input's.
  const inB = divergence !== null && divergence.a_span_id === undefined && divergence.b_span_id !== undefined;
  const execution = (inB ? input.b : input.a) as ExecutionReference | undefined;
  const spanId = inB ? divergence?.b_span_id : divergence?.a_span_id;
  const values = divergence === null || (divergence.a === undefined && divergence.b === undefined) ? ""
    : `: A ${divergence.a === undefined ? "absent" : attributeText(divergence.a)} vs B ${divergence.b === undefined ? "absent" : attributeText(divergence.b)}`;
  const head = divergence === null
    ? "compare_traces: no divergence found."
    : `compare_traces: first divergence ${divergence.kind}${divergence.key === divergence.kind ? "" : ` ${short(divergence.key, 60)}`}${values}${spanId === undefined ? "" : ` (span ${spanId} of ${inB ? "B" : "A"})`}.`;
  const next = execution?.trace_id !== undefined ? `get_trace trace_id=${execution.trace_id}`
    : execution?.session_id !== undefined ? `list_traces session_id=${execution.session_id}`
      : "get_failure_context with one of the executions";
  return `${head} ${output.aligned.length} aligned spans, ${output.after_divergence.length} later differences, ${output.unmatched.length} unmatched.${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function verifyFix(output: VerifyFixOutput): string {
  const needs = output.needs === undefined ? "" : `; needs ${num(output.needs.requests)} requests and ${num(output.needs.minutes)} minutes`;
  const recurrences = output.recurrences.length === 0 ? "" : `; recurrences in ${output.recurrences.slice(0, TOP).join(", ")}`;
  const guardrails = output.guardrails.length === 0 ? "" : `; guardrails: ${top(output.guardrails, (row) => `${row.change} ×${row.count} (failure_id ${row.failure_id})`)}`;
  const next = output.verdict === "pending" ? `verify_fix verify_id=${output.verify_id} once the needs are met`
    : output.verdict === "failed" && output.recurrences[0] !== undefined ? `get_trace trace_id=${output.recurrences[0]}`
      : output.verdict === "passed" ? "update_failure_status status=resolved with the failure's etag"
        : "wait for traffic after the change, or widen the window";
  return `verify_fix: ${output.verdict} — before ${output.before.occurrences} occurrences in ${output.before.requests} requests, after ${output.after.occurrences} in ${output.after.requests}${needs}${recurrences}${guardrails}; verify_id ${output.verify_id}.${coverageNote(output.coverage)} → ${next}`;
}

function aggregate(output: AggregateTelemetryOutput): string {
  const measures = (values: Record<string, number | null>): string => Object.entries(values)
    .map(([name, value]) => `${name} ${num(value)}${output.units[name] ? ` ${output.units[name]}` : ""}`).join(", ");
  const key = (values: Record<string, unknown>): string => Object.entries(values).map(([name, value]) => `${name}=${short(value, 40)}`).join(", ") || "all";
  const rows = top(output.groups, (group) => `${key(group.key)}: ${measures(group.measures)}`);
  const other = output.other.groups > 0 ? ` (+${output.other.groups} other groups, ${output.other.count} records)` : "";
  const first = output.groups[0];
  const next = first === undefined ? "discover_attributes to check the group_by keys" : `compare_groups or list_traces with ${key(first.key)} as matchers`;
  return `aggregate_telemetry: ${output.groups.length} groups${other}.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function discover(output: DiscoverAttributesOutput): string {
  const rows = top(output.attributes, (row) =>
    `${row.key} (${row.type}, ${row.cardinality}${row.cardinality_exact ? "" : "+"} values, ${pct(row.coverage_share)} coverage${row.top_values[0] ? `, e.g. ${attributeText(row.top_values[0].value)}` : ""})`);
  const first = output.attributes[0];
  const next = first === undefined ? "widen lookback_minutes or another signal" : `aggregate_telemetry group_by=["${first.key}"]`;
  return `discover_attributes: ${output.attributes.length} attributes.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function agentRun(output: GetAgentRunOutput): string {
  const run = output.run;
  const failed = top(output.findings.failed_tool_calls, (step) => `${step.name}${step.error ? `: ${short(step.error, 60)}` : ""} (step ${step.index}, trace ${step.trace_id})`);
  const repeated = top(output.findings.repeated_calls, (call) => `${call.tool} ×${call.count}`);
  // slowest_steps are indexes into steps.
  const slowest = output.steps.find((step) => step.index === output.findings.slowest_steps[0]);
  const tokens = [run.tokens.input === undefined ? "" : `in ${num(run.tokens.input)}`, run.tokens.output === undefined ? "" : `out ${num(run.tokens.output)}`]
    .filter(Boolean).join(" ");
  const target = output.findings.failed_tool_calls[0]?.trace_id ?? slowest?.trace_id ?? run.trace_id;
  const next = target === undefined ? "list_sessions for another run" : `get_trace trace_id=${target}`;
  return `get_agent_run: ${run.agent_name ?? "agent"} ${output.steps.length} steps, ${run.errors} errors, ${ms(run.duration_ms)}${tokens ? `, tokens ${tokens}` : ""}.` +
    `${failed ? ` Failed tool calls: ${failed}.` : ""}${repeated ? ` Repeated: ${repeated}.` : ""}` +
    `${slowest === undefined ? "" : ` Slowest: ${short(slowest.name, 50)} ${ms(slowest.duration_ms)} (step ${slowest.index}).`}` +
    `${output.unknowns.length ? ` Unknowns: ${short(output.unknowns.join("; "), 120)}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function releases(output: ListReleasesOutput): string {
  const rows = top(output.releases, (row) =>
    `${row.service}@${row.version}${row.current ? " (current)" : ""}: error rate ${pct(row.before.error_rate)}→${pct(row.after.error_rate)}, p95 ${ms(row.before.p95_ms)}→${ms(row.after.p95_ms)}${row.new_failure_ids.length ? `, new failures ${row.new_failure_ids.slice(0, TOP).join(", ")}` : ""}`);
  const newFailure = output.releases.flatMap((row) => row.new_failure_ids)[0];
  const next = newFailure !== undefined ? `get_failure_context failure_id=${newFailure}` : "triage over the release window";
  return `list_releases: ${output.releases.length} releases.${rows ? ` ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function serviceMap(output: GetServiceMapOutput): string {
  const edges = [...output.edges].sort((left, right) => right.error_rate - left.error_rate || right.calls - left.calls);
  const rows = top(edges, (edge) => `${edge.from}→${edge.to} ${edge.calls} calls, ${pct(edge.error_rate)} errors, p95 ${ms(edge.p95_ms)}`);
  const worst = edges[0];
  const next = worst === undefined ? "widen lookback_minutes" : `list_traces service_name=${worst.to} errors_only=true`;
  return `get_service_map: ${output.nodes.length} services, ${output.edges.length} edges${output.unknown_callers ? `, ${output.unknown_callers} unknown callers` : ""}.${rows ? ` Worst edges: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function health(output: CheckTelemetryHealthOutput): string {
  const silent = top(output.silent_services, (row) => `${row.service} (last seen ${row.last_seen})`);
  const gaps = top(output.semconv_gaps, (gap) => `${gap.service}/${short(gap.operation, 40)} lacks ${gap.attribute} (${pct(gap.missing_share)})`);
  const lag = output.ingestion_lag_ms === undefined ? "" : `, ingestion lag ${ms(output.ingestion_lag_ms)}`;
  const next = output.silent_services[0] !== undefined ? `triage service_name=${output.silent_services[0].service}`
    : output.semconv_gaps[0] !== undefined ? "discover_attributes signal=spans"
      : output.metric_conflicts.points > 0 ? `query_metric metric_name=${output.metric_conflicts.series[0]?.metric_name ?? "<conflicting metric>"} over a narrower window`
        : "triage";
  return `check_telemetry_health: ${output.silent_services.length} silent services${silent ? ` (${silent})` : ""}, ${output.orphan_spans.count} orphan spans (${pct(output.orphan_spans.share)}), ${output.metric_conflicts.points} conflicting metric points, ${output.semconv_gaps.length} semconv gaps${gaps ? ` (${gaps})` : ""}, unknown service share ${pct(output.unknown_service_share)}${lag}.${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function mcpStats(output: GetMcpStatsOutput): string {
  const tools = [...output.tools].sort((left, right) => right.errors - left.errors || right.requests - left.requests);
  const rows = top(tools, (tool) => `${tool.name} ${tool.requests} requests, ${tool.errors} errors, p95 ${ms(tool.p95_ms)}`);
  const worst = tools[0];
  const next = worst === undefined ? "widen lookback_minutes" : `list_traces tool_name=${worst.name}${worst.errors > 0 ? " errors_only=true" : ""}`;
  return `get_mcp_stats: ${output.totals.requests} requests, ${output.totals.errors} errors (${pct(output.totals.error_rate)}), p95 ${ms(output.totals.p95_ms)} over ${output.span_count_analyzed} spans${output.truncated ? " from an incomplete scan" : ""}.${rows ? ` Tools: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function listTraces(output: ListTracesOutput): string {
  const errors = output.traces.filter((trace) => trace.error_count > 0).length;
  const rows = top(output.traces, traceRow);
  return `list_traces: ${output.traces.length} traces, ${errors} with errors.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${traceNext(output.traces)}`;
}

function getTrace(output: GetTraceOutput): string {
  const trace = output.trace;
  const errorSpans = trace.spans.filter((item) => item.status === "error");
  const root = trace.spans.find((item) => item.parent_span_id === undefined);
  const rows = top(errorSpans, (item) => `${item.service}/${short(item.name, 50)} (span ${item.span_id})${item.message ? `: ${short(item.message, 60)}` : ""}`);
  const next = trace.error_count > 0 ? `get_failure_context trace_id=${trace.trace_id}` : `search_logs trace_id=${trace.trace_id}`;
  return `get_trace: ${trace.trace_id} ${trace.span_count} spans (${trace.spans.length} returned), ${trace.error_count} error spans${root === undefined ? "" : `; root ${root.service}/${short(root.name, 60)}`}, ${ms(trace.duration_ms)}.${rows ? ` Errors: ${rows}.` : ""}${output.logs?.length ? ` ${output.logs.length} correlated logs.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function listSessions(output: ListSessionsOutput): string {
  const rows = top(output.sessions, (session) =>
    `${session.session_id} ${session.agent_name ?? session.services[0] ?? "unknown"} ${session.state}, ${session.trace_count} traces, ${session.error_count} errors`);
  const failing = output.sessions.find((session) => session.error_count > 0);
  const next = failing !== undefined ? `get_failure_context session_id=${failing.session_id}`
    : output.sessions[0] !== undefined ? `get_agent_run session_id=${output.sessions[0].session_id}` : "widen lookback_minutes";
  return `list_sessions: ${output.sessions.length} sessions.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${next}`;
}

function searchLogs(output: SearchLogsOutput): string {
  if (output.patterns !== undefined) {
    const rows = top(output.patterns, (pattern) => `"${short(pattern.template, 70)}" ×${pattern.count}${pattern.example_trace_ids[0] ? ` (trace ${pattern.example_trace_ids[0]})` : ""}`);
    const example = output.patterns.find((pattern) => pattern.example_trace_ids.length > 0)?.example_trace_ids[0];
    return `search_logs: ${output.patterns.length} patterns${output.total_matched === undefined ? "" : ` over ${output.total_matched} logs`}.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${example === undefined ? "search_logs with query to read the records" : `get_trace trace_id=${example}`}`;
  }
  const logs = output.logs ?? [];
  const rows = top(logs, logLine);
  const traced = logs.find((log) => log.trace_id !== undefined)?.trace_id;
  const context = output.context_logs?.length ? `, ${output.context_logs.length} context records` : "";
  return `search_logs: ${logs.length} logs${output.total_matched === undefined ? "" : ` of ${output.total_matched}`}${context}.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${traced === undefined ? "widen lookback_minutes or relax the query" : `get_trace trace_id=${traced}`}`;
}

function ciLog(output: CiLogOutput): string {
  if (output.phases !== undefined) {
    const failed = output.phases.filter((phase) => phase.status === "error");
    const rows = top(failed, (phase) => `${phase.leg}/${phase.phase}${phase.message ? `: ${short(phase.message, 60)}` : ""}`);
    const comparison = output.comparison === undefined ? ""
      : ` Versus ${output.comparison.baseline_run_id ?? "the baseline"}: ${output.comparison.new_failures.length} new failures, ${output.comparison.fixed.length} fixed, ${output.comparison.duration_regressions.length} slower phases.`;
    const phasesMore = output.has_more && output.next_phase_cursor !== undefined ? " More phases: repeat with next_phase_cursor as phase_cursor." : "";
    return `ci_log: run ${output.run_id ?? "unknown"}: ${output.phases.length} phases, ${failed.length} failed.${rows ? ` Failed: ${rows}.` : ""}${comparison}${coverageNote(output.coverage)}${phasesMore} → ${failed.length > 0 && output.run_id ? `get_failure_context ci_run_id=${output.run_id}` : "ci_log without run_id for other runs"}`;
  }
  const runs = output.runs ?? [];
  const rows = top(runs, (run) => `${run.run_id} ${run.state}, ${run.error_count} errors, ${ms(run.duration_ms)}`);
  const target = runs.find((run) => run.error_count > 0) ?? runs[0];
  return `ci_log: ${runs.length} runs.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${target === undefined ? "widen lookback_minutes" : `ci_log run_id=${target.run_id}`}`;
}

function listMetrics(output: ListMetricsOutput): string {
  const rows = top(output.items, (item) => `${item.name} (${item.kind}${item.unit ? `, ${item.unit}` : ""}, ${item.series_count} series${item.has_conflict ? ", conflicting descriptors" : ""}${item.suggested_group_by.length ? `, group by ${item.suggested_group_by.slice(0, TOP).join(", ")}` : ""})`);
  const first = output.items[0];
  const groupBy = first?.suggested_group_by.length ? ` group_by=${JSON.stringify(first.suggested_group_by.slice(0, TOP))}` : "";
  return `list_metrics: ${output.items.length} instruments.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${first === undefined ? "widen lookback_minutes" : `query_metric metric_name=${first.name}${groupBy}`}`;
}

/** query_metric with raw: one series' original points. */
function rawPoints(output: QueryMetricOutput & { points: NonNullable<QueryMetricOutput["points"]> }): string {
  const unknown = output.points.filter((point) => point.data === null).length;
  const labels = (output.attributes ?? []).map((attribute) => `${attribute.key}=${attributeText(attribute.value)}`).join(", ") || "no attributes";
  const pointsMore = output.has_more === true && output.next_cursor !== undefined ? " More points: repeat with next_cursor as cursor." : "";
  const exemplar = output.points.flatMap((point) => point.exemplars ?? []).find((item) => item.trace_id !== undefined)?.trace_id;
  return `query_metric: ${output.points.length} original points of ${output.name} series ${short(output.series_id, 60)} (${short(labels, 60)})${unknown ? `, ${unknown} with an unknown original representation` : ""}.${pointsMore}${coverageNote(output.coverage)} → ${exemplar === undefined ? `query_metric metric_name=${output.name} to aggregate the series` : `get_trace trace_id=${exemplar}`}`;
}

function queryMetric(output: QueryMetricOutput): string {
  if (output.points !== undefined) return rawPoints({ ...output, points: output.points });
  const unit = output.unit ? ` ${output.unit}` : "";
  const series = output.series ?? [];
  // A series lists only the buckets that hold points, and `value` aggregates the whole window.
  const rows = top(series, (series) => {
    const labels = series.attributes.map((attribute) => `${attribute.key}=${attributeText(attribute.value)}`).join(", ") || "all";
    const last = series.buckets.at(-1);
    return `${short(labels, 60)}: ${num(series.value)}${unit} over the window, ${series.buckets.length} buckets with points${last === undefined ? "" : `, last ${num(last.value)}${unit}`}`;
  });
  const buckets = Math.max(0, ...series.map((item) => item.buckets.length));
  const step = buckets > 60 ? ` ${buckets} buckets in one series: omit step_ms for at most 60.` : "";
  const exemplar = output.exemplar_trace_ids?.[0];
  return `query_metric: ${output.name} ${output.aggregation} (${output.kind}) step ${output.step_ms} ms, ${series.length} series${output.approximate ? ", approximate" : ""}.${rows ? ` ${rows}.` : ""}${step}${coverageNote(output.coverage)} → ${exemplar === undefined ? "aggregate_telemetry for exact per-request span durations" : `get_trace trace_id=${exemplar}`}`;
}

function runSavedQuery(output: RunSavedQueryOutput): string {
  if (output.saved_queries !== undefined) {
    const rows = top(output.saved_queries, (query) => `${short(query.name, 60)} (${query.filter.signal}, id ${query.id}, etag ${query.etag})`);
    const first = output.saved_queries[0];
    return `run_saved_query: ${output.saved_queries.length} saved queries.${rows ? ` ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${first === undefined ? "save_query to save one" : `run_saved_query id=${first.id}`}`;
  }
  const name = output.saved_query === undefined ? "run_saved_query" : `run_saved_query ${short(output.saved_query.name, 60)}`;
  if (output.metric !== undefined) {
    return `${name}: metric ${output.metric.name} ${output.metric.aggregation}, ${output.metric.series.length} series.${coverageNote(output.coverage)} → query_metric metric_name=${output.metric.name} for other windows`;
  }
  if (output.logs !== undefined) {
    const traced = output.logs.find((log) => log.trace_id !== undefined)?.trace_id;
    return `${name}: ${output.logs.length} logs.${output.logs.length ? ` Top: ${top(output.logs, logLine)}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${traced === undefined ? "search_logs" : `get_trace trace_id=${traced}`}`;
  }
  const traces = output.traces ?? [];
  const rows = top(traces, traceRow);
  return `${name}: ${traces.length} traces.${rows ? ` Top: ${rows}.` : ""}${coverageNote(output.coverage)}${more(output)} → ${traceNext(traces)}`;
}

/** The headline of one analysis result; `input` supplies what the output does not repeat. */
export function analysisHeadline(operation: string, input: object, output: object): string {
  const call = input as Args;
  switch (operation) {
    case "triage": return triage(output as TriageOutput);
    case "get_failure_context": return failureContext(call, output as GetFailureContextOutput);
    case "compare_groups": return compareGroups(output as CompareGroupsOutput);
    case "compare_traces": return compareTraces(call, output as CompareTracesOutput);
    case "verify_fix": return verifyFix(output as VerifyFixOutput);
    case "aggregate_telemetry": return aggregate(output as AggregateTelemetryOutput);
    case "discover_attributes": return discover(output as DiscoverAttributesOutput);
    case "get_agent_run": return agentRun(output as GetAgentRunOutput);
    case "list_releases": return releases(output as ListReleasesOutput);
    case "get_service_map": return serviceMap(output as GetServiceMapOutput);
    case "check_telemetry_health": return health(output as CheckTelemetryHealthOutput);
    case "get_mcp_stats": return mcpStats(output as GetMcpStatsOutput);
    case "list_traces": return listTraces(output as ListTracesOutput);
    case "get_trace": return getTrace(output as GetTraceOutput);
    case "list_sessions": return listSessions(output as ListSessionsOutput);
    case "search_logs": return searchLogs(output as SearchLogsOutput);
    case "ci_log": return ciLog(output as CiLogOutput);
    case "list_metrics": return listMetrics(output as ListMetricsOutput);
    case "query_metric": return queryMetric(output as QueryMetricOutput);
    case "run_saved_query": return runSavedQuery(output as RunSavedQueryOutput);
    default: return `${operation}: result follows as JSON.`;
  }
}
