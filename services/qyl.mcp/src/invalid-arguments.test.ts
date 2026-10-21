import assert from "node:assert/strict";
import test from "node:test";
import { Client, StreamableHTTPClientTransport, type CallToolResult } from "@modelcontextprotocol/client";
import { createMcpHandler } from "@modelcontextprotocol/server";
import documents from "./contracts/generated/tool-schemas.json" with { type: "json" };
import { readContext } from "./analysis-context.test-helper.js";
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

const cases: Array<{
  tool: string; arguments: Record<string, unknown> | undefined; field: string; keyword: string; message: RegExp;
  allowed?: unknown[]; allowedIncludes?: string[]; hint?: RegExp;
}> = [
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
    tool: "save_query", arguments: { name: "errors", filter: { signal: "trace" } },
    field: "filter.signal", keyword: "enum", allowed: ["traces", "logs", "metrics"],
    message: /^filter\.signal: must be one of: traces, logs, metrics$/u,
  },
  {
    // The discriminator matched one branch: only that branch's rule is reported.
    tool: "save_query", arguments: { name: "errors", filter: { signal: "logs", errors_only: true } },
    field: "filter.errors_only", keyword: "additionalProperties", allowedIncludes: ["severity_min", "query"],
    message: /^filter\.errors_only: is not accepted here; accepted: matchers, signal, lookback_minutes, service_name, severity_min/u,
  },
  {
    tool: "get_trace", arguments: undefined,
    field: "trace_id", keyword: "required", message: /^trace_id: is required$/u,
  },
  {
    // A string no branch of a string-or-object union accepts: every alternative, the object form included.
    tool: "compare_groups", arguments: { signal: "spans", group_a: { failure_id: "fail_1" }, group_b: "others" },
    field: "group_b", keyword: "anyOf", allowed: ["rest", "previous_window", "{matchers, window, failure_id}"],
    message: /^group_b: must be one of: rest, previous_window, \{matchers, window, failure_id\}$/u,
    hint: /^Correct group_b to one of the accepted values, for example "group_b": "rest", and call compare_groups again\.$/u,
  },
  {
    tool: "triage", arguments: { baseline: "yesterday" },
    field: "baseline", keyword: "anyOf", allowed: ["previous_window", "{start_time, end_time}", "{offset_minutes}"],
    message: /^baseline: must be one of: previous_window, \{start_time, end_time\}, \{offset_minutes\}$/u,
    hint: /for example "baseline": "previous_window",/u,
  },
  {
    tool: "query_metric", arguments: { metric_name: "http.server.request.duration", baseline: 1440 },
    field: "baseline", keyword: "anyOf", allowed: ["previous_window", "{start_time, end_time}", "{offset_minutes}"],
    message: /^baseline: must be one of: previous_window, \{start_time, end_time\}, \{offset_minutes\}$/u,
  },
  {
    // query_metric and run_saved_query take the one shared time range, so their lookback has its bounds too.
    tool: "query_metric", arguments: { metric_name: "http.server.request.duration", lookback_minutes: 43_201 },
    field: "lookback_minutes", keyword: "maximum", message: /^lookback_minutes: must be <= 43200$/u,
  },
  {
    tool: "run_saved_query", arguments: { lookback_minutes: 0 },
    field: "lookback_minutes", keyword: "minimum", message: /^lookback_minutes: must be >= 1$/u,
  },
  {
    tool: "get_mcp_stats", arguments: { baseline: "last_week" },
    field: "baseline", keyword: "anyOf", allowed: ["previous_window", "{start_time, end_time}", "{offset_minutes}", "{server_version}"],
    message: /^baseline: must be one of: previous_window, \{start_time, end_time\}, \{offset_minutes\}, \{server_version\}$/u,
  },
  {
    tool: "ci_log", arguments: { ci_run_id: "run-1", baseline: "previous" },
    field: "baseline", keyword: "anyOf", allowed: ["last_success", "{ci_run_id}"],
    message: /^baseline: must be one of: last_success, \{ci_run_id\}$/u,
    hint: /for example "baseline": "last_success",/u,
  },
  {
    // An object is the object form: its own rule is reported, not the union.
    tool: "compare_groups", arguments: { signal: "spans", group_a: { failure_id: "fail_1" }, group_b: { failure: "fail_2" } },
    field: "group_b.failure", keyword: "additionalProperties", allowedIncludes: ["matchers", "window", "failure_id"],
    message: /^group_b\.failure: is not accepted here; accepted: matchers, window, failure_id/u,
  },
  {
    // Of two object forms, the one that knows every member sent is the one meant.
    tool: "triage", arguments: { baseline: { start_time: "2026-10-19T00:00:00Z" } },
    field: "baseline.end_time", keyword: "required", message: /^baseline\.end_time: is required$/u,
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
    const field = expected.field.replaceAll(".", "\\.");
    // An unknown argument is replaced by an accepted name or dropped; a rejected value is corrected,
    // to one of the accepted values where the schema enumerates them.
    if (expected.keyword === "additionalProperties") {
      assert.match(error.hint ?? "", new RegExp(`^Replace ${field} with one of the accepted arguments \\([a-z_]+(?:, [a-z_]+)*\\) or drop it, and call ${expected.tool} again\\.$`, "u"), expected.tool);
      for (const name of expected.allowedIncludes ?? []) assert.match(error.hint ?? "", new RegExp(`[(, ]${name}[,)]`, "u"), `${expected.tool} hint names ${name}`);
    } else {
      assert.match(error.hint ?? "", new RegExp(`^Correct ${field}(?: to one of the accepted values)?(?:, for example "[a-z_]+": .+,)? and call ${expected.tool} again\\.$`, "u"), expected.tool);
    }
    if (expected.hint !== undefined) assert.match(error.hint ?? "", expected.hint, expected.tool);
    const headline = (result.content as Array<{ text: string }>)[0]!.text;
    assert.match(headline, new RegExp(`^${expected.tool} failed: invalid_argument — ${field}: `, "u"));
    assert.doesNotMatch(headline, /\n/u);
  }
  assert.equal(fetch.mock.callCount(), 0);
});

/** Several arguments wrong at once; the Collector's AnalysisErrorTests send the same requests and name the same field. */
const severalWrong: ReadonlyArray<readonly [string, Record<string, unknown>, string]> = [
  ["triage", { status: "opened", lookback_minutes: 0 }, "lookback_minutes"],
  ["triage", { limit: "x", bogus: 1 }, "bogus"],
  ["triage", { service_name: 5, matchers: [{ key: "", op: "exists" }] }, "matchers[0].key"],
  ["triage", { matchers: [{ op: "like", key: "" }] }, "matchers[0].key"],
  ["aggregate_telemetry", { order_by: 5, measures: "count" }, "signal"],
  ["compare_groups", { signal: "spans", group_a: { failure_id: "" }, group_b: { bogus: 1 }, limit: 0 }, "limit"],
  ["compare_groups", { signal: "spans", group_b: { bogus: 1 }, group_a: { failure_id: "" } }, "group_a.failure_id"],
];

test("with several arguments wrong, the first the contract declares is named: an undeclared one first, then depth first", async (context) => {
  context.mock.method(globalThis, "fetch", async () => {
    throw new Error("a rejected call must not reach the Collector");
  });
  const connection = await connectModernTestClient(
    { name: "invalid-arguments-order", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());
  for (const [tool, args, field] of severalWrong) {
    const error = errorOf(await connection.client.callTool({ name: tool, arguments: args }) as CallToolResult);
    assert.deepEqual([error.code, error.field, error.fields?.[0]?.field], ["invalid_argument", field, field], `${tool} ${JSON.stringify(args)}`);
    assert(error.fields !== undefined && error.fields.length > 1, `${tool} names every rule broken: ${JSON.stringify(error.fields)}`);
  }
});

test("a JSON-quoted accepted string is that string; a free-text value keeps its quotes", async (context) => {
  const forwarded = new Map<string, Record<string, unknown>>();
  context.mock.method(globalThis, "fetch", async (input: string | Request | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const operation = new URL(request.url).pathname.split("/").at(-1)!;
    forwarded.set(operation, await request.json() as Record<string, unknown>);
    if (operation !== "compare_groups") return Response.json({ title: "fixture outage" }, { status: 503 });
    return Response.json({ ...readContext(20), discriminators: [{ key: "cloud.region", value: "eu", share_a: 1, share_b: 0, count_a: 10, count_b: 0, lift: null }],
      structural: [], durations: { a: { p50_ms: 20, p95_ms: 20, p99_ms: 20 }, b: { p50_ms: 20, p95_ms: 20, p99_ms: 20 } },
      composition: { changed: true, traffic_mix_changed: false, same_traffic_changed: true, summary: "These are associations, not causes." },
      size_a: 10, size_b: 30, excluded_keys: [] });
  });
  const connection = await connectModernTestClient(
    { name: "quoted-arguments", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  context.after(() => connection.close());
  const call = async (name: string, args: Record<string, unknown>): Promise<CallToolResult> =>
    await connection.client.callTool({ name, arguments: args }) as CallToolResult;

  // The reviewer's call: two models sent group_b with the quotes inside the string.
  const groups = await call("compare_groups", { signal: "spans", group_a: { failure_id: "fail_1" }, group_b: "\"rest\"",
    matchers: [{ key: "service.name", op: "\"eq\"", value: "\"regional\"" }] });
  assert.equal(groups.isError, undefined, JSON.stringify(groups));
  assert.equal(forwarded.get("compare_groups")?.group_b, "rest");
  assert.deepEqual(forwarded.get("compare_groups")?.matchers, [{ key: "service.name", op: "eq", value: "\"regional\"" }],
    "an enumerated op is unquoted; a free-text value is the caller's own text");

  for (const [tool, args, field, value] of [
    ["triage", { baseline: "\"previous_window\"" }, "baseline", "previous_window"],
    ["query_metric", { metric_name: "requests.total", baseline: "\"previous_window\"" }, "baseline", "previous_window"],
    ["get_mcp_stats", { baseline: "\"previous_window\"" }, "baseline", "previous_window"],
    ["ci_log", { ci_run_id: "run-1", baseline: "\"last_success\"" }, "baseline", "last_success"],
    ["triage", { status: "\"all\"" }, "status", "all"],
  ] as const) {
    const result = await call(tool, args);
    assert.notEqual((result.structuredContent as unknown as ErrorBody | undefined)?.error.code, "invalid_argument", `${tool}: ${JSON.stringify(result)}`);
    assert.equal(forwarded.get(tool)?.[field], value, tool);
  }
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
