# qyl.mcp

MCP server for the qyl AI observability platform. It gives AI agents access to traces, logs, metrics, GenAI sessions,
build failures, anomaly analysis, and qyl's workflow/meta-agent tools over collector HTTP.

`qyl.mcp` ships as a stdio MCP server for local clients (Claude Code, Cursor, Codex CLI, IDE integrations, and any
other host that launches an MCP server as a child process). Remote HTTP MCP hosting has moved to `qyl.collector`; see
that service for the OAuth-protected `/mcp/{tenant}` endpoint.

## Install

```bash
dotnet tool install --global qyl.mcp
```

## Run

```bash
QYL_COLLECTOR_URL=http://localhost:5100 qyl-mcp
```

The MCP host launches `qyl-mcp` as a child process and communicates over stdin/stdout. Configure your host (Claude
Code, Cursor, etc.) to spawn `qyl-mcp` with the collector URL in the environment.

## Configuration

| Variable                     | Default                 | Purpose                                                                    |
|------------------------------|-------------------------|----------------------------------------------------------------------------|
| `QYL_COLLECTOR_URL`          | `http://localhost:5100` | qyl collector base URL                                                     |
| `QYL_MCP_TOKEN`              | none                    | Outbound auth token used by qyl.mcp when calling qyl.collector             |
| `QYL_KEYCLOAK_AUTHORITY`     | none                    | Keycloak/OIDC authority for collector-facing auth (client-credentials)     |
| `QYL_KEYCLOAK_CLIENT_ID`     | none                    | Client credentials for qyl.mcp -> qyl.collector                            |
| `QYL_KEYCLOAK_CLIENT_SECRET` | none                    | Client credentials for qyl.mcp -> qyl.collector                            |
| `QYL_SKILLS`                 | `all`                   | Comma-separated skill filter (`inspect,health,analytics,agent,...`) or `all` |
| `QYL_SERVICE`                | none                    | Scope all tool queries to a single service name                            |
| `QYL_SESSION`                | none                    | Scope all tool queries to a single session id                              |

## Auth

`qyl.mcp` authenticates outbound to `qyl.collector` using either a shared token (`QYL_MCP_TOKEN`) or Keycloak
client-credentials (`QYL_KEYCLOAK_*` trio). Inbound auth and the public-facing OAuth flow live in `qyl.collector`.

## Monitor your MCP server with qyl

Wrap any .NET MCP server with one extension call to get full visibility into JSON-RPC traffic, tool/resource/prompt
performance, and the silent errors MCP hides from you. The facade lives in `Qyl.Instrumentation.Instrumentation.Mcp`
and produces OpenTelemetry spans on the canonical `qyl.mcp` ActivitySource — the same source `AddQylServiceDefaults`
already registers, so spans flow through your existing OTel pipeline without extra wiring.

```csharp
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.Mcp;

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .UseQylMcpInstrumentation(ActivitySources.McpSource)
    .WithTools<MyTools>()
    .WithResources<MyResources>()
    .WithPrompts<MyPrompts>();
```

The wrapper instruments four call sites:

| Span                                | Kind     | Notes                                                                          |
|-------------------------------------|----------|--------------------------------------------------------------------------------|
| `mcp.receive {method}`              | Server   | Per inbound JSON-RPC envelope. Carries `mcp.client.{name,version}` and `rpc.*` |
| `mcp.send`                          | Client   | Per outbound JSON-RPC envelope. Carries `rpc.method` + `jsonrpc.request.id`    |
| `execute_tool {name}`               | Internal | Per `tools/call`. Carries `gen_ai.*` + `mcp.tool.name`                         |
| `mcp.resource.read {uri}`           | Internal | Per `resources/read`. Carries `mcp.resource.uri`                               |
| `mcp.prompt.get {name}`             | Internal | Per `prompts/get`. Carries `mcp.prompt.name`                                   |

### Capturing arguments and results (PII-gated)

Tool arguments and outputs are off by default — they may contain user PII. Opt in explicitly:

```csharp
.UseQylMcpInstrumentation(ActivitySources.McpSource, options =>
{
    options.RecordInputs = true;   // tools/call arguments + prompts/get arguments
    options.RecordOutputs = true;  // tools/call result text + resources/read content size + prompts/get message count
})
```

Captured strings are truncated to `MaxAttributeValueLength` (4_000 chars by default) so they stay below collector
limits. Disable in production if your tools handle credentials, customer data, or other sensitive content.

### Errors that MCP silently swallows

The official MCP SDK handles tool errors by returning a `CallToolResult` with `IsError = true` instead of throwing.
The facade catches both shapes — thrown exceptions are recorded with `AddException` and `ActivityStatusCode.Error`,
and `IsError = true` results set the same status with the message `"Tool returned IsError"` so silent failures show
up in Sentry/qyl issue lists alongside thrown errors.

## Tools

Primary tool families:

- inspect: traces, spans, errors, logs, services, sessions
- health: storage and system context
- analytics: conversation and user analytics
- agent: `qyl.use_qyl`, `qyl.root_cause_analysis`, summaries, fix generation
- build: captured build failures
- anomaly: baselines and anomaly detection
- loom: AI workflow, triage, fix pipeline, and handoff tools
- debug: Rider debugger control + LSP code intelligence (gated on `QYL_SKILLS=debug`)

The exact exposed tool set is controlled by `QYL_SKILLS`.

## Capability discovery

The server exposes two low-cost discovery tools so MCP hosts and operators can understand the available qyl surface
area before invoking broad investigation flows.

### `qyl.list_capabilities`

Use `qyl.list_capabilities` to enumerate the capability families currently enabled by `QYL_SKILLS`.

The tool returns:

- server name and version
- enabled skill families
- capability ids, titles, summaries, and tags
- optional primary tool names when `includeTools=true`

Supported filters:

- `skill`: narrow to one skill family such as `inspect`, `agent`, `loom`, or `debug`
- `tag`: narrow to one domain such as `traces`, `errors`, `logs`, `metrics`, `genai`, or `debugger`
- `includeTools`: include the primary tool names behind each capability

This is the recommended entrypoint when a host needs to decide whether to stay in direct tool mode or escalate to
`qyl.use_qyl`.

### `qyl.get_capability_guide`

Use `qyl.get_capability_guide` with a capability id returned by `qyl.list_capabilities`.

The guide returns qyl-specific operating context for that capability, including:

- primary identifiers to carry through the workflow
- recommended starting tools
- recommended follow-up tools
- scoping hints
- telemetry evidence hints
- related capabilities
- enabled tool references with title, skill family, and mutability flags

This is intended to be the deterministic replacement for hard-coded client-side capability lore.

## Links

- [qyl repository](https://github.com/ANcpLua/qyl)
- [MCP specification](https://modelcontextprotocol.io)
