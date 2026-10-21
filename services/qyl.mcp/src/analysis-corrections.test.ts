import assert from "node:assert/strict";
import test, { type TestContext } from "node:test";
import { checkContract } from "./contracts/tool-schemas.js";
import { queryAnalysis } from "./analysis-client.js";
import { analysisHeadline } from "./analysis-summary.js";
import { compactAnalysisResult, responseBytes, ANALYSIS_RESPONSE_BYTES } from "./analysis-budget.js";
import { collectorPost } from "./collector.js";
import { readContext, toolTrace } from "./analysis-context.test-helper.js";
import type { SpanId } from "./contracts/generated/api.js";
import { getTelemetryFixture } from "./telemetry-fixtures.test-helper.js";

function environment(context: TestContext): void {
  const keys = ["MCP_COLLECTOR_PROJECTS", "QYL_API_KEY", "QYL_PROJECT", "QYL_COLLECTOR_URL"];
  const prior = keys.map(key => [key, process.env[key]] as const);
  keys.forEach(key => delete process.env[key]);
  process.env.QYL_COLLECTOR_URL = "http://collector.test";
  context.after(() => prior.forEach(([key, value]) => { if (value === undefined) delete process.env[key]; else process.env[key] = value; }));
}

test("budget re-query preserves filters and accepts Collector's smaller page and cursor", async context => {
  environment(context);
  const queries: Array<Record<string, unknown>> = [];
  context.mock.method(globalThis, "fetch", async (_: unknown, init?: RequestInit) => {
    assert.equal(typeof init?.body, "string");
    const input = JSON.parse(init?.body as string) as Record<string, unknown>; queries.push(input);
    const count = queries.length === 1 ? 100 : 2;
    return Response.json({ ...readContext(count), has_more:true, next_cursor:"signed-next-page",
      attributes:Array.from({length:count}, (_, i) => ({key:`key-${i}`,scope:"span",type:"string",cardinality:1,coverage_share:1,
        top_values:[{value:"x".repeat(1000),count:1,share:1}],semconv:null})),
    });
  });
  const result = await queryAnalysis("discover_attributes", {lookback_minutes:60, key_prefix:"key", limit:100}, "Mcp.Tools.DiscoverAttributesOutput", {collector:{},step:async () => {}});
  assert.equal(queries.length,2); assert.equal(queries[1]?.key_prefix,"key"); assert.equal(queries[1]?.lookback_minutes,60);
  assert.equal(queries[1]?.start_time,readContext().window.start_time);
  assert.equal(queries[1]?.end_time,readContext().window.end_time);
  assert.equal(queries[1]?.start,undefined,"output window names are not input fields");
  assert(Number(queries[1]?.limit)<100); assert.equal((result.structuredContent as Record<string, unknown>)?.next_cursor,"signed-next-page");
  assert(responseBytes(result)<=ANALYSIS_RESPONSE_BYTES);
  const [headline, json] = result.content as Array<{ type: "text"; text: string }>;
  assert.match(headline!.text, /^discover_attributes: 2 attributes\. Top: key-0 \(string, 1 values, 100% coverage, e\.g\. x+…\)/u);
  assert.match(headline!.text, /More: repeat with next_cursor\. → aggregate_telemetry group_by=\["key-0"\]$/u);
  assert.deepEqual(JSON.parse(json!.text), result.structuredContent, "content[1] is the structured result as JSON");
});

test("a ci_log budget re-query asks for fewer runs and fewer of a run's phases, each by its own size", async context => {
  environment(context);
  const sizes: Array<[unknown, unknown]> = [];
  context.mock.method(globalThis, "fetch", async (_: unknown, init?: RequestInit) => {
    const input = JSON.parse(init?.body as string) as Record<string, unknown>; sizes.push([input.limit, input.phase_limit]);
    const runs = Number(input.limit ?? 10), phases = Number(input.phase_limit ?? 50);
    return Response.json({ ...readContext(runs), has_more: true, next_cursor: "signed-next-runs", ci_run_id: "run-0", next_phase_cursor: "signed-next-phases",
      runs: Array.from({ length: runs }, (_, i) => ({ ci_run_id: `run-${i}`, state: "ended", start_time: "2026-10-12T09:30:00Z", error_count: 1, services: ["qyl-ci"], provider_run_url: `https://ci.example/${"r".repeat(900)}/${i}` })),
      phases: Array.from({ length: phases }, (_, i) => ({ leg: "test", phase: `phase-${i}`, status: "error", duration_ms: 1, message: "x".repeat(400) })) });
  });
  const result = await queryAnalysis("ci_log", { ci_run_id: "run-0" }, "Mcp.Tools.CiLogOutput", { collector: {}, step: async () => {} });
  assert(sizes.length > 1 && sizes.slice(1).every(([limit, phases]) => Number(limit) < 10 && Number(phases) < 50), JSON.stringify(sizes));
  assert(responseBytes(result) <= ANALYSIS_RESPONSE_BYTES);
  assert.equal((result.structuredContent as { next_phase_cursor?: string }).next_phase_cursor, "signed-next-phases");
});

test("a run of 50 large phases pages through the response budget, each page's cursor at the first phase it did not return", async context => {
  environment(context);
  // Each phase is about 2 KB with its error logs, so 50 exceed the budget and a page holds fewer.
  const phases = Array.from({ length: 50 }, (_, i) => ({ leg: "test", phase: `phase-${i}`, status: "error", duration_ms: i, message: "m".repeat(400),
    error_log_count: 3, error_logs: Array.from({ length: 3 }, (_, j) => ({ id: `log-${i}-${j}`, body: "b".repeat(400) })) }));
  const asked: unknown[] = [];
  context.mock.method(globalThis, "fetch", async (_: unknown, init?: RequestInit) => {
    const input = JSON.parse(init?.body as string) as Record<string, unknown>; asked.push(input.phase_limit);
    const start = typeof input.phase_cursor === "string" ? Number(input.phase_cursor.slice("after-".length)) : 0;
    const page = phases.slice(start, start + Number(input.phase_limit ?? 50)), end = start + page.length;
    return Response.json({ ...readContext(1), has_more: end < phases.length, ci_run_id: "run-0", page_totals: { phases: phases.length },
      runs: [{ ci_run_id: "run-0", state: "ended", start_time: "2026-10-12T09:30:00Z", error_count: 50, services: ["qyl-ci"] }],
      phases: page, ...(end < phases.length ? { next_phase_cursor: `after-${end}` } : {}) });
  });
  const seen: string[] = [];
  let cursor: string | undefined;
  for (let pages = 0; pages === 0 || cursor !== undefined; pages++) {
    assert(pages < 50, "the pages end");
    const result = await queryAnalysis("ci_log", { ci_run_id: "run-0", ...(cursor === undefined ? {} : { phase_cursor: cursor }) }, "Mcp.Tools.CiLogOutput",
      { collector: {}, step: async () => {} });
    assert(responseBytes(result) <= ANALYSIS_RESPONSE_BYTES);
    const output = result.structuredContent as { phases: Array<{ phase: string }>; next_phase_cursor?: string };
    seen.push(...output.phases.map((phase) => phase.phase));
    // A compacted page hands back the cursor of the first phase it left out, never one past them.
    if (output.next_phase_cursor !== undefined) assert.equal(output.next_phase_cursor, `after-${seen.length}`);
    cursor = output.next_phase_cursor;
  }
  assert.deepEqual(seen, phases.map((phase) => phase.phase), "every phase once, in order");
  assert(asked.some((limit) => Number(limit) < 50), JSON.stringify(asked));
});

test("one oversized trace is compacted with explicit omissions and a valid contract", () => {
  const trace = toolTrace(getTelemetryFixture().traces[0]!);
  trace.spans = Array.from({length:300}, (_, index) => ({...trace.spans[0]!,span_id:index.toString(16).padStart(16,"0") as SpanId, name:"large span "+index,
    attributes:[{key:"recorded.evidence",value:"detail ".repeat(2000)}]}));
  const output = { ...readContext(300), trace, has_more:false, logs:[], annotations:[], links:[], settled:true };
  const result = compactAnalysisResult("get_trace",{trace_id:trace.trace_id},output);
  assert(responseBytes(result)<=ANALYSIS_RESPONSE_BYTES);
  const checked = checkContract("Mcp.Tools.GetTraceOutput", result.structuredContent);
  assert(checked.valid, checked.valid ? "" : checked.message);
  const content = checked.value;
  assert.equal(content.coverage.truncated,true); assert.equal(content.coverage.returned,null);
  assert(Object.values(content.page_totals as Record<string, number>).some(value => value>0));
  assert.match(JSON.stringify(result.content),/other evidence item/u);
  assert.equal(trace.spans.length,300,"compaction cannot mutate cached evidence");
});

test("null coverage is reported as unknown rather than zero", () => {
  const headline = analysisHeadline("list_traces", {}, {...readContext(), traces:[],
    coverage:{...readContext().coverage,examined:null,matched:null,returned:null,scan_complete:false}});
  assert.match(headline, /scan_complete:false — a partial answer over unknown examined records/u);
  assert.doesNotMatch(headline, /Nothing matched/u, "null is unknown, not zero");
});

test("every nested matcher input rejects 33 matchers and triage rejects 51", () => {
  const matchers = Array.from({length:33},()=>({key:"service.name",op:"eq",value:"checkout"}));
  for (const [name,input] of [
    ["CompareGroupsInput",{signal:"spans",group_a:{matchers},group_b:"rest"}],
    ["CompareGroupsInput",{signal:"spans",group_a:{},group_b:{matchers}}],
    ["QueryMetricInput",{metric_name:"count",ratio_of:{metric_name:"total",matchers}}],
    ["TriageInput",{limit:51}],
  ] as const) assert.equal(checkContract(`Mcp.Tools.${name}`, input).valid,false,name);
});

test("Collector problem details never reach the model-facing exception", async context => {
  environment(context);
  context.mock.method(globalThis,"fetch",async () => Response.json({type:"https://qyl.at/problems/internal-error",title:"secret-title",status:500,code:"internal_error",detail:"private database query with secret"},{status:500,headers:{"content-type":"application/problem+json"}}));
  await assert.rejects(collectorPost("/api/v1/test", {}), error => error instanceof Error && !/secret|private database/u.test(error.message));
});
