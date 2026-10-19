import assert from "node:assert/strict";
import test, { type TestContext } from "node:test";
import { checkContract } from "./contracts/tool-schemas.js";
import { getTelemetryFixture } from "./telemetry-fixtures.test-helper.js";
import { connectModernTestClient, type ModernTestClient } from "./modern-test-client.test-helper.js";
import { createServer } from "./server.js";
import { readContext, toolTrace } from "./analysis-context.test-helper.js";
import type { QylSpan, QylTrace } from "./wire.js";

function fixtureTrace(): QylTrace {
  const trace = structuredClone(getTelemetryFixture().traces[0]!);
  const base = trace.spans[0]!;
  const services = ["qyl-ci-build", "custom-ci-build", "custom-ci-test", "web"];
  trace.spans = ([1, 2, 2, 0] as const).map((code, index): QylSpan => ({
    ...structuredClone(base),
    span_id: (index + 1).toString(16).padStart(16, "0") as QylSpan["span_id"],
    name: `phase-${index}`,
    status: { code },
    attributes: [{ key: "payload", value: "span-payload" }, { key: "cicd.pipeline.task.name", value: `leg-${index}` }],
    resource: { service_name: services[index]!, attributes: [{ key: "payload", value: "resource-payload" }] },
    events: [{
      name: "event-kept", time_unix_nano: base.start_time_unix_nano,
      attributes: [{ key: "payload", value: "event-payload" }],
    }],
    links: [{
      trace_id: trace.trace_id, span_id: base.span_id,
      attributes: [{ key: "payload", value: "link-payload" }],
    }],
    instrumentation_scope: {
      name: "scope-kept", attributes: [{ key: "payload", value: "scope-payload" }],
    },
  }));
  trace.root_span = structuredClone(trace.spans[0]!);
  trace.span_count = trace.spans.length;
  trace.services = services;
  trace.has_error = true;
  return trace;
}

async function connectCollector(
  context: TestContext,
  trace: QylTrace,
  failureStatus?: number,
): Promise<ModernTestClient> {
  const env = {
    QYL_COLLECTOR_URL: "https://collector.example.test",
    MCP_COLLECTOR_PROJECTS: undefined,
  };
  const previous = Object.fromEntries(Object.keys(env).map((key) => [key, process.env[key]]));
  for (const [key, value] of Object.entries(env)) {
    if (value === undefined) delete process.env[key];
    else process.env[key] = value;
  }
  context.after(() => {
    for (const [key, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  });
  context.mock.method(globalThis, "fetch", async (input: string | Request | URL, init?: RequestInit) => {
    const url = new URL(new Request(input, init).url);
    assert.equal(url.origin, "https://collector.example.test");
    if (failureStatus !== undefined) return Response.json({ error: "fixture failure" }, { status: failureStatus });
    const body = await new Request(input, init).json() as Record<string, unknown>;
    if (url.pathname === "/api/v1/analysis/get_trace") {
      // Projection now belongs to the collector. Verify the transport forwards
      // every option unchanged and preserves the collector's typed result.
      assert.equal(body.trace_id, trace.trace_id);
      return Response.json({ ...readContext(trace.span_count), trace: toolTrace(trace) });
    }
    if (url.pathname === "/api/v1/analysis/ci_log") {
      return Response.json({ ...readContext(), has_more: false, runs: [] });
    }
    return Response.json({ error: "not found" }, { status: 404 });
  });
  const connection = await connectModernTestClient(
    { name: "tool-options-test", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());
  return connection;
}

async function readTrace(connection: ModernTestClient, args: Record<string, unknown>) {
  const result = await connection.client.callTool({ name: "get_trace", arguments: args });
  assert.equal(result.isError, undefined, JSON.stringify(result));
  const checked = checkContract("Mcp.Tools.GetTraceOutput", result.structuredContent);
  assert(checked.valid, checked.valid ? "" : checked.message);
  return { result, output: checked.value };
}

test("get_trace preserves complete typed collector results and forwards optional selection settings", async (context) => {
  const trace = fixtureTrace();
  const forwarded: Record<string, unknown>[] = [];
  const connection = await connectCollector(context, trace);
  const transport = globalThis.fetch;
  context.mock.method(globalThis, "fetch", async (input: string | Request | URL, init?: RequestInit) => {
    const body = await new Request(input, init).json() as Record<string, unknown>;
    forwarded.push(body);
    return transport(input, init);
  });
  for (const options of [
    {},
    { selection: "all", include_attributes: true },
    { selection: "errors", max_spans: 1, include_attributes: false },
    { include_attributes: false, max_spans: 1 },
    { selection: "errors_with_ancestors", max_spans: 10 },
    { selection: "critical_path" },
  ]) {
    const { output } = await readTrace(connection, { trace_id: trace.trace_id, ...options });
    assert.deepEqual(output.trace, toolTrace(trace));
    const input = forwarded.at(-1)!;
    for (const [key, value] of Object.entries(options)) assert.deepEqual(input[key], value, key);
  }
});

test("ci_log forwards case-sensitive service prefix, limits and run comparison without local filtering", async (context) => {
  const forwarded: Record<string, unknown>[] = [];
  const connection = await connectCollector(context, fixtureTrace());
  const transport = globalThis.fetch;
  context.mock.method(globalThis, "fetch", async (input: string | Request | URL, init?: RequestInit) => {
    forwarded.push(await new Request(input, init).json() as Record<string, unknown>);
    return transport(input, init);
  });
  for (const args of [{}, { service_prefix: "custom-ci", limit: 1 }, { ci_run_id: "custom-run", service_prefix: "CUSTOM-ci", baseline: "last_success" }, { ci_run_id: "custom-run", baseline: { ci_run_id: "prior-run" }, errors_only: true }]) {
    const result = await connection.client.callTool({ name: "ci_log", arguments: args });
    assert.equal(result.isError, undefined, JSON.stringify(result));
    assert(checkContract("Mcp.Tools.CiLogOutput", result.structuredContent).valid);
    for (const [key, value] of Object.entries(args)) assert.deepEqual(forwarded.at(-1)![key], value);
  }
});

test("filtered tools preserve upstream errors instead of manufacturing empty or demo results", async (context) => {
  const trace = fixtureTrace();
  const connection = await connectCollector(context, trace, 503);
  for (const call of [
    { name: "get_trace", arguments: { trace_id: trace.trace_id, selection: "errors", max_spans: 1 } },
    { name: "ci_log", arguments: { service_prefix: "custom-ci" } },
  ]) {
    const result = await connection.client.callTool(call);
    assert.equal(result.isError, true);
    // The failure travels as `structuredContent.error`; no empty or demo data is manufactured.
    const structured = result.structuredContent as { error?: { code: string } } | undefined;
    assert.deepEqual(Object.keys(structured ?? {}), ["error"], call.name);
    assert.equal(typeof structured?.error?.code, "string", call.name);
  }
});
