import assert from "node:assert/strict";
import test from "node:test";
import { analysisHeadline } from "./analysis-summary.js";
import { checkContract, type ContractSchemaName } from "./contracts/tool-schemas.js";
import { readContext, toolLog, toolSummary, toolTrace } from "./analysis-context.test-helper.js";
import { getTelemetryFixture } from "./telemetry-fixtures.test-helper.js";

const TRACE = "0af7651916cd43dd8448eb211c80319c";
const OTHER_TRACE = "1bf7651916cd43dd8448eb211c80319d";
const SPAN = "b7ad6b7169203331";
const NOW = "2026-10-19T12:00:00Z";
const context = readContext(3);
const window = context.window;
const tokens = { input: 1200, output: 300 };
const evidence = {
  trace_id: TRACE, span_id: SPAN, service: "checkout", name: "SELECT orders", status: "error",
  status_message: "timeout", exception: { type: "TimeoutError", message: "orders-db timed out after 5s" },
  key_attributes: [], duration_ms: 5000, role: "matched",
} as const;
const regression = { service: "checkout", operation: "POST /cart", measure: "p95_ms", value: 1840, baseline_value: 210, denominator: 195, unit: "ms" } as const;
const fixture = getTelemetryFixture();
const errorTrace = fixture.traces.find((trace) => trace.has_error)!;

/** One contract-valid result per analysis tool, with the headline each must produce. */
const cases: Array<{ operation: string; schema: ContractSchemaName; input?: object; output: object; expect: RegExp[] }> = [
  {
    operation: "triage", schema: "Mcp.Tools.TriageOutput", input: { lookback_minutes: 1440 },
    output: { ...context,
      failures: [{ failure_id: "fail_orders_timeout", signature: "TimeoutError: orders-db timed out", change: "new", count: 10,
        baseline_count: 0, affected_traces: 10, services: ["checkout"], first_seen: NOW, example_trace_id: TRACE,
        coinciding_release: { service: "checkout", version: "2.3", first_seen: NOW }, status: "open", etag: "\"1\"",
        root_cause_failure_id: "fail_db_down" }],
      regressions: [regression],
      releases: [{ service: "checkout", version: "2.3", first_seen: NOW }],
      silent_services: [{ service: "billing", last_seen: NOW, usual_rate: 2.5, unit: "spans/min" }] },
    expect: [/^triage: 1 failures, 1 regressions, 1 releases, 1 silent services\./u,
      /1\. new ×10 TimeoutError: orders-db timed out \[checkout\] with checkout@2\.3, caused by fail_db_down \(failure_id fail_orders_timeout\)/u,
      /checkout\/POST \/cart p95_ms 1840 vs 210 ms/u, /→ get_failure_context failure_id=fail_db_down$/u],
  },
  {
    operation: "list_failures", schema: "Mcp.Tools.ListFailuresOutput",
    output: { ...context, failures: [{ failure_id: "fail_1", etag: "\"3\"", signature: "s", normalized_message: "orders-db timed out after # seconds",
      operation: "SELECT orders", service: "orders-db", first_seen: NOW, last_seen: NOW, count: 4, affected_traces: 4, affected_sessions: 1,
      releases_seen: ["2.3"], status: "open", updated_at: NOW, annotation_count: 0, example_trace_ids: [TRACE] }] },
    expect: [/^list_failures: 1 failures\. 1\. orders-db\/SELECT orders ×4 open "orders-db timed out after # seconds" \(failure_id fail_1, etag "3"\)/u,
      /→ get_failure_context failure_id=fail_1$/u],
  },
  {
    operation: "get_failure_context", schema: "Mcp.Tools.GetFailureContextOutput", input: { failure_id: "fail_1" },
    output: { ...context, observed: { spans: [evidence], root_cause_span: { trace_id: TRACE, span_id: SPAN }, error_spans: [{ trace_id: TRACE, span_id: SPAN }],
      propagation: [], logs: [], retries: [], fanout: [] },
      comparison: { nearest_success_trace_id: OTHER_TRACE, first_divergence: null },
      context: { similar_failures: [], changes: [regression], annotations: [] }, settled: true, unknowns: ["whether the pool was saturated"] },
    expect: [/^get_failure_context: root cause checkout\/SELECT orders \(span b7ad6b7169203331, trace 0af7651916cd43dd8448eb211c80319c\) TimeoutError: orders-db timed out after 5s\./u,
      /1 error spans, 0 logs, 0 similar failures; settled\. Changed: checkout\/POST \/cart p95_ms 1840 vs 210 ms\./u,
      /Unknowns: whether the pool was saturated\./u, /→ verify_fix failure_id=fail_1 since=<when the fix shipped>$/u],
  },
  {
    // Without a failure_id the nearest success is the comparison to make.
    operation: "get_failure_context", schema: "Mcp.Tools.GetFailureContextOutput", input: { trace_id: TRACE },
    output: { ...context, observed: { spans: [], root_cause_span: null, error_spans: [{ trace_id: TRACE, span_id: SPAN }],
      propagation: [], logs: [], retries: [], fanout: [] },
      comparison: { nearest_success_trace_id: OTHER_TRACE, first_divergence: null },
      context: { similar_failures: [], changes: [], annotations: [] }, settled: false, unknowns: [] },
    expect: [/^get_failure_context: no root-cause span was recorded\. 1 error spans/u,
      new RegExp(`→ compare_traces a=\\{trace_id:${TRACE}\\} b=\\{trace_id:${OTHER_TRACE}\\}$`, "u")],
  },
  {
    operation: "compare_groups", schema: "Mcp.Tools.CompareGroupsOutput",
    output: { ...context, discriminators: [{ key: "cloud.region", value: "eu", share_a: 1, share_b: 0.2, count_a: 10, count_b: 4, lift: 5 }],
      structural: [], durations: { a: { p50_ms: 900, p95_ms: 1840, p99_ms: 2000 }, b: { p50_ms: 100, p95_ms: 210, p99_ms: 300 } },
      composition: { changed: true, traffic_mix_changed: false, same_traffic_changed: true, summary: "Same traffic got slower." },
      size_a: 10, size_b: 20, excluded_keys: [] },
    expect: [/^compare_groups: A 10 vs B 20; p95 1840 ms vs 210 ms\. Top discriminators: cloud\.region=eu \(A 100% vs B 20%, lift 5\)\./u,
      /→ list_traces matchers=\[\{key:cloud\.region,op:eq,value:eu\}\]$/u],
  },
  {
    operation: "compare_traces", schema: "Mcp.Tools.CompareTracesOutput", input: { a: { trace_id: TRACE }, b: { trace_id: OTHER_TRACE } },
    output: { ...context, first_divergence: { kind: "status", key: "status", a: "error", b: "ok", a_span_id: SPAN, b_span_id: "c7ad6b7169203332" },
      aligned: [{ a_span_id: SPAN, b_span_id: "c7ad6b7169203332", name: "SELECT orders",
        differences: [{ kind: "status", key: "status", a: "error", b: "ok" }] }], after_divergence: [], unmatched: [], comparable: true },
    expect: [/^compare_traces: first divergence status: A error vs B ok \(span b7ad6b7169203331 of A\)\. 1 aligned spans, 0 later differences, 0 unmatched\./u,
      /→ get_trace trace_id=0af7651916cd43dd8448eb211c80319c$/u],
  },
  {
    // A span only B has is named by B's span id and read in B's trace.
    operation: "compare_traces", schema: "Mcp.Tools.CompareTracesOutput", input: { a: { trace_id: TRACE }, b: { trace_id: OTHER_TRACE } },
    output: { ...context, first_divergence: { kind: "extra", key: "retry orders", b_span_id: "c7ad6b7169203332" },
      aligned: [], after_divergence: [], unmatched: [{ kind: "extra", key: "retry orders", b_span_id: "c7ad6b7169203332" }], comparable: true },
    expect: [/^compare_traces: first divergence extra retry orders \(span c7ad6b7169203332 of B\)\./u,
      /→ get_trace trace_id=1bf7651916cd43dd8448eb211c80319d$/u],
  },
  {
    operation: "verify_fix", schema: "Mcp.Tools.VerifyFixOutput",
    output: { ...context, verdict: "pending",
      before: { window, occurrences: 10, requests: 100, rate: 0.1, unit: "requests" },
      after: { window, occurrences: 0, requests: 4, rate: 0, unit: "requests" },
      needs: { requests: 96, minutes: 24 }, recurrences: [], guardrails: [], verify_id: "vf_1" },
    expect: [/^verify_fix: pending — before 10 occurrences in 100 requests, after 0 in 4; needs 96 requests and 24 minutes; verify_id vf_1\./u,
      /→ verify_fix verify_id=vf_1 once the needs are met$/u],
  },
  {
    operation: "aggregate_telemetry", schema: "Mcp.Tools.AggregateTelemetryOutput",
    output: { ...context, groups: [{ key: { "http.route": "/checkout" }, measures: { count: 195, duration_p95: 1840 } }],
      other: { groups: 2, count: 40 }, units: { count: "{span}", duration_p95: "ms" } },
    expect: [/^aggregate_telemetry: 1 groups \(\+2 other groups, 40 records\)\. Top: http\.route=\/checkout: count 195 \{span\}, duration_p95 1840 ms\./u,
      /→ compare_groups or list_traces with http\.route=\/checkout as matchers$/u],
  },
  {
    operation: "discover_attributes", schema: "Mcp.Tools.DiscoverAttributesOutput",
    output: { ...context, attributes: [{ key: "http.response.status_code", scope: "span", type: "int", coverage_share: 0.5, cardinality: 3,
      cardinality_exact: true, top_values: [{ value: { type: "int", value: "500" }, count: 4, share: 0.4 }], semconv: null }] },
    expect: [/^discover_attributes: 1 attributes\. Top: http\.response\.status_code \(int, 3 values, 50% coverage, e\.g\. 500\)\./u,
      /→ aggregate_telemetry group_by=\["http\.response\.status_code"\]$/u],
  },
  {
    operation: "get_agent_run", schema: "Mcp.Tools.GetAgentRunOutput",
    output: { ...context, run: { agent_name: "triager", models: ["claude"], started: NOW, duration_ms: 4200, tokens, errors: 1, trace_id: TRACE },
      steps: [{ index: 0, kind: "tool_call", name: "search", duration_ms: 300, tokens, status: "error", error: "timeout", trace_id: TRACE, span_id: SPAN }],
      findings: { repeated_calls: [{ tool: "search", argument_hash: "h", count: 3 }],
        failed_tool_calls: [{ index: 0, kind: "tool_call", name: "search", duration_ms: 300, tokens, status: "error", error: "timeout", trace_id: TRACE, span_id: SPAN }],
        slowest_steps: [0], token_share: [] }, unknowns: [] },
    expect: [/^get_agent_run: triager 1 steps, 1 errors, 4200 ms, tokens in 1200 out 300\. Failed tool calls: search: timeout \(step 0, trace 0af7651916cd43dd8448eb211c80319c\)\. Repeated: search ×3\./u,
      /Slowest: search 300 ms \(step 0\)\./u, /→ get_trace trace_id=0af7651916cd43dd8448eb211c80319c$/u],
  },
  {
    operation: "list_releases", schema: "Mcp.Tools.ListReleasesOutput",
    output: { ...context, releases: [{ service: "regional", version: "2.3", first_seen: NOW, last_seen: NOW, current: true, concurrent: false,
      traffic_share: 1, before: { error_rate: 0, p95_ms: 200 }, after: { error_rate: 0.1, p95_ms: 270 }, new_failure_ids: ["fail_1"] }] },
    expect: [/^list_releases: 1 releases\. regional@2\.3 \(current\): error rate 0%→10%, p95 200 ms→270 ms, new failures fail_1\./u,
      /→ get_failure_context failure_id=fail_1$/u],
  },
  {
    operation: "get_service_map", schema: "Mcp.Tools.GetServiceMapOutput",
    output: { ...context, nodes: [{ name: "checkout", kind: "service", calls: 10, error_rate: 0, p95_ms: 200, unit: "spans" }],
      edges: [{ from: "checkout", to: "orders-db", calls: 10, error_rate: 0.3, p95_ms: 269.99999999999994, example_operations: [], unit: "spans" }], unknown_callers: 0 },
    expect: [/^get_service_map: 1 services, 1 edges\. Worst edges: checkout→orders-db 10 calls, 30% errors, p95 270 ms\./u,
      /→ list_traces service_name=orders-db errors_only=true$/u],
  },
  {
    operation: "check_telemetry_health", schema: "Mcp.Tools.CheckTelemetryHealthOutput",
    output: { ...context, silent_services: [{ service: "billing", last_seen: NOW, usual_rate: null, unit: "spans/min" }],
      orphan_spans: { count: 2, share: 0.01, services: ["checkout"], unit: "spans" }, metric_conflicts: { points: 0, unit: "points", series: [] },
      semconv_gaps: [], unknown_service_share: 0, schema_urls: [], data_span: { oldest: NOW, days: 1 }, ingestion_lag_ms: 1500 },
    expect: [/^check_telemetry_health: 1 silent services \(billing \(last seen 2026-10-19T12:00:00Z\)\), 2 orphan spans \(1%\)/u,
      /, ingestion lag 1500 ms\./u, /→ triage service_name=billing$/u],
  },
  {
    operation: "get_mcp_stats", schema: "Mcp.Tools.GetMcpStatsOutput",
    output: { ...context, totals: { requests: 10, errors: 2, error_rate: 0.2, p50_ms: 10, p95_ms: 90, unit: "requests" }, groups: [],
      failure_classes: { transport: 0, protocol: 0, tool_result: 2, authentication: 0, cancellation: 0, timeout: 0 },
      bucket_ms: 60_000, buckets: [], by_server: [], by_transport: [], by_method: [],
      tools: [{ name: "get_trace", requests: 4, errors: 2, error_rate: 0.5, avg_ms: 40, p95_ms: 90 }], span_count_analyzed: 10, truncated: false, mode: "live" },
    expect: [/^get_mcp_stats: 10 requests, 2 errors \(20%\), p95 90 ms over 10 spans\. Tools: get_trace 4 requests, 2 errors, p95 90 ms\./u,
      /→ list_traces tool_name=get_trace errors_only=true$/u],
  },
  {
    operation: "list_traces", schema: "Mcp.Tools.ListTracesOutput",
    output: { ...context, mode: "live", traces: fixture.traces.map(toolSummary) },
    expect: [new RegExp(`^list_traces: ${fixture.traces.length} traces, ${fixture.traces.filter((trace) => trace.has_error).length} with errors\\. Top: ${fixture.traces[0]!.trace_id} `, "u"),
      new RegExp(`→ get_failure_context trace_id=${errorTrace.trace_id}$`, "u")],
  },
  {
    operation: "get_trace", schema: "Mcp.Tools.GetTraceOutput",
    output: { ...context, mode: "live", trace: toolTrace(errorTrace) },
    expect: [new RegExp(`^get_trace: ${errorTrace.trace_id} ${errorTrace.span_count} spans \\(${errorTrace.spans.length} returned\\), [1-9]\\d* error spans`, "u"),
      new RegExp(`→ get_failure_context trace_id=${errorTrace.trace_id}$`, "u")],
  },
  {
    operation: "list_sessions", schema: "Mcp.Tools.ListSessionsOutput",
    output: { ...context, mode: "live", sessions: fixture.sessions },
    expect: [new RegExp(`^list_sessions: ${fixture.sessions.length} sessions\\. Top: ${fixture.sessions[0]!.session_id} `, "u"), /→ get_(failure_context|agent_run) session_id=/u],
  },
  {
    operation: "search_logs", schema: "Mcp.Tools.SearchLogsOutput",
    output: { ...context, mode: "live", logs: fixture.logs.slice(0, 2).map(toolLog) },
    expect: [/^search_logs: 2 logs\. Top: /u, /→ (get_trace trace_id=[a-f0-9]{32}|widen lookback_minutes or relax the query)$/u],
  },
  {
    operation: "search_logs", schema: "Mcp.Tools.SearchLogsOutput", input: { group_by: "pattern" },
    output: { ...context, mode: "live", total_matched: 7, patterns: [{ template: "orders-db timed out after # s", count: 7, severity: 17,
      first_seen: NOW, last_seen: NOW, example_trace_ids: [TRACE] }] },
    expect: [/^search_logs: 1 patterns over 7 logs\. Top: "orders-db timed out after # s" ×7 \(trace 0af7651916cd43dd8448eb211c80319c\)\./u,
      /→ get_trace trace_id=0af7651916cd43dd8448eb211c80319c$/u],
  },
  {
    operation: "ci_log", schema: "Mcp.Tools.CiLogOutput", input: { run_id: "run-7" },
    output: { ...context, mode: "live", run_id: "run-7", phases: [
      { leg: "linux", phase: "build", status: "ok", duration_ms: 1000 },
      { leg: "linux", phase: "test", status: "error", duration_ms: 2000, message: "3 tests failed" }] },
    expect: [/^ci_log: run run-7: 2 phases, 1 failed\. Failed: linux\/test: 3 tests failed\./u, /→ get_failure_context ci_run_id=run-7$/u],
  },
  {
    operation: "list_metrics", schema: "Mcp.Tools.ListMetricsOutput",
    output: { ...context, items: [{ name: "http.server.request.duration", kind: "histogram", unit: "s", temporality: "cumulative",
      services: ["checkout"], series_count: 2, last_seen: NOW, supported_aggregations: ["avg", "p95"], has_conflict: false }] },
    expect: [/^list_metrics: 1 instruments\. Top: http\.server\.request\.duration \(histogram, s, 2 series\)\./u,
      /→ query_metric metric_name=http\.server\.request\.duration$/u],
  },
  {
    operation: "query_metric", schema: "Mcp.Tools.QueryMetricOutput",
    output: { ...readContext(1, false), numeric_representation: "float64", approximate: false, name: "http.server.request.duration", kind: "histogram", unit: "s",
      aggregation: "p95", step_ms: 60_000, start_time: "2026-10-18T12:00:00Z", end_time: "2026-10-19T12:00:00Z", truncated: false,
      // A series lists only the buckets that hold points; a caller's small step_ms can still give more than 60.
      series: [{ attributes: [{ key: "http.route", value: "/cart" }], value: 0.25,
        buckets: Array.from({ length: 90 }, (_, index) => ({ bucket_start: new Date(Date.parse("2026-10-18T12:00:00Z") + index * 60_000).toISOString(),
          value: index === 89 ? 0.21 : 0.3, point_count: 1 })) }] },
    expect: [/^query_metric: http\.server\.request\.duration p95 \(histogram\) step 60000 ms, 1 series\. http\.route=\/cart: 0\.25 s over the window, 90 buckets with points, last 0\.21 s\./u,
      /90 buckets in one series: omit step_ms for at most 60\./u, /→ aggregate_telemetry for exact per-request span durations$/u],
  },
  {
    operation: "list_saved_queries", schema: "Mcp.Tools.ListSavedQueriesOutput",
    output: { ...context, saved_queries: [{ id: "sq-1", etag: "\"1\"", created_at: NOW, updated_at: NOW, name: "checkout errors",
      filter: { signal: "traces", errors_only: true } }] },
    expect: [/^list_saved_queries: 1 saved queries\. checkout errors \(traces, id sq-1, etag "1"\)\./u, /→ run_saved_query id=sq-1$/u],
  },
  {
    operation: "run_saved_query", schema: "Mcp.Tools.RunSavedQueryOutput",
    output: { ...readContext(1, false), mode: "live", result: { signal: "traces", items: [toolSummary(errorTrace)], has_more: false } },
    expect: [new RegExp(`^run_saved_query: 1 traces\\. Top: ${errorTrace.trace_id} `, "u"), new RegExp(`→ get_failure_context trace_id=${errorTrace.trace_id}$`, "u")],
  },
];

test("every analysis headline is one line with the top rows, their ids and the next tool", () => {
  for (const { operation, schema, input, output, expect } of cases) {
    const checked = checkContract(schema, output);
    assert(checked.valid, `${operation} fixture: ${checked.valid ? "" : checked.message}`);
    const headline = analysisHeadline(operation, input ?? {}, output);
    assert.doesNotMatch(headline, /\n/u, operation);
    for (const pattern of expect) assert.match(headline, pattern, `${operation}: ${headline}`);
  }
});

test("an empty answer says it matched nothing instead of reading as healthy", () => {
  const empty = { ...readContext(0), silent_services: [], orphan_spans: { count: 0, share: 0, services: [], unit: "spans" },
    metric_conflicts: { points: 0, unit: "points", series: [] }, semconv_gaps: [], unknown_service_share: 0, schema_urls: [],
    data_span: { oldest: null, days: null } };
  assert(checkContract("Mcp.Tools.CheckTelemetryHealthOutput", empty).valid);
  assert.match(analysisHeadline("check_telemetry_health", { service_name: "unknown-service" }, empty),
    /Nothing matched in this window; empty does not mean healthy\./u);
});
