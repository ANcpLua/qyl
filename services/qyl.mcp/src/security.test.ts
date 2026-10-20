import assert from "node:assert/strict";
import { createServer, type Server } from "node:http";
import test from "node:test";
import type { CallToolResult } from "@modelcontextprotocol/server";
import { CollectorError, collectorPost } from "./collector.js";
import { collectorUrl } from "./config.js";
import { toolError } from "./request-scope.js";

test("collector URL rejects embedded credentials and query secrets", () => {
  const previous = process.env.QYL_COLLECTOR_URL;
  try {
    process.env.QYL_COLLECTOR_URL = "https://user:password@example.test/api?token=secret";
    assert.throws(collectorUrl, /credential-free/u);
  } finally {
    restoreEnvironment("QYL_COLLECTOR_URL", previous);
  }
});

test("tool errors expose only controlled collector failures", () => {
  const unreachable = { code: "collector_unreachable", message: "collector unavailable", retryable: true, hint: "Retry the same call later." };
  assert.deepEqual(toolError("list_traces", new CollectorError("collector unavailable", true)), {
    content: [
      { type: "text", text: "list_traces failed: collector_unreachable — collector unavailable → Retry the same call later." },
      { type: "text", text: JSON.stringify({ error: unreachable }) },
    ],
    structuredContent: { error: unreachable },
    isError: true,
  });
  const internal = { code: "internal", message: "Telemetry request failed.", retryable: false, hint: "Tell the qyl operator; repeating the call will not help." };
  const result = toolError("get_trace", new Error("Authorization: Bearer should-never-escape"));
  assert.deepEqual(result.structuredContent, { error: internal });
  assert.doesNotMatch(JSON.stringify(result), /should-never-escape/u);
});

test("collector Problem Details cannot inject remote detail into tool-facing errors", async () => {
  const listener = createServer((_request, response) => {
    response.writeHead(503, { "content-type": "application/problem+json" });
    response.end(JSON.stringify({
      type: "https://qyl.at/problems/service-unavailable",
      code: "service_unavailable",
      title: "Unavailable",
      status: 503,
      detail: "Authorization: Bearer remote-secret",
    }));
  });
  await listen(listener);
  const address = listener.address();
  assert(address && typeof address === "object");
  const previous = process.env.QYL_COLLECTOR_URL;
  process.env.QYL_COLLECTOR_URL = `http://127.0.0.1:${address.port}`;
  try {
    await assert.rejects(
      collectorPost("/failure", {}),
      (error: unknown) => error instanceof CollectorError
        && error.status === 503
        && !error.message.includes("remote-secret"),
    );
  } finally {
    restoreEnvironment("QYL_COLLECTOR_URL", previous);
    await close(listener);
  }
});

test("transient answers without a contract body keep their retryable status code", async () => {
  const cases = [
    { status: 502, type: "text/html", body: "<html>bad gateway</html>", code: "collector_error", retryable: true },
    { status: 429, type: undefined, body: "", code: "collector_rate_limited", retryable: true },
    { status: 400, type: "text/plain", body: "nope", code: "contract_mismatch", retryable: false },
  ];
  for (const expected of cases) {
    const listener = createServer((_request, response) => {
      response.writeHead(expected.status, expected.type === undefined ? {} : { "content-type": expected.type });
      response.end(expected.body);
    });
    await listen(listener);
    const address = listener.address();
    assert(address && typeof address === "object");
    const previous = process.env.QYL_COLLECTOR_URL;
    process.env.QYL_COLLECTOR_URL = `http://127.0.0.1:${address.port}`;
    try {
      await assert.rejects(collectorPost("/failure", {}), (error: unknown) => {
        assert(error instanceof CollectorError);
        assert.equal(error.code, expected.code, `status ${expected.status}`);
        const structured = toolError("list_traces", error).structuredContent as { error: { retryable: boolean } };
        assert.equal(structured.error.retryable, expected.retryable, `status ${expected.status}`);
        return true;
      });
    } finally {
      restoreEnvironment("QYL_COLLECTOR_URL", previous);
      await close(listener);
    }
  }
});

test("collector requests fail closed on the configured timeout", async () => {
  const listener = createServer((_request, response) => {
    setTimeout(() => {
      if (!response.destroyed) response.end("{}");
    }, 100);
  });
  await listen(listener);
  const address = listener.address();
  assert(address && typeof address === "object");
  const previous = process.env.QYL_COLLECTOR_URL;
  process.env.QYL_COLLECTOR_URL = `http://127.0.0.1:${address.port}`;
  try {
    await assert.rejects(
      collectorPost("/slow", {}, { timeoutMs: 20 }),
      (error: unknown) => error instanceof CollectorError
        && error.connectionError
        && error.message === "collector timed out for /slow",
    );
  } finally {
    restoreEnvironment("QYL_COLLECTOR_URL", previous);
    await close(listener);
  }
});

/** The tool error a Collector answering `status` with the Problem Details `body` produces for `tool`. */
async function refusedWith(tool: string, status: number, body: object): Promise<CallToolResult> {
  const listener = createServer((_request, response) => {
    response.writeHead(status, { "content-type": "application/problem+json" });
    response.end(JSON.stringify(body));
  });
  await listen(listener);
  const address = listener.address();
  assert(address && typeof address === "object");
  const previous = process.env.QYL_COLLECTOR_URL;
  process.env.QYL_COLLECTOR_URL = `http://127.0.0.1:${address.port}`;
  try {
    const error = await collectorPost(`/api/v1/analysis/${tool}`, {}).then(() => undefined, (failure: unknown) => failure);
    assert(error instanceof CollectorError, String(error));
    return toolError(tool, error);
  } finally {
    restoreEnvironment("QYL_COLLECTOR_URL", previous);
    await close(listener);
  }
}

test("a Collector 404 reaches the model with what is missing and where valid ids come from", async () => {
  const id = "0000000000000000000000000000dead";
  const result = await refusedWith("get_trace", 404, {
    type: "https://qyl.at/problems/not-found", title: "Not Found", status: 404, code: "not_found",
    detail: `Trace ${id} was not found in this project; list_traces lists the traces. Authorization: Bearer remote-secret`,
    resource_type: "trace", resource_id: id,
  });
  const { error } = result.structuredContent as { error: Record<string, unknown> };
  assert.equal(error.code, "not_found");
  assert.equal(error.resource_type, "trace");
  assert.equal(error.resource_id, id);
  assert.match(String(error.message), new RegExp(`^Trace ${id} was not found in this project; list_traces lists the traces\\.`, "u"));
  assert.match(String(error.hint), /list_traces returns the trace ids/u);
  assert.doesNotMatch(JSON.stringify(result), /remote-secret/u, "a forwarded detail is redacted like telemetry");
  const [headline] = result.content as Array<{ text: string }>;
  assert.match(headline!.text, new RegExp(`^get_trace failed: not_found — Trace ${id} was not found in this project; list_traces lists the traces\\.`, "u"));
});

test("a Collector validation error reaches the model with the field, the values it accepts and its hint", async () => {
  const series = ["series-a", "series-b"];
  const operators = ["eq", "neq", "in", "prefix", "contains", "exists", "gt", "gte", "lt", "lte"];
  const ambiguous = "2 series of queue.depth match; raw reads one: pass one of their series_id values, or matchers that select one series.";
  const result = await refusedWith("query_metric", 400, {
    type: "https://qyl.at/problems/validation-failed", title: "Validation Failed", status: 400, code: "validation_failed",
    errors: [
      { field: "series_id", message: ambiguous, code: "series_id.ambiguous", allowed: series, hint: "query_metric with group_by shows how the series differ." },
      { field: "matchers[0].op", message: `must be one of: ${operators.join(", ")}`, code: "matchers[].op.invalid", allowed: operators,
        hint: "Send one of the allowed values." },
    ],
  });
  const { error } = result.structuredContent as { error: Record<string, unknown> };
  assert.equal(error.code, "collector_rejected");
  assert.equal(error.field, "series_id");
  assert.deepEqual(error.allowed, series);
  assert.equal(error.hint, "query_metric with group_by shows how the series differ.", "the Collector's hint is the one to follow");
  assert.equal((error.fields as unknown[]).length, 2, "every rejected field stays available");
  // A rule that does not name the accepted values gets them appended; one that does is left alone.
  assert.equal(error.message, `series_id: ${ambiguous} Accepted: ${series.join(", ")}; matchers[0].op: must be one of: ${operators.join(", ")}`);
  const [headline] = result.content as Array<{ text: string }>;
  assert.match(headline!.text, /^query_metric failed: collector_rejected — series_id: .* → query_metric with group_by shows how the series differ\.$/u);
});

function restoreEnvironment(name: string, value: string | undefined): void {
  if (value === undefined) delete process.env[name];
  else process.env[name] = value;
}

function listen(server: Server): Promise<void> {
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
}

function close(server: Server): Promise<void> {
  return new Promise((resolve, reject) => {
    server.close((error) => error ? reject(error) : resolve());
  });
}
