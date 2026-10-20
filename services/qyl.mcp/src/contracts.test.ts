import assert from "node:assert/strict";
import test from "node:test";
import { CollectorError, parseCollectorBody } from "./collector.js";
import { checkContract, contractSchema, type ContractSchemaName } from "./contracts/tool-schemas.js";
import documents from "./contracts/generated/tool-schemas.json" with { type: "json" };
import { readContext, toolLog, toolTrace, toolSummary } from "./analysis-context.test-helper.js";
import { getTelemetryFixture } from "./telemetry-fixtures.test-helper.js";
import { toolError } from "./request-scope.js";

const valid = (name: ContractSchemaName, value: unknown): boolean => checkContract(name, value).valid;

function property(name: ContractSchemaName, key: string): Record<string, unknown> {
  return (documents[name] as unknown as { properties: Record<string, Record<string, unknown>> }).properties[key]!;
}

test("generated tool inputs own their bounds, required members and advertised defaults", () => {
  // The schema validates and advertises; the Collector applies the defaults it advertises.
  assert.equal(property("Mcp.Tools.ListTracesInput", "limit").default, 10);
  assert.equal(property("Mcp.Tools.SearchLogsInput", "limit").default, 50);
  assert.equal(property("Mcp.Tools.AggregateTelemetryInput", "limit").default, 20);
  assert.equal(property("Mcp.Tools.TriageInput", "limit").default, 10);
  assert.equal(property("Mcp.Tools.GetFailureContextInput", "limit").default, 100);
  for (const status of ["open", "resolved", "ignored", "all"]) assert(valid("Mcp.Tools.TriageInput", { status }));
  assert(!valid("Mcp.Tools.TriageInput", { status: "closed" }));
  assert.equal(property("Mcp.Tools.GetTraceInput", "max_spans").default, 100);
  assert.equal(property("Mcp.Tools.GetFailureContextInput", "max_logs").default, 100);
  assert.equal(property("Mcp.Tools.CiLogInput", "service_prefix").default, "qyl-ci");
  assert(valid("Mcp.Tools.ListTracesInput", {}));
  assert(!valid("Mcp.Tools.ListTracesInput", { limit: 1.5 }));
  assert(valid("Mcp.Tools.ListTracesInput", {
    start_time: "2026-10-11T00:00:00Z",
    end_time: "2026-10-11T01:00:00Z",
    service_name: "checkout-api",
    errors_only: true,
    cursor: "opaque",
  }));
  assert(!valid("Mcp.Tools.ListTracesInput", { start_time: "2026-10-11" }), "RFC 3339 only");
  assert(!valid("Mcp.Tools.ListTracesInput", { errors_only: "true" }));
  assert(!valid("Mcp.Tools.ListTracesInput", { invented: true }), "inputs are sealed");

  const traceId = "0".repeat(32);
  assert(valid("Mcp.Tools.GetTraceInput", { trace_id: traceId }));
  assert(!valid("Mcp.Tools.GetTraceInput", {}));
  assert(!valid("Mcp.Tools.GetTraceInput", { trace_id: "short" }));
  for (const max_spans of [0, 1001, 1.5]) assert(!valid("Mcp.Tools.GetTraceInput", { trace_id: traceId, max_spans }));
  for (const max_spans of [1, 1000]) assert(valid("Mcp.Tools.GetTraceInput", { trace_id: traceId, max_spans }));
  assert(!valid("Mcp.Tools.GetTraceInput", { trace_id: traceId, errors_only: "true" }));
  assert(!valid("Mcp.Tools.GetTraceInput", { trace_id: traceId, include_attributes: "false" }));
  assert(!valid("Mcp.Tools.CiLogInput", { service_prefix: "" }));
  assert(!valid("Mcp.Tools.CiLogInput", { service_prefix: "x".repeat(257) }));
  assert(valid("Mcp.Tools.CiLogInput", { service_prefix: "x".repeat(256) }));

  assert(!valid("Mcp.Tools.ListSessionsInput", { limit: 101 }));
  assert(valid("Mcp.Tools.ListSessionsInput", { start_time: "2026-10-11T00:00:00Z", cursor: "opaque" }));
  assert(!valid("Mcp.Tools.SearchLogsInput", { severity_min: 0 }));
  assert(!valid("Mcp.Tools.SearchLogsInput", { severity_min: 25 }));
  assert(!valid("Mcp.Tools.SearchLogsInput", { limit: 201 }));
  assert(valid("Mcp.Tools.SearchLogsInput", {
    session_id: "sess-demo-genai-01",
    start_time: "2026-10-11T00:00:00Z",
    end_time: "2026-10-11T01:00:00Z",
    cursor: "opaque",
  }));
  // A validated call is forwarded as given: the validator inserts nothing.
  const args = {};
  assert.deepEqual(checkContract("Mcp.Tools.ListTracesInput", args), { valid: true, value: {} });
});

test("discovered attribute values keep int64 precision in the lossless attribute form", () => {
  const discovered = (value: unknown) => ({ ...readContext(1), has_more: false, attributes: [{ key: "probe.number", scope: "span", type: "int",
    coverage_share: 1, cardinality: 1, top_values: [{ value, count: 1, share: 1 }], semconv: null }] });
  assert(valid("Mcp.Tools.DiscoverAttributesOutput", discovered({ type: "int", value: "9007199254740993" })));
  assert(valid("Mcp.Tools.DiscoverAttributesOutput", discovered({ type: "double", value: 0.5 })));
  assert(valid("Mcp.Tools.DiscoverAttributesOutput", discovered("text")));
  assert(!valid("Mcp.Tools.DiscoverAttributesOutput", discovered({ type: "int", value: 42 })), "an int64 travels as a string");
});

test("get_failure_context says that exactly one target selects the failure", () => {
  const rule = /exactly one of trace_id, session_id, failure_id or ci_run_id/u;
  assert.match(String((documents["Mcp.Tools.GetFailureContextInput"] as unknown as { description?: string }).description), rule);
  for (const key of ["trace_id", "session_id", "failure_id", "ci_run_id"])
    assert.match(String(property("Mcp.Tools.GetFailureContextInput", key).description), rule, key);
});

test("the server's error codes are the contract's ToolErrorCode enum", () => {
  for (const error of [
    new CollectorError("bad input"),
    new CollectorError("gone", false, 404),
    new CollectorError("down", true),
    new CollectorError("aggregation: must be one of: avg, p95", false, 400, undefined,
      "https://qyl.at/problems/validation-failed",
      [{ field: "aggregation", message: "must be one of: avg, p95", code: "aggregation.invalid",
        allowed: ["avg", "p95"], hint: "Send one of the allowed values." }]),
    new CollectorError("Trace 0af7651916cd43dd8448eb211c80319c was not found in this project; list_traces lists the traces.", false, 404,
      undefined, "https://qyl.at/problems/not-found", undefined, { resource_type: "trace", resource_id: "0af7651916cd43dd8448eb211c80319c" }),
    new Error("Authorization: Bearer should-never-escape"),
  ]) {
    const structured = toolError("get_trace", error).structuredContent as { error: Record<string, unknown> };
    // Every member, field, allowed and hint included, is the contract's ToolError, and each names a next step.
    assert(valid("Mcp.Tools.ToolError", structured.error), String(error));
    assert.equal(typeof structured.error.hint, "string", String(error));
  }
});

test("generated output documents accept the test telemetry fixture", () => {
  const fixture = getTelemetryFixture();
  const [trace] = fixture.traces;
  const summaries = fixture.traces.map(toolSummary);
  const context = readContext(fixture.traces.length);

  assert(valid("Mcp.Tools.ListTracesOutput", { ...context, traces: summaries, has_more: false }));
  assert(
    !valid("Mcp.Tools.ListTracesOutput", { ...readContext(fixture.traces.length, false), traces: summaries }),
    "has_more is required: a page must say whether it is the whole result",
  );
  assert(valid("Mcp.Tools.ListTracesOutput", { ...context, traces: summaries, has_more: true, next_cursor: "c" }));
  assert(valid("Mcp.Tools.GetTraceOutput", { ...context, trace: toolTrace(trace!) }));
  assert(valid("Mcp.Tools.ListSessionsOutput", { ...context, sessions: fixture.sessions, has_more: false }));
  assert(valid("Mcp.Tools.SearchLogsOutput", { ...context, logs: fixture.logs.map(toolLog), has_more: false }));
});

test("raw metric points carry RFC 3339 times and int64 values as exact strings", () => {
  const output = (time: unknown, value: unknown) => ({
    ...readContext(1), name: "queue.depth", kind: "gauge", series_id: "s", attributes: [], has_more: false,
    points: [{ time, has_conflict: false, data: { type: "number", value: { type: "int", value } } }],
  });
  // Past Number.MAX_SAFE_INTEGER: as a JS number literal this value silently becomes ...992, which is
  // the precision loss the decimal-string encoding exists to remove.
  const parsed = checkContract("Mcp.Tools.QueryMetricOutput", output("2026-10-12T09:30:00.1234567Z", "9007199254740993"));
  assert(parsed.valid, parsed.valid ? "" : parsed.message);
  assert.deepEqual(parsed.value.points![0]!.data, { type: "number", value: { type: "int", value: "9007199254740993" } });

  assert(!valid("Mcp.Tools.QueryMetricOutput", output("1742000000123456789", "1")), "times are RFC 3339, not unix nanoseconds");
  assert(!valid("Mcp.Tools.QueryMetricOutput", output("2026-10-12T09:30:00Z", 42)), "an int64 travels as a string");
});

test("TypeSpec record dictionaries retain their value contract at runtime", () => {
  const trace = toolTrace(structuredClone(getTelemetryFixture().traces[0]!));
  const span = trace.spans[0]!;
  span.attributes = [
    {
      key: "nested",
      value: {
        type: "kvlist",
        values: {
          message: "Grüße",
          retryable: true,
          attempts: { type: "int", value: "2" },
        },
      },
    },
  ];
  const output = () => ({ ...readContext(1), trace: { ...trace, spans: [span] } });
  assert(valid("Mcp.Tools.GetTraceOutput", output()));
  span.attributes[0]!.value = { unsupported: null } as never;
  assert(!valid("Mcp.Tools.GetTraceOutput", output()));
});

test("analysis boundary preserves RFC 3339 precision and rejects alternate wire encodings", () => {
  const trace = toolSummary(structuredClone(getTelemetryFixture().traces[0]!));
  trace.start_time = "2026-07-28T12:00:00.123456789+02:00";
  const body = { ...readContext(1), traces: [trace] };
  const parse = (value: unknown) => parseCollectorBody(value, "/api/v1/analysis/list_traces", "Mcp.Tools.ListTracesOutput");
  assert.equal(parse(body).traces[0]?.start_time, trace.start_time);
  assert.throws(() => parse([trace]), /collector contract mismatch/u);
  assert.throws(() => parse({ ...body, has_more: undefined }), /has_more/u);
  assert.throws(() => parse({ ...body, invented: true }), /invented: is not accepted here; accepted: window, coverage, page_totals, traces/u);
  trace.root = { service: 1 } as never;
  assert.throws(() => parse(body), /collector contract mismatch/u);
});

test("contract date-time fields accept RFC 3339 and reject missing seconds or offsets", () => {
  const trace = toolSummary(structuredClone(getTelemetryFixture().traces[0]!));
  const cases = [
    ["2026-07-28T12:00:00Z", true],
    ["2026-07-28T12:00:00+02:00", true],
    ["2026-07-28T12:00:00.123456789Z", true],
    ["2026-07-28T12:00Z", false],
    ["2026-07-28T12:00:00", false],
    ["2026-02-30T12:00:00Z", false],
  ] as const;

  for (const [value, accepted] of cases) {
    assert.equal(
      valid("Mcp.Tools.ListTracesOutput", { ...readContext(1), traces: [{ ...trace, start_time: value }] }),
      accepted,
      `start_time ${value} should be ${accepted ? "accepted" : "rejected"}`,
    );
  }
});

test("only generated names resolve to documents", () => {
  // `in` answers true for every Object.prototype key; none of them is a document.
  for (const inherited of ["toString", "constructor", "__proto__", "hasOwnProperty"]) {
    assert.throws(
      () => contractSchema(inherited as ContractSchemaName),
      new RegExp(`has no '${inherited}' document`, "u"),
      `${inherited} must not resolve to a contract document`,
    );
  }
});
