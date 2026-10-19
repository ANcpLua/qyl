import assert from "node:assert/strict";
import { createServer as createHttpServer, type IncomingMessage, type Server } from "node:http";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import protobuf from "protobufjs";
import { Client } from "@modelcontextprotocol/client";
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";
import { CONTRACT_REVISION } from "./contracts/generated/api.js";
import { PROJECT_SCOPE_HEADER } from "./contracts/generated/headers.js";

const protoDirectory = fileURLToPath(new URL("../testdata/otlp/", import.meta.url));
const protoRoot = new protobuf.Root();
protoRoot.resolvePath = (_origin, target) => resolve(protoDirectory, target);
await protoRoot.load(["trace", "metrics", "logs"].map(signal =>
  `opentelemetry/proto/collector/${signal}/v1/${signal}_service.proto`));
const exportTypes = new Map([
  ["/v1/traces", protoRoot.lookupType("opentelemetry.proto.collector.trace.v1.ExportTraceServiceRequest")],
  ["/v1/metrics", protoRoot.lookupType("opentelemetry.proto.collector.metrics.v1.ExportMetricsServiceRequest")],
  ["/v1/logs", protoRoot.lookupType("opentelemetry.proto.collector.logs.v1.ExportLogsServiceRequest")],
]);

interface Attribute { key: string; value: { stringValue?: string } }
interface Export { route: string; project: string | undefined; body: Record<string, unknown> }
const attribute = (attributes: Attribute[], key: string) => attributes.find(item => item.key === key)?.value.stringValue;

async function body(request: IncomingMessage): Promise<Buffer> {
  const chunks: Buffer[] = [];
  for await (const chunk of request) chunks.push(Buffer.from(chunk));
  return Buffer.concat(chunks);
}

async function listen(server: Server): Promise<string> {
  await new Promise<void>((done) => server.listen(0, "127.0.0.1", done));
  const address = server.address();
  assert(address && typeof address !== "string");
  return `http://127.0.0.1:${address.port}`;
}

async function stop(server: Server): Promise<void> {
  server.closeAllConnections();
  await new Promise<void>((done, fail) => server.close(error => error ? fail(error) : done()));
}

test("a stdio session exports its telemetry to QYL_PROJECT on close and the process exits by itself", async (context) => {
  const exports: Export[] = [];
  // One local port serves both roles, as `qyl up` does: the Collector API and its OTLP receiver.
  const collector = createHttpServer((request, response) => {
    void (async () => {
      const payload = await body(request);
      const type = exportTypes.get(request.url ?? "");
      if (type !== undefined) {
        exports.push({
          route: request.url!, project: request.headers[PROJECT_SCOPE_HEADER.toLowerCase()] as string | undefined,
          body: type.toObject(type.decode(payload), { longs: String, bytes: String }),
        });
        response.writeHead(200, { "content-type": "application/x-protobuf" }).end();
        return;
      }
      if (request.url === "/health") {
        response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify({ status: "healthy", contract_revision: CONTRACT_REVISION }));
        return;
      }
      if (request.url === "/api/v1/analysis/list_metrics") {
        const now = new Date().toISOString();
        response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify({
          window: { start_time: now, end_time: now },
          coverage: { examined: 0, matched: 0, returned: 0, truncated: false, scan_complete: true, as_of: now },
          items: [], has_more: false,
        }));
        return;
      }
      response.writeHead(404).end();
    })().catch(() => response.writeHead(500).end());
  });
  const url = await listen(collector);
  const home = await mkdtemp(join(tmpdir(), "qyl-stdio-lifecycle-"));
  context.after(async () => {
    await stop(collector);
    await rm(home, { recursive: true, force: true });
  });

  const transport = new StdioClientTransport({
    command: "bun",
    args: [fileURLToPath(new URL("./main.js", import.meta.url)), "--stdio"],
    // The inherited environment keeps the toolchain's own lookup working (a version
    // manager's bun shim reads HOME); every variable that steers qyl is set here.
    env: {
      ...Object.fromEntries(Object.entries(process.env).filter((entry): entry is [string, string] =>
        entry[1] !== undefined && !/^(QYL_|OTEL_|MCP_)/u.test(entry[0]))),
      QYL_COLLECTOR_URL: url,
      QYL_PROJECT: "answer-key",
      QYL_MCP_NATIVE_STATE_PATH: join(home, "native.json"),
    },
    stderr: "pipe",
  });
  const client = new Client({ name: "stdio-lifecycle", version: "1.0.0" }, { versionNegotiation: { mode: { pin: "2026-07-28" } } });
  await client.connect(transport);
  const result = await client.callTool({ name: "list_metrics", arguments: {} });
  assert.equal(result.isError, undefined, JSON.stringify(result));
  // Batches flush every 5 s; a short session's span still waits in the batch.
  assert.equal(exports.filter(item => item.route === "/v1/traces").length, 0);

  // Closing ends stdin. The transport gives the server 2 s before it signals; a server that
  // released its keep-alive handles and flushed exits well before that.
  const closing = Date.now();
  await client.close();
  const elapsed = Date.now() - closing;
  assert(elapsed < 1_900, `the server exited ${elapsed} ms after end-of-file; the transport had to signal it`);

  assert.deepEqual([...new Set(exports.map(item => item.route))].sort(), ["/v1/logs", "/v1/metrics", "/v1/traces"]);
  assert(exports.every(item => item.project === "answer-key"));
  const traces = exports.find(item => item.route === "/v1/traces")!.body as unknown as {
    resourceSpans: Array<{ resource: { attributes: Attribute[] }; scopeSpans: Array<{ spans: Array<{ name: string; attributes: Attribute[] }> }> }> };
  const resourceSpans = traces.resourceSpans[0]!;
  assert.equal(attribute(resourceSpans.resource.attributes, "qyl.project.id"), "answer-key");
  const span = resourceSpans.scopeSpans[0]!.spans.find(item => item.name === "tools/call list_metrics");
  assert(span, "the list_metrics span was exported");
  assert.equal(attribute(span.attributes, "mcp.protocol.version"), "2026-07-28");
  assert.equal(attribute(span.attributes, "network.transport"), "pipe");
  assert.equal(attribute(span.attributes, "user_agent.name"), "stdio-lifecycle");
});
