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
- `/mcp.json` — lightweight discovery metadata
- `/llms.txt` — human/LLM-readable server summary
- `/healthz` — container health endpoint

### Railway monorepo deployment

`qyl.mcp` is a separate Railway service from `qyl.collector`.

Use the dedicated config-as-code file:

```text
/src/qyl.mcp/railway.toml
```

That file points Railway at:

- `src/qyl.mcp/Dockerfile`
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
