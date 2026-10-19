import assert from "node:assert/strict";
import test, { type TestContext } from "node:test";
import type { MetricRawPoint } from "./contracts/generated/api.js";
import { queryAnalysis } from "./analysis-client.js";
import { ANALYSIS_RESPONSE_BYTES, responseBytes } from "./analysis-budget.js";
import { analysisHeadline } from "./analysis-summary.js";
import { CollectorError } from "./collector.js";
import { readContext } from "./analysis-context.test-helper.js";

const metricName = "worker.queue.depth";
const seriesId = "exact-meter-series";
const window = readContext().window;
const schema = "Mcp.Tools.QueryMetricOutput";

function environment(context: TestContext): void {
  const keys = ["MCP_COLLECTOR_PROJECTS", "QYL_API_KEY", "QYL_PROJECT", "QYL_COLLECTOR_URL"];
  const prior = keys.map(key => [key, process.env[key]] as const);
  keys.forEach(key => delete process.env[key]);
  process.env.QYL_COLLECTOR_URL = "http://collector.test";
  context.after(() => prior.forEach(([key, value]) => {
    if (value === undefined) delete process.env[key];
    else process.env[key] = value;
  }));
}

function point(index: number): MetricRawPoint {
  return {
    time: new Date(Date.parse(window.start_time) + index).toISOString(),
    has_conflict: false,
    data: { type: "number", value: { type: "int", value: "9007199254740993" } },
  };
}

function output(points: MetricRawPoint[], hasMore = false) {
  return {
    ...readContext(1), name: metricName,
    kind: points[0]?.data?.type === "exponential_histogram" ? "exponential_histogram" : "gauge",
    series_id: seriesId, attributes: [{ key: "worker.queue", value: "primary" }], points, has_more: hasMore,
    ...(hasMore ? { next_cursor: `signed-points-after-${points.length}` } : {}),
  };
}

test("an original-point page over the budget is re-queried with a smaller limit and keeps its signed continuation", async context => {
  environment(context);
  const queries: Array<Record<string, unknown>> = [];
  const points = Array.from({ length: 500 }, (_, index) => point(index));
  const before = structuredClone(points);
  context.mock.method(globalThis, "fetch", async (_: unknown, init?: RequestInit) => {
    assert.equal(typeof init?.body, "string");
    const input = JSON.parse(init?.body as string) as Record<string, unknown>;
    queries.push(input);
    return Response.json(output(points.slice(0, Number(input.limit)), true));
  });
  const result = await queryAnalysis("query_metric", {
    metric_name: metricName, raw: true, limit: 500,
    cursor: "signed-current-page", lookback_minutes: 60,
    matchers: [{ key: "worker.queue", op: "eq", value: "primary" }],
  }, schema, { collector: {}, step: async () => {} });
  assert.equal(queries.length, 2);
  const next = queries[1]!;
  assert(Number(next.limit) < 500);
  assert.equal(next.metric_name, metricName);
  assert.equal(next.raw, true);
  assert.equal(next.cursor, "signed-current-page");
  assert.equal(next.start_time, window.start_time);
  assert.equal(next.end_time, window.end_time);
  assert.deepEqual(next.matchers, queries[0]?.matchers);
  assert.deepEqual(result.structuredContent, output(points.slice(0, Number(next.limit)), true));
  assert(responseBytes(result) <= ANALYSIS_RESPONSE_BYTES);
  assert.match(JSON.stringify(result.content), /next_cursor as cursor/u);
  assert.deepEqual(points, before);
});

test("one original point over the MCP budget fails explicitly without trimming its native buckets", async context => {
  environment(context);
  const original = point(0);
  original.data = {
    type: "exponential_histogram", scale: 20, count: "18446744073709551615",
    zero_count: "0", zero_threshold: 0,
    positive: { offset: -4000, bucket_counts: Array.from({ length: 8000 }, () => "18446744073709551615") },
  };
  const page = output([original]);
  const before = structuredClone(page);
  let requests = 0;
  context.mock.method(globalThis, "fetch", async () => {
    requests++;
    return Response.json(page);
  });
  await assert.rejects(queryAnalysis("query_metric", {
    metric_name: metricName, series_id: seriesId, raw: true, limit: 1,
  }, schema, { collector: {}, step: async () => {} }), error => {
    assert(error instanceof CollectorError);
    assert.equal(error.code, "response_too_large");
    assert.equal(error.connectionError, false);
    assert.match(error.message, /\/api\/v1\/metrics\/worker.queue.depth\/series\/exact-meter-series\/points/u);
    return true;
  });
  assert.equal(requests, 1);
  assert.deepEqual(page, before);
});

test("a raw headline counts historically unknown originals and names the point continuation", () => {
  const old = point(0);
  old.data = null;
  const text = analysisHeadline("query_metric", { metric_name: metricName, raw: true }, output([point(1), old], true));
  assert.match(text, new RegExp(`^query_metric: 2 original points of ${metricName} series ${seriesId} \\(worker\\.queue=primary\\), 1 with an unknown original representation\\.`, "u"));
  assert.match(text, /More points: repeat with next_cursor as cursor\./u);
  assert.doesNotMatch(text, /\n/u);
});
