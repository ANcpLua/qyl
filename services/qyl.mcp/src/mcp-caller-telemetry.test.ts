import assert from "node:assert/strict";
import test from "node:test";
import { Client, StreamableHTTPClientTransport } from "@modelcontextprotocol/client";
import { createMcpHandler } from "@modelcontextprotocol/server";
import * as z from "zod/v4";
import { describeMcpOperationLog, describeMcpOperationMetric, describeMcpOperationSpan } from "./mcp-semconv.js";
import { NativeExecutionRuntime, type NativeExecutionTelemetry } from "./native-execution.js";
import { SecretRedactor } from "./secret-redactor.js";
import { createServer } from "./server.js";
import type { McpOperationStartInput } from "./telemetry.js";

function capturing(starts: McpOperationStartInput[]): NativeExecutionTelemetry {
  return {
    startOperation(input) {
      starts.push(structuredClone(input));
      return { run: (operation) => operation(), end: () => undefined };
    },
  };
}

for (const era of ["modern", "legacy"] as const) {
  test(`a ${era} call over HTTP starts its span with the revision, request id and caller`, async (context) => {
    const starts: McpOperationStartInput[] = [];
    const runtime = new NativeExecutionRuntime(undefined, { telemetry: capturing(starts), redactor: new SecretRedactor() });
    const handler = createMcpHandler(() => {
      const server = createServer({ nativeExecution: runtime, transport: "streamable_http" });
      server.registerTool("fixture.caller", { inputSchema: z.object({}) }, async () => ({ content: [{ type: "text", text: "ok" }] }));
      return server;
    });
    context.after(() => handler.close());
    const client = new Client({ name: "claude-code", version: "2.1.303" },
      era === "modern" ? { versionNegotiation: { mode: { pin: "2026-07-28" } } } : {});
    context.after(() => client.close().catch(() => undefined));
    await client.connect(new StreamableHTTPClientTransport(new URL("https://mcp.qyl.at/mcp"), {
      fetch: (url, init) => handler.fetch(new Request(url, init)),
      requestInit: { headers: { "user-agent": "claude-code/2.1.303 (cli)" } },
    }));
    assert.equal(client.getProtocolEra(), era);

    await client.callTool({ name: "fixture.caller", arguments: {} });
    assert.equal(starts.length, 1);
    const [started] = starts;
    assert.equal(started!.protocolVersion, client.getNegotiatedProtocolVersion());
    assert.equal(started!.protocolVersion, era === "modern" ? "2026-07-28" : "2025-11-25");
    assert.equal(typeof started!.jsonRpcRequestId, "number");
    assert.equal(started!.userAgent, "claude-code/2.1.303 (cli)");
    assert.equal(started!.sessionId, undefined, "neither a 2026-07-28 nor a stateless 2025 request belongs to a session");
    if (era === "modern") {
      // The envelope names the client on every request.
      assert.equal(started!.clientName, "claude-code");
      assert.equal(started!.clientVersion, "2.1.303");
    } else {
      // A stateless 2025 tools/call reaches a fresh instance that never saw initialize.
      assert.equal(started!.clientName, undefined);
    }
  });
}

test("the server span carries the MCP conventions' caller attributes; the metric keeps only the revision", () => {
  const input = {
    method: "tools/call", toolName: "triage", transport: "streamable_http" as const, startTimeMs: 1_000, endTimeMs: 1_250,
    protocolVersion: "2026-07-28", jsonRpcRequestId: 7, sessionId: "191c4850af6c49e08843a3f6c80e5046",
    clientName: "openai-mcp", clientVersion: "1.0.0", userAgent: "openai-mcp/1.0.0 (Codex)",
  };
  const span = describeMcpOperationSpan(input).attributes;
  assert.equal(span["mcp.protocol.version"], "2026-07-28");
  assert.equal(span["jsonrpc.request.id"], "7");
  assert.equal(span["mcp.session.id"], "191c4850af6c49e08843a3f6c80e5046");
  assert.equal(span["user_agent.name"], "openai-mcp");
  assert.equal(span["user_agent.version"], "1.0.0");
  assert.equal(span["user_agent.original"], "openai-mcp/1.0.0 (Codex)");
  assert.equal(span["network.transport"], "tcp");
  assert.equal(span["gen_ai.operation.name"], "execute_tool");

  const log = describeMcpOperationLog(input).attributes;
  assert.equal(log["mcp.protocol.version"], "2026-07-28");
  assert.equal(log["user_agent.original"], "openai-mcp/1.0.0 (Codex)");

  const metric = describeMcpOperationMetric(input).attributes;
  assert.equal(metric["mcp.protocol.version"], "2026-07-28", "the semconv metric is recommended to carry the revision");
  for (const key of ["jsonrpc.request.id", "mcp.session.id", "user_agent.name", "user_agent.version", "user_agent.original"]) {
    assert.equal(metric[key], undefined, `${key} would give every caller its own series`);
  }
});
