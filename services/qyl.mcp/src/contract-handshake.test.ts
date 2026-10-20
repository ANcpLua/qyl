import assert from "node:assert/strict";
import { createServer as createHttpServer, type Server } from "node:http";
import test, { type TestContext } from "node:test";
import { SpanStatusCode } from "@opentelemetry/api";
import { BasicTracerProvider, InMemorySpanExporter, SimpleSpanProcessor } from "@opentelemetry/sdk-trace-base";
import {
  collectorContract,
  collectorUnavailable,
  observeCollectorRevision,
  observeCollectorUnreachable,
  readCollectorRevision,
  startCollectorContractMonitor,
} from "./contract-handshake.js";
import { CONTRACT_REVISION } from "./contracts/generated/api.js";
import { checkContract } from "./contracts/tool-schemas.js";
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { readContext } from "./analysis-context.test-helper.js";
import { NativeExecutionRuntime } from "./native-execution.js";
import { SecretRedactor } from "./secret-redactor.js";
import { createServer } from "./server.js";
import { McpTelemetry } from "./telemetry.js";
import { createFetch, createHostedHandler } from "./main.js";

const foreign = "sha256:aaaabbbbccccdddd";

function collectorAt(context: TestContext, url: string): void {
  const prior = process.env.QYL_COLLECTOR_URL;
  const projects = process.env.MCP_COLLECTOR_PROJECTS;
  process.env.QYL_COLLECTOR_URL = url;
  delete process.env.MCP_COLLECTOR_PROJECTS;
  context.after(() => {
    if (prior === undefined) delete process.env.QYL_COLLECTOR_URL;
    else process.env.QYL_COLLECTOR_URL = prior;
    if (projects !== undefined) process.env.MCP_COLLECTOR_PROJECTS = projects;
  });
}

/** Health reads the test decides; the state is forgotten after the test. */
function monitor(context: TestContext): { advertise(revision: string | null | undefined): void } {
  context.after(() => observeCollectorRevision(undefined));
  return { advertise: (revision) => void observeCollectorRevision(revision) };
}

test("the generated revision is present and well formed", () => {
  assert.match(CONTRACT_REVISION, /^sha256:[a-f0-9]{16}$/u);
});

test("a health read proves a match, a mismatch, a pre-revision collector, or nothing, and one without an answer rejects", async (context) => {
  collectorAt(context, "http://collector.test");
  const bodies: Array<[Response, string | null | undefined]> = [
    [Response.json({ status: "Healthy", contract_revision: CONTRACT_REVISION }), CONTRACT_REVISION],
    [Response.json({ status: "Unhealthy", contract_revision: foreign }, { status: 503 }), foreign],
    [Response.json({ status: "Healthy" }), null],
    [Response.json({ status: "Unhealthy" }, { status: 503 }), undefined],
    [new Response("<html>bad gateway</html>", { status: 502 }), undefined],
  ];
  // One mock for the whole test: a mock of a mock is restored to the inner mock, not to fetch.
  let answer: Response | Error = new TypeError("connect ECONNREFUSED");
  context.mock.method(globalThis, "fetch", async (input: URL) => {
    assert.equal(input.pathname, "/health");
    if (answer instanceof Error) throw answer;
    return answer.clone();
  });
  await assert.rejects(readCollectorRevision(), /ECONNREFUSED/u);
  for (const [body, expected] of bodies) {
    answer = body;
    assert.equal(await readCollectorRevision(), expected);
  }
});

test("skew blocks every tool with both revisions and clears on the next matching read", async (context) => {
  collectorAt(context, "http://collector.test");
  const collector = monitor(context);
  const analysisCalls: string[] = [];
  context.mock.method(globalThis, "fetch", async (input: string | URL | Request) => {
    analysisCalls.push(new URL(String(input instanceof Request ? input.url : input)).pathname);
    return Response.json({ ...readContext(), items: [], has_more: false });
  });
  const connection = await connectModernTestClient(
    { name: "contract-skew-test", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());

  collector.advertise(foreign);
  assert.deepEqual(collectorContract(), { kind: "mismatch", revision: foreign });
  for (const name of ["list_metrics", "annotate", "get_trace"]) {
    const result = await connection.client.callTool({ name, arguments: name === "list_metrics" ? {} : name === "get_trace"
      ? { trace_id: "0".repeat(32) }
      : { target: { kind: "trace", trace_id: "0".repeat(32) }, text: "note" } });
    assert.equal(result.isError, true, name);
    const error = (result.structuredContent as { error: Record<string, unknown> }).error;
    assert.equal(error.code, "collector_contract_mismatch", name);
    assert.equal(error.retryable, true);
    assert.equal(error.collector_contract_revision, foreign);
    assert.equal(error.server_contract_revision, CONTRACT_REVISION);
    assert.match(String(error.message), new RegExp(`${foreign}.*${CONTRACT_REVISION}`, "u"));
    assert(checkContract("Mcp.Tools.ToolError", error).valid, "the error is the contract's ToolError");
    assert.match(String(error.hint), /one contract revision/u);
  }
  assert.deepEqual(analysisCalls, [], "a refused call never reaches the Collector");

  collector.advertise(null);
  assert.deepEqual(collectorContract(), { kind: "mismatch", revision: null });

  collector.advertise(CONTRACT_REVISION);
  const recovered = await connection.client.callTool({ name: "list_metrics", arguments: {} });
  assert.equal(recovered.isError, undefined, JSON.stringify(recovered));
  assert.deepEqual(analysisCalls, ["/api/v1/analysis/list_metrics"]);

  collector.advertise(undefined);
  assert.deepEqual(collectorContract(), { kind: "unknown" }, "an unanswered read proves nothing");
});

test("/healthz stays 200 and reports degraded with both revisions while they differ", async (context) => {
  const collector = monitor(context);
  const handler = createHostedHandler(() => createServer({ nativeExecution: false }), () => {});
  context.after(() => handler.close());
  const endpoint = createFetch({ handler });
  const health = async () => {
    const response = await endpoint(new Request("http://127.0.0.1:3001/healthz", { headers: { host: "127.0.0.1:3001" } }));
    assert.equal(response.status, 200);
    return response.json() as Promise<Record<string, unknown>>;
  };

  assert.deepEqual(await health(), { status: "ok", contract_revision: CONTRACT_REVISION });
  collector.advertise(foreign);
  assert.deepEqual(await health(), { status: "degraded", contract_revision: CONTRACT_REVISION, collector_contract_revision: foreign });
  observeCollectorUnreachable();
  assert.deepEqual(await health(), { status: "degraded", contract_revision: CONTRACT_REVISION }, "an unanswered Collector names no revision");
  collector.advertise(CONTRACT_REVISION);
  assert.deepEqual(await health(), { status: "ok", contract_revision: CONTRACT_REVISION, collector_contract_revision: CONTRACT_REVISION });
});

test("the monitor reads at start and on its interval, never twice at once", async (context) => {
  let reads = 0;
  let release: (() => void) | undefined;
  const stop = startCollectorContractMonitor({
    read: () => {
      reads += 1;
      return new Promise((resolve) => { release = () => resolve(CONTRACT_REVISION); });
    },
    intervalMs: 5,
  });
  context.after(stop);
  await new Promise((resolve) => setTimeout(resolve, 40));
  assert.equal(reads, 1, "a slow read is not overlapped by the next tick");
  release?.();
  await new Promise((resolve) => setTimeout(resolve, 40));
  assert(reads >= 2);
  assert.deepEqual(collectorContract(), { kind: "match", revision: CONTRACT_REVISION });
  stop();
  assert.deepEqual(collectorContract(), { kind: "unknown" });
});

test("while the Collector gives no answer the monitor reads again at the short interval, then at its own", async (context) => {
  const answers: Array<string | Error> = [new TypeError("fetch failed"), new TypeError("fetch failed"), CONTRACT_REVISION];
  const seen: string[] = [];
  const stop = startCollectorContractMonitor({
    read: async () => {
      seen.push(collectorContract().kind);
      const answer = answers[Math.min(seen.length - 1, answers.length - 1)]!;
      if (answer instanceof Error) throw answer;
      return answer;
    },
    intervalMs: 60_000,
    unreachableIntervalMs: 5,
  });
  context.after(stop);
  await until(() => collectorContract().kind === "match");
  assert.deepEqual(seen, ["unknown", "unreachable", "unreachable"], "each unanswered read is followed soon by the next");
  await new Promise((resolve) => setTimeout(resolve, 50));
  assert.equal(seen.length, 3, "an answered read waits the monitor's own interval");
});

test("the retry hint grows with the outage from 5 to 60 seconds", (context) => {
  context.after(() => observeCollectorRevision(undefined));
  const since = Date.parse("2026-10-20T01:28:30Z");
  observeCollectorUnreachable(since);
  assert.equal(collectorUnavailable(false, since + 900).retryAfterSeconds, 5);
  assert.equal(collectorUnavailable(false, since + 21_400).retryAfterSeconds, 22);
  assert.equal(collectorUnavailable(true, since + 600_000).retryAfterSeconds, 60);
});

/**
 * A Railway deploy restarts the volume-backed Collector while this server stays up: the old
 * container stops answering, its address still resolves for a while, then nothing listens, then
 * the new one answers. A call waiting on the old one is abandoned once the monitor's read finds
 * no answer, a call while it is out is answered at once without reaching it, both as server
 * errors, and the first answered read serves calls again.
 */
test("a Collector that stops answering is reported at once with collector_unavailable and served again once /health answers", async (context) => {
  let answering = true;
  const analysis: string[] = [];
  const collector = createHttpServer((request, response) => {
    if (!answering) return;
    if (request.url === "/health") {
      response.writeHead(200, { "content-type": "application/json" })
        .end(JSON.stringify({ status: "Healthy", contract_revision: CONTRACT_REVISION }));
      return;
    }
    analysis.push(request.url ?? "");
    response.writeHead(200, { "content-type": "application/json" })
      .end(JSON.stringify({ ...readContext(), items: [], has_more: false }));
  });
  const port = await listen(collector);
  context.after(() => closeServer(collector));
  collectorAt(context, `http://127.0.0.1:${port}`);

  const exporter = new InMemorySpanExporter();
  const provider = new BasicTracerProvider({ spanProcessors: [new SimpleSpanProcessor(exporter)] });
  context.after(() => provider.shutdown());
  const telemetry = new McpTelemetry({ QYL_MCP_TELEMETRY: "0" }, new SecretRedactor(), {
    tracer: provider.getTracer("collector-outage-test"),
    logger: { emit: () => {}, enabled: () => true },
  });
  const runtime = new NativeExecutionRuntime(undefined, { telemetry, redactor: new SecretRedactor() });
  const stop = startCollectorContractMonitor({ read: () => readCollectorRevision(200), intervalMs: 50, unreachableIntervalMs: 20 });
  context.after(stop);
  const connection = await connectModernTestClient(
    { name: "collector-outage-test", version: "1.0.0" },
    () => createServer({ nativeExecution: runtime, transport: "inproc" }),
  );
  context.after(() => connection.close());
  const listMetrics = async () => {
    const started = performance.now();
    const result = await connection.client.callTool({ name: "list_metrics", arguments: {} });
    return { result, ms: performance.now() - started, error: (result.structuredContent as { error?: Record<string, unknown> }).error };
  };
  await until(() => collectorContract().kind === "match");
  assert.equal((await listMetrics()).result.isError, undefined, "served while the Collector answers");

  answering = false;
  const abandoned = await listMetrics();
  assert(abandoned.ms < 5_000, `the waiting call ended after ${Math.round(abandoned.ms)} ms, not at its 55 s budget`);
  assert.equal(abandoned.result.isError, true);
  assert.equal(abandoned.error?.code, "collector_unavailable");
  assert.match(String(abandoned.error?.message), /abandoned unanswered/u);
  assert.equal(collectorContract().kind, "unreachable");

  await closeServer(collector);
  const reached = analysis.length;
  const refused = await listMetrics();
  assert.equal(refused.error?.code, "collector_unavailable");
  assert.match(String(refused.error?.message), /was not sent/u);
  assert.equal(analysis.length, reached, "a call while the Collector is out never reaches it");
  for (const { error } of [abandoned, refused]) {
    assert.equal(error?.retryable, true);
    assert.equal(error?.retry_after_seconds, 5);
    assert.equal(error?.hint, "Retry the same call in 5 seconds.");
    assert(checkContract("Mcp.Tools.ToolError", error).valid, "the error is the contract's ToolError");
  }

  answering = true;
  await listen(collector, port);
  await until(() => collectorContract().kind === "match");
  assert.equal((await listMetrics()).result.isError, undefined, "served from the first answered read");

  const spans = exporter.getFinishedSpans().filter((span) => span.name === "tools/call list_metrics");
  assert.deepEqual(spans.map((span) => span.status.code),
    [SpanStatusCode.UNSET, SpanStatusCode.ERROR, SpanStatusCode.ERROR, SpanStatusCode.UNSET]);
  assert.deepEqual(spans.map((span) => span.attributes["error.type"]), [undefined, "tool_error", "tool_error", undefined]);
});

async function until(condition: () => boolean, timeoutMs = 5_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (!condition()) {
    assert(Date.now() < deadline, "the condition did not hold in time");
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
}

async function listen(server: Server, port = 0): Promise<number> {
  await new Promise<void>((done) => server.listen(port, "127.0.0.1", done));
  const address = server.address();
  assert(address && typeof address !== "string");
  return address.port;
}

async function closeServer(server: Server): Promise<void> {
  if (!server.listening) return;
  server.closeAllConnections();
  await new Promise<void>((done, fail) => server.close((error) => error ? fail(error) : done()));
}
