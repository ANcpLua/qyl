import assert from "node:assert/strict";
import test from "node:test";
import { Client, StreamableHTTPClientTransport, type CallToolResult } from "@modelcontextprotocol/client";
import { createMcpHandler } from "@modelcontextprotocol/server";
import documents from "./contracts/generated/tool-schemas.json" with { type: "json" };
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { createServer, MAX_TOOL_INPUT_ELEMENTS } from "./server.js";

interface ErrorBody {
  error: {
    code: string;
    message: string;
    retryable: boolean;
    field?: string;
    allowed?: unknown[];
    hint?: string;
    fields?: Array<{ field: string; message: string; code: string }>;
  };
}

const cases = [
  {
    tool: "triage", arguments: { since: "2026-10-19T00:00:00Z" } as Record<string, unknown> | undefined,
    field: "since", keyword: "additionalProperties", allowedIncludes: ["lookback_minutes", "start_time", "baseline"],
    message: /since: is not accepted here; accepted: lookback_minutes, start_time, end_time/u,
  },
  {
    tool: "update_failure_status", arguments: { failure_id: "fail_1", etag: "\"1\"", status: "closed" },
    field: "status", keyword: "enum", allowed: ["open", "resolved", "ignored"],
    message: /status: must be one of: open, resolved, ignored/u,
  },
  {
    // A discriminator none of the union's branches accepts: the branches' other complaints are noise.
    tool: "create_saved_query", arguments: { name: "errors", filter: { signal: "trace" } },
    field: "filter.signal", keyword: "enum", allowed: ["traces", "logs", "metrics"],
    message: /^filter\.signal: must be one of: traces, logs, metrics$/u,
  },
  {
    // The discriminator matched one branch: only that branch's rule is reported.
    tool: "create_saved_query", arguments: { name: "errors", filter: { signal: "logs", errors_only: true } },
    field: "filter.errors_only", keyword: "additionalProperties", allowedIncludes: ["severity_min", "query"],
    message: /^filter\.errors_only: is not accepted here; accepted: matchers, signal, lookback_minutes, service_name, severity_min/u,
  },
  {
    tool: "get_trace", arguments: undefined,
    field: "trace_id", keyword: "required", message: /^trace_id: is required$/u,
  },
];

function errorOf(result: CallToolResult): ErrorBody["error"] {
  assert.equal(result.isError, true, JSON.stringify(result));
  const [headline, json] = result.content as Array<{ type: string; text: string }>;
  assert.equal(headline?.type, "text");
  assert.equal(json?.type, "text");
  assert.deepEqual(JSON.parse(json!.text), result.structuredContent, "content[1] is structuredContent as JSON");
  return (result.structuredContent as unknown as ErrorBody).error;
}

test("arguments the input schema rejects answer invalid_argument with the field, what it accepts and a hint", async (context) => {
  const fetch = context.mock.method(globalThis, "fetch", async () => {
    throw new Error("a rejected call must not reach the Collector");
  });
  const connection = await connectModernTestClient(
    { name: "invalid-arguments", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());

  for (const expected of cases) {
    const result = await connection.client.callTool({
      name: expected.tool, ...(expected.arguments === undefined ? {} : { arguments: expected.arguments }),
    }) as CallToolResult;
    const error = errorOf(result);
    assert.equal(error.code, "invalid_argument", expected.tool);
    assert.equal(error.retryable, false);
    assert.equal(error.field, expected.field, expected.tool);
    assert.match(error.message, expected.message, expected.tool);
    if (expected.allowed !== undefined) assert.deepEqual(error.allowed, expected.allowed, expected.tool);
    for (const name of expected.allowedIncludes ?? []) assert(error.allowed?.includes(name), `${expected.tool} accepts ${name}`);
    assert.equal(error.fields?.[0]?.field, expected.field);
    assert.equal(error.fields?.[0]?.code, expected.keyword);
    assert.match(error.hint ?? "", new RegExp(`^Correct ${expected.field.replace(".", "\\.")}.* and call ${expected.tool} again\\.$`, "u"));
    const headline = (result.content as Array<{ text: string }>)[0]!.text;
    assert.match(headline, new RegExp(`^${expected.tool} failed: invalid_argument — ${expected.field.replace(".", "\\.")}: `, "u"));
    assert.doesNotMatch(headline, /\n/u);
  }
  assert.equal(fetch.mock.callCount(), 0);
});

test("a 2025-era client gets the same structured invalid_argument", async (context) => {
  const handler = createMcpHandler(() => createServer({ nativeExecution: false }));
  context.after(() => handler.close());
  const client = new Client({ name: "invalid-arguments-legacy", version: "1.0.0" });
  context.after(() => client.close().catch(() => undefined));
  await client.connect(new StreamableHTTPClientTransport(new URL("http://qyl-legacy-test.invalid/mcp"), {
    fetch: (url, init) => handler.fetch(new Request(url, init)),
  }));
  assert.equal(client.getProtocolEra(), "legacy");
  const error = errorOf(await client.callTool({ name: "update_failure_status",
    arguments: { failure_id: "fail_1", etag: "\"1\"", status: "closed" } }) as CallToolResult);
  assert.equal(error.code, "invalid_argument");
  assert.equal(error.field, "status");
  assert.deepEqual(error.allowed, ["open", "resolved", "ignored"]);
});

test("maxToolInputElements caps a call above the largest arguments the contract accepts", async (context) => {
  // An upper bound of array elements plus object members for every valid instance of a schema.
  const bound = (schema: Record<string, unknown> | undefined): number => {
    if (schema === undefined) return 0;
    const branches = (schema.oneOf ?? schema.anyOf) as Array<Record<string, unknown>> | undefined;
    if (branches !== undefined) return Math.max(...branches.map(bound));
    if (schema.properties !== undefined) {
      return Object.values(schema.properties as Record<string, Record<string, unknown>>)
        .reduce((total, property) => total + 1 + bound(property), 0);
    }
    if (schema.items !== undefined) {
      // A list without maxItems holds scalars here; count a generous hundred of them.
      return (typeof schema.maxItems === "number" ? schema.maxItems : 100) * (1 + bound(schema.items as Record<string, unknown>));
    }
    return 0;
  };
  const inputs = Object.entries(documents as Record<string, Record<string, unknown>>).filter(([name]) => name.endsWith("Input"));
  const largest = Math.max(...inputs.map(([, schema]) => bound(schema)));
  assert(largest > 20_000, `compare_groups' matcher lists are the largest legitimate input (${largest})`);
  assert(MAX_TOOL_INPUT_ELEMENTS > largest, `${MAX_TOOL_INPUT_ELEMENTS} must exceed ${largest}`);

  const connection = await connectModernTestClient(
    { name: "max-input-elements", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());
  const result = await connection.client.callTool({ name: "get_trace", arguments: {
    trace_id: "0".repeat(32), attributes: Array.from({ length: MAX_TOOL_INPUT_ELEMENTS }, () => "k"),
  } }) as CallToolResult;
  assert.equal(result.isError, true);
  assert.match(JSON.stringify(result.content), new RegExp(`more than the maximum of ${MAX_TOOL_INPUT_ELEMENTS} elements`, "u"));
});
