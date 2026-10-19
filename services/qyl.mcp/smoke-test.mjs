/**
 * Smoke test for the child-process MCP transport with fixed telemetry fixtures.
 *
 * Spawns `bun dist/main.js --stdio` with a local fixture collector and asserts the direct tool
 * surface, the result shape (headline, compact JSON, structuredContent), the structured errors,
 * trace/log behavior, explicit mode selection and the native execution records. The fixture
 * collector answers with the contract's compact shapes, built by the typed test helpers and
 * checked against the generated output documents before they are served.
 *
 * Run: node smoke-test.mjs   (after `bun run test`, which builds dist and dist-test)
 */
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";
import { Client } from "@modelcontextprotocol/client";
import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createServer } from "node:http";
import { readContext, toolLog, toolSummary, toolTrace } from "./dist-test/analysis-context.test-helper.js";
import { checkContract } from "./dist-test/contracts/tool-schemas.js";
import { getTelemetryFixture } from "./dist-test/telemetry-fixtures.test-helper.js";

let failures = 0;
function check(name, condition, detail = "") {
  if (condition) {
    console.log(`  ok  ${name}`);
  } else {
    failures++;
    console.error(`FAIL  ${name}${detail ? ` — ${detail}` : ""}`);
  }
}

async function callTool(client, name, args) {
  return client.callTool({ name, arguments: args });
}

/** Every result is a one-line headline, the compact JSON and the same object as structuredContent. */
function framed(result) {
  const [headline, json] = result.content ?? [];
  if (headline?.type !== "text" || /\n/u.test(headline.text) || json?.type !== "text") return false;
  try {
    return JSON.stringify(JSON.parse(json.text)) === JSON.stringify(result.structuredContent);
  } catch {
    return false;
  }
}

// Presence of named tools; the committed manifest pins the whole catalog.
const READ_TOOLS = [
  "triage", "get_failure_context", "compare_groups", "compare_traces", "verify_fix",
  "aggregate_telemetry", "discover_attributes", "get_agent_run", "list_releases", "get_service_map",
  "check_telemetry_health", "get_mcp_stats", "ci_log", "get_trace", "list_metrics",
  "list_sessions", "list_traces", "query_metric", "run_saved_query", "search_logs",
];
const WRITE_TOOLS = ["annotate", "save_query", "update_failure_status", "delete_record"];

// This fixture verifies the child-process transport only.
// Recorded-telemetry query semantics are verified in verify-answer-key.mjs.
const fixture = getTelemetryFixture();
const MISSING_TRACE = "0000000000000000000000000000dead";
const now = new Date().toISOString();
const metricRow = (index) => ({
  name: `fixture.metric.${index}`, kind: "gauge", unit: "1", temporality: "unspecified", services: ["qyl-collector"],
  series_count: 1, last_seen: now, supported_aggregations: ["avg", "min", "max", "last"], has_conflict: false,
  keys: [], suggested_group_by: [],
});
const outputSchema = (operation) => `Mcp.Tools.${operation.split("_").map((part) => part[0].toUpperCase() + part.slice(1)).join("")}Output`;

/** The analysis body the Collector would answer `operation` with, or a Problem Details refusal. */
function analysisAnswer(operation, args) {
  switch (operation) {
    case "list_traces":
      return { ...readContext(fixture.traces.length), mode: "live", traces: fixture.traces.map(toolSummary) };
    case "get_trace": {
      const trace = fixture.traces.find((candidate) => candidate.trace_id === args.trace_id);
      if (!trace) {
        return { status: 404, problem: {
          type: "https://qyl.at/problems/not-found", title: "Not Found", status: 404, code: "not_found",
          detail: `Trace ${args.trace_id} was not found in this project; list_traces lists the traces.`,
          resource_type: "trace", resource_id: args.trace_id,
        } };
      }
      return { ...readContext(trace.spans.length), mode: "live", trace: toolTrace(trace) };
    }
    case "search_logs": {
      const logs = fixture.logs
        .filter((log) => (args.severity_min === undefined || log.severity_number >= args.severity_min) && (args.trace_id === undefined || log.trace_id === args.trace_id))
        .map(toolLog);
      return { ...readContext(logs.length), mode: "live", logs };
    }
    case "ci_log":
      return { ...readContext(0), mode: "live", runs: [] };
    case "list_metrics":
      return { ...readContext(3), items: [0, 1, 2].map(metricRow) };
    default:
      return { status: 400, problem: {
        type: "https://qyl.at/problems/validation-failed", title: "Validation Failed", status: 400, code: "validation_failed",
        errors: [], detail: "Writes require the real collector integration suite.",
      } };
  }
}

const fixtureCollector = createServer((request, response) => {
  void (async () => {
    const chunks = []; for await (const chunk of request) chunks.push(chunk);
    const args = JSON.parse(Buffer.concat(chunks).toString() || "{}");
    const operation = request.url.split("/").at(-1);
    const answer = analysisAnswer(operation, args);
    if (answer.problem !== undefined) {
      response.writeHead(answer.status, { "content-type": "application/problem+json" }).end(JSON.stringify(answer.problem));
      return;
    }
    // A fixture that drifted from the contract fails here, by name, not as a contract mismatch in the MCP.
    const checked = checkContract(outputSchema(operation), answer);
    assert(checked.valid, `fixture ${operation} answer: ${checked.valid ? "" : checked.message}`);
    response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify(answer));
  })().catch((error) => {
    console.error(`fixture collector: ${error instanceof Error ? error.message : String(error)}`);
    response.writeHead(500, { "content-type": "text/plain" }).end(String(error));
  });
});
await new Promise((resolve) => fixtureCollector.listen(0, "127.0.0.1", resolve));
const fixtureUrl = `http://127.0.0.1:${fixtureCollector.address().port}`;
const temp = await mkdtemp(join(tmpdir(), "qyl-mcp-server-smoke-"));
const nativeStatePath = join(temp, "native-executions.json");
const transport = new StdioClientTransport({
  command: "bun",
  args: ["dist/main.js", "--stdio"],
  env: {
    ...process.env,
    QYL_COLLECTOR_URL: fixtureUrl,
    QYL_MCP_TELEMETRY: "0",
    QYL_MCP_NATIVE_STATE_PATH: nativeStatePath,
  },
});
const client = new Client(
  { name: "qyl-smoke", version: "1.0.0" },
  { versionNegotiation: { mode: { pin: "2026-07-28" } } },
);
try {
  await client.connect(transport);

// --- 0. stdout hygiene ---------------------------------------------------
// A stdio MCP server owns stdout for JSON-RPC alone; every log line belongs on
// stderr. Proven in LIVE mode against an unreachable collector, so the startup
// warning is actually emitted, by teeing the raw stdout stream and parsing it.
console.log("stdout hygiene");
{
  const rawPath = join(temp, "stdout.raw");
  const hygieneTransport = new StdioClientTransport({
    command: "sh",
    args: ["-c", `exec bun dist/main.js --stdio | tee "${rawPath}"`],
    env: { ...process.env, QYL_COLLECTOR_URL: "http://127.0.0.1:9", QYL_MCP_TELEMETRY: "0", QYL_MCP_NATIVE_STATE_PATH: join(temp, "hygiene-native.json") },
    stderr: "ignore",
  });
  let transportErrors = 0;
  hygieneTransport.onerror = () => transportErrors++;
  const hygieneClient = new Client({ name: "qyl-smoke-hygiene", version: "1.0.0" }, { versionNegotiation: { mode: { pin: "2026-07-28" } } });
  await hygieneClient.connect(hygieneTransport);
  await hygieneClient.listTools();
  await hygieneClient.callTool({ name: "list_traces", arguments: { limit: 1 } });
  await hygieneClient.callTool({ name: "get_trace", arguments: { nonsense: 1 } }).catch(() => {});
  await hygieneClient.close();
  await new Promise((r) => setTimeout(r, 200));
  const lines = (await readFile(rawPath, "utf8")).split("\n").filter((l) => l.trim());
  const foreign = lines.filter((l) => { try { return JSON.parse(l).jsonrpc !== "2.0"; } catch { return true; } });
  check("stdout carried only JSON-RPC messages in live mode with a startup warning", lines.length >= 3 && foreign.length === 0, foreign.slice(0, 3).join(" | "));
  check("the client parsed every stdout line", transportErrors === 0, `${transportErrors} transport error(s)`);
}

// --- 1. Direct tools/list ----------------------------------------------------
console.log("tools/list");
const { tools } = await client.listTools();
const names = new Set(tools.map((t) => t.name));
const missing = [...READ_TOOLS, ...WRITE_TOOLS].filter((name) => !names.has(name));
check("every read and write tool is published", missing.length === 0, `missing: ${missing.join(", ")}`);
check("no tool names a UI resource", tools.every((t) => t._meta?.ui === undefined));
// A client that cached an outputSchema validates results against it; no tool publishes one, so
// the compact results and the structured errors never meet a stale schema.
check("no tool advertises an outputSchema", tools.every((t) => t.outputSchema === undefined),
  tools.filter((t) => t.outputSchema !== undefined).map((t) => t.name).join(", "));

// Writes change only annotations and saved queries; their hints say so, and
// the collector enforces the verified project and required write roles.
const hints = (name) => JSON.stringify(tools.find((t) => t.name === name)?.annotations);
check(
  "the write tools may replace or remove a version, and repeating one changes nothing more",
  hints("annotate") === JSON.stringify({ readOnlyHint: false, destructiveHint: true, idempotentHint: true, openWorldHint: false }) &&
    hints("save_query") === hints("annotate") &&
    hints("update_failure_status") === hints("annotate") &&
    hints("delete_record") === hints("annotate"),
  `${hints("annotate")} ${hints("save_query")} ${hints("delete_record")}`,
);
check(
  "every other tool stays read-only",
  tools
    .filter((t) => !WRITE_TOOLS.includes(t.name))
    .every((t) => t.annotations?.readOnlyHint === true && t.annotations?.destructiveHint === false),
);
const fixtureWrite = await callTool(client, "annotate", {
  target: { kind: "trace", trace_id: "0af7651916cd43dd8448eb211c80319c" },
  text: "smoke",
});
check(
  "read-only transport fixture refuses writes with a stable code",
  fixtureWrite.isError === true && fixtureWrite.structuredContent?.error?.code === "collector_rejected" && framed(fixtureWrite) &&
    !/real collector integration suite/u.test(JSON.stringify(fixtureWrite.content)),
  JSON.stringify(fixtureWrite),
);

const ciLog = await callTool(client, "ci_log", {});
check("ci_log returns typed run evidence", !ciLog.isError && Array.isArray(ciLog.structuredContent?.runs) && framed(ciLog), JSON.stringify(ciLog));

// --- 2. Direct read tools + handler error contract ---------------------------
console.log("list_traces / get_trace error");
const listed = await callTool(client, "list_traces", {});
check("list_traces: not isError", !listed.isError, listed.content?.[0]?.text);
check(
  "list_traces returns 8 compact trace rows without spans",
  listed.structuredContent?.traces?.length === 8 &&
    listed.structuredContent.traces.every((t) => t.spans === undefined && typeof t.root?.name === "string" && typeof t.error_count === "number"),
);
check("mode is live", listed.structuredContent?.mode === "live");
check("list_traces: headline, compact JSON and structuredContent", framed(listed), JSON.stringify(listed.content?.[0]));
const errorRow = listed.structuredContent?.traces?.find((t) => t.error_count > 0);
check(
  "list_traces headline names the rows and the next tool with an id",
  /^list_traces: 8 traces, \d+ with errors\. Top: [a-f0-9]{32} /u.test(listed.content?.[0]?.text ?? "") &&
    (listed.content?.[0]?.text ?? "").endsWith(`→ get_failure_context trace_id=${errorRow?.trace_id}`),
  listed.content?.[0]?.text,
);

const missingTrace = await callTool(client, "get_trace", { trace_id: MISSING_TRACE });
const missingError = missingTrace.structuredContent?.error;
check("handler failure returns isError:true", missingTrace.isError === true && framed(missingTrace));
check(
  "a missing trace names what is missing and the tool that lists valid ids",
  missingError?.code === "not_found" && missingError.resource_type === "trace" && missingError.resource_id === MISSING_TRACE &&
    /list_traces lists the traces/u.test(missingError.message) && /list_traces/u.test(missingError.hint ?? "") &&
    (missingTrace.content?.[0]?.text ?? "").startsWith(`get_trace failed: not_found — Trace ${MISSING_TRACE} was not found`),
  JSON.stringify(missingTrace.structuredContent),
);

const rejected = await callTool(client, "get_trace", { trace_id: MISSING_TRACE, selection: "everything" });
check(
  "a rejected argument names the field, the accepted values and a hint without reaching the Collector",
  rejected.isError === true && framed(rejected) && rejected.structuredContent?.error?.code === "invalid_argument" &&
    rejected.structuredContent.error.field === "selection" &&
    JSON.stringify(rejected.structuredContent.error.allowed) === JSON.stringify(["all", "errors", "errors_with_ancestors", "critical_path"]) &&
    (rejected.structuredContent.error.hint ?? "").startsWith("Correct selection"),
  JSON.stringify(rejected.structuredContent),
);

// --- 3. fixture traces -------------------------------------------------------
// The response budget may place a special case outside this bounded page.
// Fetch each known case explicitly so its transport assertions always run.
async function requiredFixtureTrace(predicate, label) {
  const expected = fixture.traces.find(predicate);
  assert(expected, `fixture must contain ${label}`);
  const result = await callTool(client, "get_trace", { trace_id: expected.trace_id });
  check(`${label}: not isError`, !result.isError, result.content?.[0]?.text);
  assert(result.structuredContent?.trace, `${label}: get_trace must return its typed trace`);
  return result.structuredContent.trace;
}
const errorTrace = await requiredFixtureTrace(t => t.has_error, "error trace");
check("the error trace counts its error spans", errorTrace.error_count > 0 && errorTrace.error_count === errorTrace.spans.filter((s) => s.status === "error").length);
check(
  "an error span carries its status message",
  errorTrace.spans.some((s) => s.status === "error" && typeof s.message === "string" && s.message.length > 0),
);
const deepTrace = await requiredFixtureTrace(t => t.span_count >= 10, "deep trace");
check(
  "a deep trace with 10+ spans exists",
  deepTrace.span_count >= 10 && deepTrace.spans.length === deepTrace.span_count,
);
const genAiTrace = await requiredFixtureTrace(t => t.spans.some(s => s.attributes?.some(a => a.key === "gen_ai.system")), "gen_ai trace");
check(
  "gen_ai spans present",
  genAiTrace.spans.some((s) => s.attributes?.some((a) => a.key === "gen_ai.system")),
);

// --- 4. get_trace ------------------------------------------------------------
console.log("get_trace");
const someId = listed.structuredContent.traces[0].trace_id;
const got = await callTool(client, "get_trace", { trace_id: someId });
check("not isError", !got.isError, got.content?.[0]?.text);
check(
  "returns the trace with spans",
  got.structuredContent?.trace?.trace_id === someId &&
    got.structuredContent.trace.spans.length > 0,
);
check(
  "get_trace headline names the trace, its counts and the next tool",
  framed(got) && (got.content?.[0]?.text ?? "").startsWith(`get_trace: ${someId} `) && / → (get_failure_context|search_logs) trace_id=/u.test(got.content?.[0]?.text ?? ""),
  got.content?.[0]?.text,
);

// --- 5. search_logs severity_min:17 ------------------------------------------
console.log("search_logs { severity_min: 17 }");
const logsRes = await callTool(client, "search_logs", { severity_min: 17 });
const logs = logsRes.structuredContent?.logs;
check("not isError", !logsRes.isError, logsRes.content?.[0]?.text);
check("returns at least one log", Array.isArray(logs) && logs.length > 0);
check(
  "only ERROR+ logs (severity_number >= 17)",
  logs?.every((l) => l.severity_number >= 17),
  JSON.stringify(logs?.map((l) => l.severity_number)),
);

// --- 6. search_logs for the error trace -------------------------------------
console.log("search_logs { trace_id: <error trace> }");
const corr = await callTool(client, "search_logs", { trace_id: errorTrace.trace_id });
const corrLogs = corr.structuredContent?.logs;
check("not isError", !corr.isError, corr.content?.[0]?.text);
check("returns logs", Array.isArray(corrLogs) && corrLogs.length > 0);
check("every log belongs to the error trace", corrLogs?.every((l) => l.trace_id === errorTrace.trace_id));
check(
  "includes an ERROR log with a stacktrace body",
  corrLogs?.some(
    (l) =>
      l.severity_number >= 17 &&
      typeof l.body === "string" &&
      l.body.includes("   at "),
  ),
);

const nativeState = JSON.parse(await readFile(nativeStatePath, "utf8"));
check(
  "native tool execution evidence is automatic and terminal",
  nativeState.version === 3 &&
    nativeState.executions.length >= 8 &&
    nativeState.executions.every((execution) =>
      execution.status !== "running" &&
      execution.durationMs >= 0 &&
      typeof execution.toolName === "string" &&
      typeof execution.id === "string"),
);
check(
  "native records contain only operation metadata",
  nativeState.executions.every((execution) =>
    Object.keys(execution).every((key) => [
      "id", "toolName", "status", "createdAt", "startedAt", "completedAt", "durationMs", "errorType",
    ].includes(key))),
);
} finally {
  await client.close().catch(() => undefined);
  await rm(temp, { recursive: true, force: true });
}

// A stock SDK v2 client opens with the 2025 handshake. It uses the same
// standalone entry point and tool contract as the modern client above.
console.log("2025-era stdio compatibility");
const legacyTemp = await mkdtemp(join(tmpdir(), "qyl-mcp-legacy-smoke-"));
const legacyClient = new Client({ name: "qyl-smoke-legacy", version: "1.0.0" });
try {
  await legacyClient.connect(new StdioClientTransport({
    command: "bun",
    args: ["dist/main.js", "--stdio"],
    env: {
      ...process.env,
      QYL_COLLECTOR_URL: fixtureUrl, QYL_MCP_TELEMETRY: "0",
      QYL_MCP_NATIVE_STATE_PATH: join(legacyTemp, "native.json"),
    },
  }));
  check("stock client negotiated the legacy era", legacyClient.getProtocolEra() === "legacy");
  const legacyTools = (await legacyClient.listTools()).tools;
  const legacyNames = new Set(legacyTools.map((t) => t.name));
  check(
    "legacy catalog publishes the same tools, without output schemas",
    [...READ_TOOLS, ...WRITE_TOOLS].every((name) => legacyNames.has(name)) && legacyTools.every((t) => t.outputSchema === undefined),
  );
  const metrics = await legacyClient.callTool({ name: "list_metrics", arguments: {} });
  check("legacy read tool returns compact metric rows", !metrics.isError && metrics.structuredContent?.items?.length === 3 && framed(metrics), JSON.stringify(metrics));
  const invalid = await legacyClient.callTool({ name: "get_trace", arguments: {} });
  check(
    "legacy invalid arguments are the same structured tool error",
    invalid.isError === true && framed(invalid) && invalid.structuredContent?.error?.code === "invalid_argument" &&
      invalid.structuredContent.error.field === "trace_id",
    JSON.stringify(invalid),
  );
} finally {
  await legacyClient.close().catch(() => undefined);
  await rm(legacyTemp, { recursive: true, force: true });
  await new Promise(resolve=>fixtureCollector.close(resolve));
}


if (failures > 0) {
  console.error(`\n${failures} check(s) FAILED`);
  process.exit(1);
}
console.log("\nall checks passed");
