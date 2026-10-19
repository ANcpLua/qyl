import assert from "node:assert/strict";
import test, { type TestContext } from "node:test";
import {
  Client,
  InsufficientScopeError,
  StreamableHTTPClientTransport,
  type FetchLike,
} from "@modelcontextprotocol/client";
import {
  createMcpHandler,
  getOAuthProtectedResourceMetadataUrl,
  type AuthInfo,
  type CallToolResult,
} from "@modelcontextprotocol/server";
import type { Annotation, SavedQuery } from "./contracts/generated/api.js";
import { createResourceAuthorization } from "./authorization.js";
import { API_KEY_AUTH_HEADER, IDEMPOTENCY_KEY_HEADER, IF_MATCH_HEADER, PROJECT_SCOPE_HEADER } from "./contracts/generated/headers.js";
import { readContext, toolSummary, toolTrace } from "./analysis-context.test-helper.js";
import { getTelemetryFixture } from "./telemetry-fixtures.test-helper.js";
import {
  QYL_MCP_DELETE_SCOPE,
  QYL_MCP_RESOURCE,
  QYL_MCP_SCOPE,
  QYL_MCP_WRITE_SCOPE,
} from "./oauth.js";
import { createServer } from "./server.js";
import { READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS } from "./tools.js";
import { DELETE_TOOL_ANNOTATIONS, UPDATE_TOOL_ANNOTATIONS } from "./write-tools.js";

const RESOURCE = new URL(QYL_MCP_RESOURCE);
const METADATA_URL = getOAuthProtectedResourceMetadataUrl(RESOURCE);
const OWNER = "auth0|write-owner-test";
const REVIEWER = "auth0|write-reviewer-test";
const PROJECTS = [
  { project: "owner", apiKey: "owner-write-key", subjects: [OWNER] },
  { project: "review", apiKey: "review-write-key", subjects: [REVIEWER] },
];
const TRACE = getTelemetryFixture().traces[0]!;
const SPAN_ID = TRACE.spans[0]!.span_id;
const SESSION_ID = getTelemetryFixture().sessions[0]!.session_id;

const WRITE_TOOLS = {
  annotate: { annotations: UPDATE_TOOL_ANNOTATIONS, scopes: [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE] },
  save_query: { annotations: UPDATE_TOOL_ANNOTATIONS, scopes: [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE] },
  update_failure_status: { annotations: UPDATE_TOOL_ANNOTATIONS, scopes: [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE] },
  delete_record: { annotations: DELETE_TOOL_ANNOTATIONS, scopes: [QYL_MCP_SCOPE, QYL_MCP_DELETE_SCOPE] },
  run_saved_query: { annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS, scopes: [QYL_MCP_SCOPE] },
} as const;

function environment(context: TestContext, values: Record<string, string | undefined>): void {
  const previous = Object.fromEntries(Object.keys(values).map((key) => [key, process.env[key]]));
  for (const [key, value] of Object.entries(values)) {
    if (value === undefined) delete process.env[key];
    else process.env[key] = value;
  }
  context.after(() => {
    for (const [key, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  });
}

function liveCollector(context: TestContext, projects = true): void {
  environment(context, {
    QYL_COLLECTOR_URL: "https://collector.example.test",
    QYL_API_KEY: projects ? "default-must-not-be-used" : "local-write-key",
    QYL_PROJECT: projects ? "default-must-not-be-used" : undefined,
    MCP_COLLECTOR_PROJECTS: projects ? JSON.stringify(PROJECTS) : undefined,
  });
}

const STAMP = { created_at: "2026-10-11T14:00:00.000Z", updated_at: "2026-10-11T14:00:00.000Z" };

function annotation(overrides: Partial<Annotation> = {}): Annotation {
  return {
    id: "ann-1",
    etag: "\"1\"",
    target: { kind: "span", trace_id: TRACE.trace_id, span_id: SPAN_ID },
    text: "The checkout retry loop starts here.",
    ...STAMP,
    ...overrides,
  } as Annotation;
}

function savedQuery(overrides: Partial<SavedQuery> = {}): SavedQuery {
  return {
    id: "sq-1",
    etag: "\"1\"",
    name: "checkout errors",
    filter: { signal: "traces", service_name: "checkout-api", errors_only: true, lookback_minutes: 60 },
    ...STAMP,
    ...overrides,
  } as SavedQuery;
}

// Collector problems name their type URI and stable code; the collector picks both by status.
const PROBLEM_TYPES: Record<number, [string, string]> = {
  400: ["validation-failed", "validation_failed"],
  401: ["unauthorized", "unauthorized"],
  403: ["forbidden", "forbidden"],
  404: ["not-found", "not_found"],
  409: ["conflict", "conflict"],
  412: ["precondition-failed", "precondition_failed"],
  428: ["precondition-required", "precondition_required"],
  500: ["internal-error", "internal_error"],
  503: ["service-unavailable", "service_unavailable"],
};

function problem(status: number, title: string, extension: Readonly<Record<string, string>> = {}): Response {
  const [name, code] = PROBLEM_TYPES[status] ?? ["internal-error", "internal_error"];
  return Response.json({ type: `https://qyl.at/problems/${name}`, code, title, status, ...extension }, {
    status, headers: { "content-type": "application/problem+json" },
  });
}

interface CollectorCall {
  method: string;
  path: string;
  search: URLSearchParams;
  project: string | null;
  apiKey: string | null;
  ifMatch: string | null;
  idempotencyKey: string | null;
  body: unknown;
}

type Route = (call: CollectorCall) => Response | Promise<Response>;

/** A Collector stand-in: records every request, answers from `route`. */
function mockCollector(context: TestContext, route: Route): CollectorCall[] {
  const calls: CollectorCall[] = [];
  context.mock.method(globalThis, "fetch", async (input: string | Request | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    assert.equal(url.origin, "https://collector.example.test");
    const text = await request.text();
    const call: CollectorCall = {
      method: request.method,
      path: url.pathname,
      search: url.searchParams,
      project: request.headers.get(PROJECT_SCOPE_HEADER),
      apiKey: request.headers.get(API_KEY_AUTH_HEADER),
      ifMatch: request.headers.get(IF_MATCH_HEADER),
      idempotencyKey: request.headers.get(IDEMPOTENCY_KEY_HEADER),
      body: text === "" ? undefined : JSON.parse(text),
    };
    calls.push(call);
    return route(call);
  });
  return calls;
}

function verifier() {
  return {
    async verifyAccessToken(token: string): Promise<AuthInfo> {
      const { subject, scopes } = JSON.parse(Buffer.from(token, "base64url").toString("utf8")) as {
        subject: string;
        scopes: string[];
      };
      return {
        token, clientId: "write-tools-test", scopes,
        expiresAt: Math.floor(Date.now() / 1000) + 300,
        resource: RESOURCE,
        extra: { subject },
      };
    },
  };
}

function token(subject: string, scopes: readonly string[]): string {
  return Buffer.from(JSON.stringify({ subject, scopes })).toString("base64url");
}

/** The hosted composition: bearer gate in front of the SDK handler, in either era. */
async function hostedClient(
  context: TestContext,
  subject: string,
  scopes: readonly string[],
  era: "modern" | "legacy" = "modern",
): Promise<Client> {
  const handler = createMcpHandler(() => createServer({ nativeExecution: false }));
  context.after(() => handler.close());
  const gate = createResourceAuthorization({
    verifier: verifier(),
    expectedResource: RESOURCE,
    requiredScopes: [QYL_MCP_SCOPE],
    resourceMetadataUrl: METADATA_URL,
  });
  const serve: FetchLike = async (input, init) => {
    const request = new Request(input, init);
    const auth = await gate(request);
    return auth instanceof Response ? auth : handler.fetch(request, { authInfo: auth });
  };
  const client = new Client(
    { name: "write-tools-test", version: "1.0.0" },
    { versionNegotiation: { mode: era === "modern" ? { pin: "2026-07-28" } : "legacy" } },
  );
  context.after(() => client.close().catch(() => undefined));
  await client.connect(new StreamableHTTPClientTransport(RESOURCE, {
    fetch: serve,
    requestInit: { headers: { authorization: `Bearer ${token(subject, scopes)}` } },
  }));
  return client;
}

/** Local serving without verified authentication, as stdio and loopback HTTP run: the Collector key decides. */
async function localClient(context: TestContext): Promise<Client> {
  const handler = createMcpHandler(() => createServer({ nativeExecution: false }));
  context.after(() => handler.close());
  const client = new Client(
    { name: "write-tools-local", version: "1.0.0" },
    { versionNegotiation: { mode: { pin: "2026-07-28" } } },
  );
  context.after(() => client.close().catch(() => undefined));
  await client.connect(new StreamableHTTPClientTransport(RESOURCE, {
    fetch: (input, init) => handler.fetch(new Request(input, init)),
  }));
  return client;
}

function structured<T>(result: CallToolResult): T {
  return result.structuredContent as T;
}

function errorOf(result: CallToolResult): { code: string; message: string; retryable: boolean } {
  assert.equal(result.isError, true, JSON.stringify(result));
  return structured<{ error: { code: string; message: string; retryable: boolean } }>(result).error;
}

test("the write tools carry the contract's annotations and scopes; every other tool stays read-only", async (context) => {
  const client = await localClient(context);
  const { tools } = await client.listTools();
  for (const name of Object.keys(WRITE_TOOLS)) assert.ok(tools.some((tool) => tool.name === name), `${name} is published`);
  assert.ok(!tools.some((tool) => tool.name === "delete"), "delete was renamed to delete_record");
  for (const tool of tools) {
    const expected = WRITE_TOOLS[tool.name as keyof typeof WRITE_TOOLS];
    assert.ok(tool.title, tool.name);
    assert.match(tool.description ?? "", /when/u, tool.name);
    if (expected === undefined) {
      assert.deepEqual(tool.annotations, READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS, tool.name);
      assert.deepEqual(tool._meta?.securitySchemes, [{ type: "oauth2", scopes: [QYL_MCP_SCOPE] }], tool.name);
      continue;
    }
    assert.deepEqual(tool.annotations, expected.annotations, tool.name);
    assert.deepEqual(tool._meta?.securitySchemes, [{ type: "oauth2", scopes: [...expected.scopes] }], tool.name);
  }
  const remove = tools.find((tool) => tool.name === "delete_record");
  assert.ok(remove);
  assert.deepEqual(
    (remove.inputSchema.properties as { kind: { enum: string[] } }).kind.enum,
    ["annotation", "saved_query"],
    "alert rules arrive with Contract 11.7.0; telemetry is never a kind",
  );
  assert.match(remove.description ?? "", /never traces, logs or metrics/u);
});

for (const era of ["modern", "legacy"] as const) {
  test(`a ${era} call without qyl:write gets a 403 insufficient_scope challenge before any Collector request`, async (context) => {
    liveCollector(context);
    const calls = mockCollector(context, () => Response.json(annotation(), { status: 201 }));
    const reader = await hostedClient(context, OWNER, [QYL_MCP_SCOPE], era);
    await assert.rejects(
      reader.callTool({
        name: "annotate",
        arguments: { target: { kind: "trace", trace_id: TRACE.trace_id }, text: "note" },
      }),
      (error: unknown) => {
        assert(error instanceof InsufficientScopeError, String(error));
        assert.equal(error.requiredScope, "qyl:write");
        assert.equal(error.resourceMetadataUrl?.href, METADATA_URL);
        assert.equal(error.errorDescription, "annotate needs the qyl:write scope");
        return true;
      },
    );
    assert.equal(calls.length, 0);

    // A step-up challenge names only the scope this operation still lacks.
    const writer = await hostedClient(context, OWNER, [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE], era);
    await assert.rejects(
      writer.callTool({ name: "delete_record", arguments: { kind: "annotation", id: "ann-1", etag: "\"1\"" } }),
      (error: unknown) => {
        assert(error instanceof InsufficientScopeError, String(error));
        assert.equal(error.requiredScope, "qyl:delete");
        return true;
      },
    );
    assert.equal(calls.length, 0);
  });
}

test("qyl:read alone keeps the read surface, saved-query reads included", async (context) => {
  liveCollector(context);
  mockCollector(context, (call) => {
    if (call.path === "/api/v1/analysis/run_saved_query") return Response.json({ ...readContext(1), saved_queries: [savedQuery()], has_more: false });
    return problem(404, "Not Found");
  });
  const reader = await hostedClient(context, OWNER, [QYL_MCP_SCOPE]);
  const listed = await reader.callTool({ name: "run_saved_query", arguments: {} });
  assert.equal(listed.isError, undefined, JSON.stringify(listed));
});

test("writes reach only the verified subject's project, with its key and the etag as If-Match", async (context) => {
  liveCollector(context);
  const calls = mockCollector(context, (call) => {
    if (call.method === "POST" && call.path === "/api/v1/annotations") {
      return Response.json(annotation({ target: (call.body as Annotation).target, text: (call.body as Annotation).text }), { status: 201 });
    }
    if (call.method === "PATCH" && call.path === "/api/v1/annotations/ann-1") {
      return call.ifMatch === "\"1\""
        ? Response.json(annotation({ etag: "\"2\"", text: (call.body as { text: string }).text }))
        : problem(412, "Precondition Failed");
    }
    if (call.method === "DELETE" && call.path === "/api/v1/annotations/ann-1") {
      return call.ifMatch === "\"2\"" ? new Response(null, { status: 204 }) : problem(412, "Precondition Failed");
    }
    return problem(404, "Not Found");
  });
  const scopes = [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE, QYL_MCP_DELETE_SCOPE];
  const [owner, reviewer] = await Promise.all([
    hostedClient(context, OWNER, scopes),
    hostedClient(context, REVIEWER, scopes),
  ]);

  const target = { kind: "span", trace_id: TRACE.trace_id, span_id: SPAN_ID };
  const created = await owner.callTool({
    name: "annotate",
    arguments: { target, text: "The checkout retry loop starts here.", idempotency_key: "repeat-note" },
    _meta: { project: "review", subject: REVIEWER },
  }) as CallToolResult;
  assert.equal(created.isError, undefined, JSON.stringify(created));
  assert.deepEqual(structured<{ annotation: Annotation }>(created).annotation.target, target);
  assert.match(JSON.stringify(created.content), /annotate: created annotation ann-1 on span/u);
  const firstCreate = calls.at(-1);
  assert.ok(firstCreate);
  assert.match(firstCreate.idempotencyKey ?? "", /^[a-f0-9]{64}$/u, "a create carries a derived Idempotency-Key");
  assert.deepEqual(firstCreate, {
    method: "POST", path: "/api/v1/annotations", search: new URLSearchParams(),
    project: "owner", apiKey: "owner-write-key", ifMatch: null, idempotencyKey: firstCreate.idempotencyKey,
    body: { target, text: "The checkout retry loop starts here." },
  });
  // No argument can name a project: the published input is sealed.
  const smuggled = await reviewer.callTool({
    name: "annotate",
    arguments: { target, text: "x", project: "owner" },
  }) as CallToolResult;
  assert.equal(smuggled.isError, true);
  assert.equal(calls.length, 1);

  const stale = await reviewer.callTool({
    name: "annotate",
    arguments: { id: "ann-1", etag: "\"0\"", text: "edited" },
  }) as CallToolResult;
  assert.deepEqual(errorOf(stale), {
    code: "precondition_failed",
    message: "annotation ann-1 changed since that etag was read; read it again for its current etag",
    retryable: false,
    hint: "Read the object again and retry with its current etag.",
  });
  assert.equal(calls.at(-1)!.project, "review");
  assert.equal(calls.at(-1)!.apiKey, "review-write-key");

  const moved = await owner.callTool({
    name: "annotate",
    arguments: { id: "ann-1", etag: "\"1\"", target: { kind: "trace", trace_id: TRACE.trace_id } },
  }) as CallToolResult;
  assert.equal(errorOf(moved).code, "invalid_argument");
  assert.match(errorOf(moved).message, /^target: is fixed at creation/u);
  const updated = await owner.callTool({
    name: "annotate",
    arguments: { id: "ann-1", etag: "\"1\"", text: "edited" },
  }) as CallToolResult;
  assert.equal(updated.isError, undefined, JSON.stringify(updated));
  assert.match(JSON.stringify(updated.content), /annotate: updated annotation ann-1 on span/u);
  assert.equal(structured<{ annotation: Annotation }>(updated).annotation.etag, "\"2\"");
  assert.deepEqual(calls.at(-1)!.body, { text: "edited" }, "the target is fixed at creation");
  assert.equal(calls.at(-1)!.ifMatch, "\"1\"");

  const deleted = await owner.callTool({
    name: "delete_record",
    arguments: { kind: "annotation", id: "ann-1", etag: "\"2\"" },
  }) as CallToolResult;
  assert.equal(deleted.isError, undefined, JSON.stringify(deleted));
  assert.deepEqual(structured(deleted), { deleted: true });
  assert.deepEqual(
    calls.map((call) => `${call.method} ${call.path} ${call.project}`),
    [
      "POST /api/v1/annotations owner",
      "PATCH /api/v1/annotations/ann-1 review",
      "PATCH /api/v1/annotations/ann-1 owner",
      "DELETE /api/v1/annotations/ann-1 owner",
    ],
  );
  assert.doesNotMatch(JSON.stringify([created, updated, deleted, stale]), /owner-write-key|review-write-key/u);

  // The same intended create repeats the same key, so a retry after a lost
  // answer returns the first object; a different note is a different key.
  await owner.callTool({ name: "annotate", arguments: { target, text: "The checkout retry loop starts here.", idempotency_key: "repeat-note" } });
  assert.equal(calls.at(-1)?.idempotencyKey, firstCreate.idempotencyKey);
  await owner.callTool({ name: "annotate", arguments: { target, text: "A different note.", idempotency_key: "different-note" } });
  assert.notEqual(calls.at(-1)?.idempotencyKey, firstCreate.idempotencyKey);
});

test("saved queries: create, conflict, change, list with paging, run and delete", async (context) => {
  liveCollector(context, false);
  const calls = mockCollector(context, (call) => {
    if (call.method === "POST" && call.path === "/api/v1/saved-queries") {
      const body = call.body as { name: string; filter: SavedQuery["filter"] };
      return body.name === "taken"
        ? problem(409, "Conflict")
        : Response.json(savedQuery({ name: body.name, filter: body.filter }), { status: 201 });
    }
    if (call.method === "PATCH" && call.path === "/api/v1/saved-queries/sq-1") {
      return Response.json(savedQuery({ etag: "\"2\"", name: (call.body as { name: string }).name }));
    }
    if (call.method === "POST" && call.path === "/api/v1/analysis/run_saved_query") {
      return (call.body as { id?: string }).id === undefined
        ? Response.json({ ...readContext(1), saved_queries: [savedQuery()], has_more: true, next_cursor: "page-2" })
        : Response.json({ ...readContext(1, false), saved_query: savedQuery(), traces: [toolSummary(TRACE)], has_more: false });
    }
    if (call.method === "DELETE" && call.path === "/api/v1/saved-queries/sq-1") {
      return new Response(null, { status: 204 });
    }
    return problem(404, "Not Found", { resource_type: "saved_query", resource_id: call.path.split("/").at(-1)! });
  });
  // Local stdio-style serving: no verified auth, the Collector key decides.
  const client = await localClient(context);
  const filter = { signal: "traces", service_name: "checkout-api", errors_only: true, lookback_minutes: 60 };

  const incomplete = await client.callTool({ name: "save_query", arguments: { filter } }) as CallToolResult;
  assert.equal(errorOf(incomplete).code, "invalid_argument");
  assert.match(errorOf(incomplete).message, /^name: is required to create a saved query/u);
  assert.equal(calls.length, 0, "a create without a name never reaches the Collector");
  const created = await client.callTool({ name: "save_query", arguments: { name: "checkout errors", filter } }) as CallToolResult;
  assert.equal(created.isError, undefined, JSON.stringify(created));
  assert.equal(structured<{ saved_query: SavedQuery }>(created).saved_query.id, "sq-1");
  assert.equal(calls.at(-1)!.apiKey, "local-write-key");
  assert.equal(calls.at(-1)!.project, null, "unset QYL_PROJECT sends no project header");

  const taken = await client.callTool({ name: "save_query", arguments: { name: "taken", filter } }) as CallToolResult;
  assert.deepEqual(errorOf(taken), {
    code: "conflict",
    message: 'a saved query named "taken" already exists in this project',
    retryable: false,
    hint: "Choose another name, or a new idempotency_key only when a second object is intended.",
  });

  const nothing = await client.callTool({ name: "save_query", arguments: { id: "sq-1", etag: "\"1\"" } }) as CallToolResult;
  assert.equal(errorOf(nothing).code, "invalid_argument");
  const untagged = await client.callTool({ name: "save_query", arguments: { id: "sq-1", name: "renamed" } }) as CallToolResult;
  assert.match(errorOf(untagged).message, /^etag: is required with id/u);
  const before = calls.length;
  const badTag = await client.callTool({
    name: "save_query",
    arguments: { id: "sq-1", etag: "\"1\"\r\nX-Injected: 1", name: "renamed" },
  }) as CallToolResult;
  assert.equal(errorOf(badTag).code, "invalid_argument");
  assert.equal(calls.length, before, "an etag that is no HTTP field value never reaches the Collector");

  const renamed = await client.callTool({ name: "save_query", arguments: { id: "sq-1", etag: "\"1\"", name: "renamed" } }) as CallToolResult;
  assert.equal(renamed.isError, undefined, JSON.stringify(renamed));
  assert.match(JSON.stringify(renamed.content), /save_query: updated renamed \(traces\), id sq-1/u);
  assert.deepEqual(calls.at(-1)!.body, { name: "renamed" }, "omitted fields keep their value");

  const listed = await client.callTool({ name: "run_saved_query", arguments: { signal: "traces", limit: 5, cursor: "page-1" } }) as CallToolResult;
  assert.equal(listed.isError, undefined, JSON.stringify(listed));
  assert.deepEqual(
    calls.at(-1)!.body,
    { signal: "traces", limit: 5, cursor: "page-1" },
  );
  const page = structured<{ saved_queries: SavedQuery[]; has_more: boolean; next_cursor?: string }>(listed);
  assert.equal(page.saved_queries.length, 1);
  assert.equal(page.has_more, true);
  assert.equal(page.next_cursor, "page-2");
  assert.equal(calls.at(-1)!.path, "/api/v1/analysis/run_saved_query");

  const ran = await client.callTool({ name: "run_saved_query", arguments: { id: "sq-1" } }) as CallToolResult;
  assert.equal(ran.isError, undefined, JSON.stringify(ran));
  const run = structured<{ saved_query: SavedQuery; traces: unknown[] }>(ran);
  assert.equal(run.saved_query.id, "sq-1");
  assert.equal(run.traces.length, 1);
  assert.deepEqual(calls.at(-1)!.body, { id: "sq-1" }, "the call travels as given; the Collector applies the contract's default page size");

  const deleted = await client.callTool({ name: "delete_record", arguments: { kind: "saved_query", id: "sq-1", etag: "\"2\"" } }) as CallToolResult;
  assert.equal(deleted.isError, undefined, JSON.stringify(deleted));
  assert.equal(calls.at(-1)!.path, "/api/v1/saved-queries/sq-1");
  assert.equal(calls.at(-1)!.ifMatch, "\"2\"");

  const missing = await client.callTool({ name: "delete_record", arguments: { kind: "saved_query", id: "gone", etag: "\"2\"" } }) as CallToolResult;
  assert.deepEqual(errorOf(missing), {
    code: "not_found", message: "saved query not found: gone", retryable: false, resource_type: "saved_query", resource_id: "gone",
    hint: "Take ids only from earlier results: run_saved_query without id, get_trace, list_sessions or get_failure_context return ids and current etags.",
  });
});

test("a Collector key without the write or delete role surfaces as collector_unauthorized", async (context) => {
  liveCollector(context, false);
  mockCollector(context, () => problem(403, "Forbidden"));
  const client = await localClient(context);
  const created = await client.callTool({
    name: "annotate",
    arguments: { target: { kind: "session", session_id: SESSION_ID }, text: "note" },
  }) as CallToolResult;
  assert.deepEqual(errorOf(created), {
    code: "collector_unauthorized",
    message: "the Collector refused the change: the server's Collector key needs the write role",
    retryable: false,
    hint: "The Collector refused this server's credential; tell the qyl operator.",
  });
  const deleted = await client.callTool({
    name: "delete_record",
    arguments: { kind: "annotation", id: "ann-1", etag: "\"1\"" },
  }) as CallToolResult;
  assert.match(errorOf(deleted).message, /needs the delete role/u);
});


test("get_trace and list_sessions read the annotations back; a failed read leaves the telemetry result", async (context) => {
  liveCollector(context, false);
  const sessions = getTelemetryFixture().sessions.slice(0, 2);
  let annotationsUp = true;
  const calls = mockCollector(context, (call) => {
    if (call.path === "/api/v1/analysis/get_trace") return Response.json({ ...readContext(1), trace: toolTrace(TRACE), mode: "live", ...(annotationsUp ? { annotations: [annotation()] } : {}) });
    if (call.path === "/api/v1/analysis/list_sessions") return Response.json({ ...readContext(sessions.length), sessions, mode: "live", has_more: false, annotations: [...sessions].reverse().map(session => annotation({ id: `ann-${session.session_id}`, target: { kind: "session", session_id: session.session_id } })) });
    return problem(404, "Not Found");
  });
  const client = await localClient(context);

  const trace = await client.callTool({ name: "get_trace", arguments: { trace_id: TRACE.trace_id } }) as CallToolResult;
  assert.equal(trace.isError, undefined, JSON.stringify(trace));
  assert.deepEqual(structured<{ annotations: Annotation[] }>(trace).annotations, [annotation()]);
  assert.equal((calls.at(-1)!.body as { trace_id: string }).trace_id, TRACE.trace_id);

  const listed = await client.callTool({ name: "list_sessions", arguments: {} }) as CallToolResult;
  assert.equal(listed.isError, undefined, JSON.stringify(listed));
  assert.deepEqual(
    structured<{ annotations: Annotation[] }>(listed).annotations.map((note) => note.id),
    [`ann-${sessions[1]!.session_id}`, `ann-${sessions[0]!.session_id}`],
    "merged newest first across sessions",
  );

  annotationsUp = false;
  const degraded = await client.callTool({ name: "get_trace", arguments: { trace_id: TRACE.trace_id } }) as CallToolResult;
  assert.equal(degraded.isError, undefined, "the trace read itself succeeded");
  assert.equal(Object.hasOwn(degraded.structuredContent ?? {}, "annotations"), false);
  assert.equal(calls.at(-1)!.path, "/api/v1/analysis/get_trace");
});


test("creates without a supplied idempotency key are stable and project-scoped", async context => {
  liveCollector(context);
  const calls = mockCollector(context, () => Response.json({annotation:annotation(),idempotent_replay:false}));
  const owner = await hostedClient(context, OWNER, [QYL_MCP_SCOPE,QYL_MCP_WRITE_SCOPE]);
  const reviewer = await hostedClient(context, REVIEWER, [QYL_MCP_SCOPE,QYL_MCP_WRITE_SCOPE]);
  const args = {target:{kind:"trace",trace_id:TRACE.trace_id},text:"retry-safe bookmark"};
  await owner.callTool({name:"annotate",arguments:args});
  await owner.callTool({name:"annotate",arguments:args});
  await reviewer.callTool({name:"annotate",arguments:args});
  assert(calls[0]?.idempotencyKey);
  assert.equal(calls[0]?.idempotencyKey,calls[1]?.idempotencyKey);
  assert.notEqual(calls[0]?.idempotencyKey,calls[2]?.idempotencyKey);
});

test("failure update rejects ignore_until outside ignored and explains stale or inaccessible state", async context => {
  liveCollector(context);
  let status = 412;
  const calls = mockCollector(context, () => problem(status,"unsafe raw detail"));
  const owner = await hostedClient(context, OWNER, [QYL_MCP_SCOPE,QYL_MCP_WRITE_SCOPE]);
  const args = {failure_id:"fail_"+"a".repeat(64),etag:'"1"',status:"open"};
  const invalid = await owner.callTool({name:"update_failure_status",arguments:{...args,ignore_until:"2026-10-13T00:00:00Z"}});
  assert.equal(invalid.isError,true); assert.match(JSON.stringify(invalid),/status=ignored/u); assert.equal(calls.length,0);
  for (const [value,message] of [[412,/changed|current|etag/iu],[404,/not found|no longer/iu],[403,/write role/iu]] as const) {
    status=value;
    const result=await owner.callTool({name:"update_failure_status",arguments:args});
    assert.equal(result.isError,true); assert.match(JSON.stringify(result),message); assert.doesNotMatch(JSON.stringify(result),/unsafe raw detail/u);
    assert.equal(calls.at(-1)?.ifMatch,'"1"');
  }
});
