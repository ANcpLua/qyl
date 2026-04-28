# qyl.mcp

MCP server for the qyl AI observability platform. It gives AI agents access to traces, logs, metrics, GenAI sessions,
build failures, anomaly analysis, and qyl's workflow/meta-agent tools over collector HTTP.

`qyl.mcp` now supports both:

- `stdio` transport for local MCP clients like Claude Code, Cursor, and desktop tools
- Streamable HTTP at `/mcp` for remote connectors such as Anthropic and OpenAI

## Install

```bash
dotnet tool install --global qyl.mcp
```

## Modes

### Local stdio mode

Default mode when no HTTP hosting environment is configured.

```bash
QYL_COLLECTOR_URL=http://localhost:5100 qyl.mcp
```

### Remote HTTP mode

Enable remote MCP hosting by setting `QYL_MCP_TRANSPORT=http`.

```bash
QYL_MCP_TRANSPORT=http \
QYL_COLLECTOR_URL=http://localhost:5100 \
ASPNETCORE_URLS=http://0.0.0.0:8080 \
qyl.mcp
```

Remote mode exposes:

- `/mcp` — Streamable HTTP MCP endpoint
- `/mcp` — Session-aware stream endpoint is the same Streamable HTTP root (`POST` initialize/request, `GET` session
  stream)
- `/mcp.json` — lightweight discovery metadata
- `/llms.txt` — human/LLM-readable server summary
- `/healthz` — container health endpoint

`ModelContextProtocol` 1.2.0 behavior is now reflected here:

- Legacy SSE endpoints (`/mcp/sse`, `/mcp/message`) are not exposed by default.
- Existing clients should target `/mcp` for Streamable HTTP discovery and transport.
- Legacy SSE compatibility must be handled through your client migration plan before this 1.2.0 transport default is
  adopted.

### Railway monorepo deployment

`qyl.mcp` is a separate Railway service from `qyl.collector`.

Use the dedicated config-as-code file:

```text
/services/qyl.mcp/railway.toml
```

That file points Railway at:

- `services/qyl.mcp/Dockerfile`
- `/healthz`

Do not reuse the repo-root `railway.toml` for the MCP service. The root file is for `qyl.collector`.

## Configuration

| Variable                     | Default                 | Purpose                                                                         |
|------------------------------|-------------------------|---------------------------------------------------------------------------------|
| `QYL_COLLECTOR_URL`          | `http://localhost:5100` | qyl collector base URL                                                          |
| `QYL_MCP_TRANSPORT`          | `stdio`                 | `stdio` or `http`                                                               |
| `QYL_MCP_PATH`               | `/mcp`                  | MCP HTTP route prefix                                                           |
| `QYL_MCP_PUBLIC_URL`         | derived from request    | Public base URL used in metadata                                                |
| `QYL_MCP_STATELESS`          | `false`                 | Enables stateless Streamable HTTP sessions                                      |
| `QYL_MCP_TOKEN`              | none                    | Outbound auth token used by qyl.mcp when calling qyl.collector                  |
| `QYL_KEYCLOAK_AUTHORITY`     | none                    | Keycloak/OIDC authority for collector auth and optional incoming JWT validation |
| `QYL_KEYCLOAK_CLIENT_ID`     | none                    | Client credentials for qyl.mcp -> qyl.collector                                 |
| `QYL_KEYCLOAK_CLIENT_SECRET` | none                    | Client credentials for qyl.mcp -> qyl.collector                                 |
| `QYL_KEYCLOAK_AUDIENCE`      | none                    | Optional audience for incoming bearer token validation in HTTP mode             |
| `PORT`                       | none                    | PaaS fallback for HTTP port binding                                             |

## Auth

- If `QYL_KEYCLOAK_AUTHORITY` is not configured, HTTP mode runs without host-facing auth.
- If `QYL_KEYCLOAK_AUTHORITY` is configured, HTTP mode requires bearer tokens and publishes MCP protected-resource
  metadata for OAuth-aware clients.
- Collector-facing auth remains separate: `qyl.mcp` still authenticates to `qyl.collector` using Keycloak client
  credentials or `QYL_MCP_TOKEN`.

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
    .WithHttpTransport()
    .UseQylMcpInstrumentation(ActivitySources.McpSource)
    .WithTools<MyTools>()
    .WithResources<MyResources>()
    .WithPrompts<MyPrompts>();
```

The wrapper instruments four call sites:

| Span                                | Kind   | Notes                                                                          |
|-------------------------------------|--------|--------------------------------------------------------------------------------|
| `mcp.receive {method}`              | Server | Per inbound JSON-RPC envelope. Carries `mcp.client.{name,version}` and `rpc.*` |
| `mcp.send`                          | Client | Per outbound JSON-RPC envelope. Carries `rpc.method` + `jsonrpc.request.id`    |
| `execute_tool {name}`               | Internal | Per `tools/call`. Carries `gen_ai.*` + `mcp.tool.name`                       |
| `mcp.resource.read {uri}`           | Internal | Per `resources/read`. Carries `mcp.resource.uri`                             |
| `mcp.prompt.get {name}`             | Internal | Per `prompts/get`. Carries `mcp.prompt.name`                                 |

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

### Composing with custom filters

`UseQylMcpInstrumentation` is additive — it registers its filters and returns the same `IMcpServerBuilder`. You can
chain `.WithMessageFilters` / `.WithRequestFilters` after it for business concerns (admin tool denial, scope
injection, response shaping). qyl.mcp itself does this for `McpAdminToolFilter` + `ConstraintInjector`; see
`services/qyl.mcp/Hosting/QylMcpServerRegistration.cs` for the canonical composition.

## Tools

Primary tool families:

- inspect: traces, spans, errors, logs, services, sessions
- health: storage and system context
- analytics: conversation and user analytics
- agent: `qyl.use_qyl`, `qyl.root_cause_analysis`, summaries, fix generation
- build: captured build failures
- anomaly: baselines and anomaly detection
- copilot / Claude Code / loom: AI workflow, triage, fix pipeline, and handoff tools

The exact exposed tool set is controlled by `QYL_SKILLS`.

## Remote client notes

- Anthropic and OpenAI remote connectors should point at the public `https://.../mcp` URL.
- If you are behind a proxy or ingress, set `QYL_MCP_PUBLIC_URL` so metadata uses the public origin rather than the
  internal container address.
- For OAuth-backed deployments, your identity provider must publish standard OIDC metadata and be reachable by the MCP
  client.

## Links

- [qyl repository](https://github.com/ANcpLua/qyl)
- [MCP specification](https://modelcontextprotocol.io)

## Capability discovery and HTTP metadata

The server now exposes two low-cost discovery tools so MCP hosts and operators can understand the available qyl surface
area before invoking broad investigation flows.

### `qyl.list_capabilities`

Use `qyl.list_capabilities` to enumerate the capability families currently enabled by `QYL_SKILLS`.

The tool returns:

- server name and version
- enabled skill families
- capability ids, titles, summaries, and tags
- optional primary tool names when `includeTools=true`

Supported filters:

- `skill`: narrow to one skill family such as `inspect`, `agent`, `loom`, `apps`, or `debug`
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

## HTTP transport metadata endpoints

When the server runs in HTTP mode, it exposes the MCP endpoint plus two metadata documents for hosts, operators, and
LLM-oriented clients.

### `/mcp.json`

`/mcp.json` returns a JSON manifest describing the live HTTP server surface.

The manifest includes:

- server name and version
- resolved MCP endpoint URL
- transport type
- auth mode
- summary text
- enabled tool-family labels
- capability count and capability summaries
- enabled tool count

This endpoint is intended for programmatic discovery and host bootstrapping.

### `/llms.txt`

`/llms.txt` returns a plain-text summary of the live HTTP server for LLM-facing clients.

The document includes:

- server summary
- resolved MCP endpoint URL
- auth mode
- enabled tool count
- enabled capability count
- discovery tool names
- a compact list of enabled capabilities

This endpoint is intended to give clients a fast human-readable and model-readable overview of the server without
requiring a full MCP session.

## Default endpoint behavior

When qyl runs in HTTP mode, the MCP endpoint defaults to `/mcp` unless `QYL_MCP_PATH` overrides it. The companion
metadata endpoints remain available at:

- `/mcp.json`
- `/llms.txt`

The root path `/` serves the landing page, and `/healthz` remains available for health checks.
