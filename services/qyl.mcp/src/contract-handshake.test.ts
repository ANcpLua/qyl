import assert from "node:assert/strict";
import test, { type TestContext } from "node:test";
import {
  collectorContract,
  observeCollectorRevision,
  readCollectorRevision,
  startCollectorContractMonitor,
} from "./contract-handshake.js";
import { CONTRACT_REVISION } from "./contracts/generated/api.js";
import { checkContract } from "./contracts/tool-schemas.js";
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { readContext } from "./analysis-context.test-helper.js";
import { createServer } from "./server.js";
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

test("a health read proves a match, a mismatch, a pre-revision collector, or nothing", async (context) => {
  collectorAt(context, "http://collector.test");
  const bodies: Array<[Response | Error, string | null | undefined]> = [
    [Response.json({ status: "Healthy", contract_revision: CONTRACT_REVISION }), CONTRACT_REVISION],
    [Response.json({ status: "Unhealthy", contract_revision: foreign }, { status: 503 }), foreign],
    [Response.json({ status: "Healthy" }), null],
    [Response.json({ status: "Unhealthy" }, { status: 503 }), undefined],
    [new Response("<html>bad gateway</html>", { status: 502 }), undefined],
    [new Error("connect ECONNREFUSED"), undefined],
  ];
  for (const [answer, expected] of bodies) {
    context.mock.method(globalThis, "fetch", async (input: URL) => {
      assert.equal(input.pathname, "/health");
      if (answer instanceof Error) throw answer;
      return answer.clone();
    });
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
  for (const name of ["list_metrics", "create_annotation", "get_trace"]) {
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
