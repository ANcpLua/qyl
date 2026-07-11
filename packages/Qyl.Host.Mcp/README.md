# Qyl.Host.Mcp

MCP support for the qyl runner, as an **opt-in package** — the engine
([`Qyl.Host`](../Qyl.Host)) has no MCP assumption.

```csharp
using Qyl.Host;
using Qyl.Host.Mcp;

var builder = QylAppBuilder.Create(args);

// SDK-spawned stdio server, already-running HTTP server, or in-process server:
builder.AddMcpStdio("everything", "npx", ["-y", "@modelcontextprotocol/server-everything"]);
builder.AddMcpHttp("remote", new Uri("http://127.0.0.1:8321"));
builder.AddMcpInProc("qyl-telemetry", transport => McpServer.Create(transport, serverOptions));

await builder.Build().RunAsync();
```

What it adds:

- **`McpHandshakeProbe`** — readiness is `initialize` + `tools/list` (the same
  two-step gate as qyl.mcp's TS orchestrator), not an HTTP health route. The
  connected client parks in `McpClientRegistry`.
- **Connection-only resource kinds** — `stdio` (the SDK transport owns the child
  process), `http`, and `inproc` (hosted in the runner over an in-memory stream
  pair; the factory receives the server-side transport because the C# SDK couples
  server and transport at `McpServer.Create`).
- **`/runner/mcp` passthrough** — `GET {name}/tools`, `POST {name}/tools/call`,
  `POST {name}/resources/read` on the runner API (404 unknown, 409 not ready,
  502 call failed), via the engine's `IQylRunnerRequestHandler` seam.
- **`McpTelemetry`** — one CLIENT span per passthrough call (`mcp.*` +
  `gen_ai.tool.name` keys, spec-hex ids) batched as OTLP/JSON to
  `QYL_OTLP_ENDPOINT/v1/traces`. `QYL_MCP_TELEMETRY=0` disables;
  `QYL_MCP_RECORD_INPUTS=1` / `QYL_MCP_RECORD_OUTPUTS=1` gate content capture.

Built on the official [`ModelContextProtocol.Core`](https://www.nuget.org/packages/ModelContextProtocol.Core) SDK.
