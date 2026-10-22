# qyl-mcp-server

The MCP server of [qyl](https://qyl.at), an analytical engine built for AI agents. Its 24 tools
investigate the traces, logs, metrics, sessions and CI runs recorded in a qyl Collector and answer
typed questions: what changed, why a request failed or was slow, whether a fix held. The Collector
computes every answer; this server validates, scopes and frames each call and adds no analysis of
its own. Telemetry is data, not instructions.

## Connect

**Hosted.** Add `https://mcp.qyl.at/mcp` as a remote MCP server (Streamable HTTP). Sign-in is OAuth
through qyl's Auth0 tenant `qyl-eu` (issuer `https://qyl-eu.eu.auth0.com/`) with PKCE. The client
registers itself with a Client ID Metadata Document (CIMD) or Dynamic Client Registration (DCR), so
no client ID is needed. An unauthenticated request answers `401` with a `WWW-Authenticate`
challenge whose `resource_metadata` names
`https://mcp.qyl.at/.well-known/oauth-protected-resource/mcp`; an MCP client follows it on its own.
The server maps the signed-in account to its Collector project; no tool argument, `_meta` field or
header selects one.

**Local.** Start a Collector with the qyl tool (`dotnet tool install --global qyl && qyl up`), then
the server:

```bash
bunx qyl-mcp-server --stdio    # MCP over stdio
bunx qyl-mcp-server            # Streamable HTTP at http://127.0.0.1:3001/mcp
```

The executable runs on Bun 1.4 or newer; `npx` and `pnpm dlx` work when Bun is installed. The
default `QYL_COLLECTOR_URL`, `http://127.0.0.1:5100`, is the Collector `qyl up` starts. Set
`QYL_API_KEY` for a Collector that requires a key. That key authenticates this server to the
Collector, not MCP clients to this server, which is why the local HTTP server listens on loopback
only.

**Protocol.** Both transports serve MCP `2026-07-28` and 2025-era clients that open with
`initialize`, from one server factory: MCP TypeScript SDK v2 `createMcpHandler` with its default
`legacy: 'stateless'`, and `serveStdio`.

## Tools

The committed [`tool-manifest.snapshot.json`](https://github.com/ANcpLua/qyl/blob/main/services/qyl.mcp/tool-manifest.snapshot.json)
is the tool contract: 20 read tools with `readOnlyHint: true` and 4 write tools with
`destructiveHint: true`.

| Group | Tools |
| --- | --- |
| Failure investigation | `triage`, `get_failure_context`, `verify_fix` |
| Executions | `list_traces`, `get_trace`, `list_sessions`, `get_agent_run`, `ci_log` |
| Logs and metrics | `search_logs`, `list_metrics`, `query_metric` |
| Comparison and discovery | `aggregate_telemetry`, `compare_groups`, `compare_traces`, `discover_attributes` |
| Operations | `list_releases`, `get_service_map`, `check_telemetry_health`, `get_mcp_stats` |
| Saved questions | `run_saved_query` |
| Writes | `annotate`, `save_query`, `update_failure_status`, `delete_record` |

Start with `triage` over a `lookback_minutes` that covers the incident, take ids only from earlier
results, then call `get_failure_context` and `verify_fix`. The `qyl-investigate` skill carries the
detail; the server serves it through the MCP skills extension (`io.modelcontextprotocol/skills`:
`skills/list`, `skills/get`, and `resources/read` of `skill://qyl/qyl-investigate/SKILL.md`).

**Results.** `content[0]` is a one-line headline naming the top rows, their ids and the tool to
call next; `content[1]` is the whole result as compact JSON, and `structuredContent` is the same
object. No tool declares an `outputSchema`.

**Errors.** A failure is `isError` with the contract's `ToolError`,
`{ error: { code, message, retryable, field, allowed, hint, ... } }`. `field` and `allowed` name the
argument to change and what it accepts, whether the input schema or the Collector refused it; a
`not_found` names `resource_type` and `resource_id`, and `hint` names the next step, such as the
tool that lists valid ids.

**Windows and coverage.** Read tools take `start_time` and `end_time` (RFC 3339) or
`lookback_minutes` (default 60, at most 43200) and return the resolved `window` and a `coverage`:
`examined`, `matched` and `returned` counts (null when unknown, never an invented zero),
`truncated`, `scan_complete` (false means a partial answer) and `as_of`. Pass a result's `window`
back as `start_time` and `end_time` to keep one window. Filters apply before pagination, and
`next_cursor` with the same filters continues a page. Matchers take `eq`, `neq`, `in`, `prefix`,
`contains`, `exists`, `gt`, `gte`, `lt` and `lte`, on `span`, `resource` or `log` scope. `verify_fix`
answers `pending` while fewer than `min_requests` requests or `min_minutes` minutes have passed
since the change; it never calls an unobserved failure fixed.

## Writes and scopes

The write tools change only records in the caller's Collector project; no tool edits or deletes
telemetry, which retention alone removes.

- `annotate`: a plain-text note of at most 2000 characters on a trace, span, session or failure,
  with up to 10 labels and a classification (`note`, `hypothesis`, `finding` or `rejected`).
- `save_query`: a named filter over traces, logs or metrics. `run_saved_query` runs it, or lists
  the saved queries when called without an `id`.
- `update_failure_status`: a failure's status (`open`, `resolved` or `ignored`), its assignee and
  `ignore_until`.
- `delete_record`: deletes one annotation or saved query (`kind` is `annotation` or `saved_query`).

`annotate` and `save_query` create a record without an `id` and change the one an `id` names. A
change or delete sends the record's current `etag` as `If-Match`: a stale one answers
`precondition_failed`, a duplicate saved-query name `conflict`. A create repeated within 24 hours
answers the record the first one created; without an `idempotency_key` the server derives one from
the project, the operation and the body.

Every request needs `qyl:read`. `annotate`, `save_query` and `update_failure_status` also need
`qyl:write`, and `delete_record` needs `qyl:delete`. A call without it answers
`403 insufficient_scope` naming only the missing scope, before the tool runs and in both protocol
eras; a client that re-authorizes for it keeps `qyl:read`.

## Hosted mode

`MCP_PUBLIC_URL` makes the server an OAuth resource server for `<MCP_PUBLIC_URL>/mcp`:

- It accepts only access tokens from the qyl Auth0 issuer, and starts only if the issuer's
  metadata advertises CIMD, a registration endpoint, PKCE S256, the authorization code flow, issuer
  identification, and both `private_key_jwt` and `none` token endpoint authentication.
- Every request needs an RFC 9068 access token, checked for its RS256 signature, issuer,
  audience, expiry and scopes. No token answers `401` with the challenge; a token in the query
  string `400 invalid_request`; a malformed, invalid, expired or foreign token `401 invalid_token`;
  missing scopes `403 insufficient_scope`. Every refusal carries the `WWW-Authenticate` challenge
  with the scopes to request and the `resource_metadata` URL.
- `/.well-known/oauth-protected-resource` and `/.well-known/oauth-protected-resource/mcp` describe
  the `/mcp` resource, and `/.well-known/oauth-authorization-server` serves the issuer's metadata.

In either mode the HTTP server also answers `/healthz`: `ok`, or `degraded` while the Collector is
unreachable or serves another contract revision, when tools answer `collector_unavailable` or
`collector_contract_mismatch`. Every other path, `/` included, is `404`, and a disallowed `Host` or
`Origin` is `403`.

`eng/auth0` in the [repository](https://github.com/ANcpLua/qyl/tree/main/eng/auth0) holds the
tenant's configuration and the script that reconciles it.

`MCP_COLLECTOR_PROJECTS` maps verified Auth0 subjects to project-bound Collector keys. Only the
verified subject selects the project. A mapped server refuses unmapped callers and never falls back
to `QYL_API_KEY`; a malformed, duplicate or conflicting entry stops it at startup. The shape, with
placeholder values:

```json
[
  { "project": "default", "apiKey": "OWNER_COLLECTOR_KEY", "subjects": ["auth0|owner-example"] },
  { "project": "review", "apiKey": "REVIEW_COLLECTOR_KEY", "subjects": ["auth0|reviewer-example"] }
]
```

`MCP_AUTH_PROVIDER=cloudflare-access`, with `MCP_ACCESS_TEAM_DOMAIN` and `MCP_ACCESS_AUD`, replaces
Auth0 with Cloudflare Access: the server validates the `Cf-Access-Jwt-Assertion` header (RS256,
team issuer, application audience, expiry and subject). The assertion carries no qyl scopes, so only
the read tools work there. `MCP_AUTH_EXTENSIONS` turns on optional authorization extensions with
Auth0, by exact version: `enterprise-managed-authorization@1.0.0` (stable; JWT bearer grant with
the ID-JAG profile) and `oauth-client-credentials@1.0.0` (draft; client credentials with
`private_key_jwt`). An unknown version or a requirement the issuer does not meet stops the server
at startup. `qyl-mcp-server/auth` exports these building blocks for an embedding host.

## Configuration

| Variable | Meaning |
| --- | --- |
| `QYL_COLLECTOR_URL` | Collector base URL; default `http://127.0.0.1:5100`. |
| `QYL_API_KEY` | Collector key for tool calls and the server's own OTLP export; the write tools need its `write` and `delete` roles. |
| `QYL_PROJECT` | Collector project for reads and the server's own telemetry, sent as `X-Qyl-Project` and resource `qyl.project.id`; unset, the Collector's default project. Never a tool argument. |
| `MCP_PUBLIC_URL` | HTTPS origin, without a path, of a hosted deployment. Turns on hosted authorization, sets the resource `<origin>/mcp`, adds the host to the allowlists and turns off the local execution record. |
| `MCP_BIND_HOST`, `PORT` | Listener address and port; default `127.0.0.1:3001`. A non-loopback address needs `MCP_PUBLIC_URL`. |
| `MCP_ALLOWED_HOSTS`, `MCP_ALLOWED_ORIGIN_HOSTS` | Comma-separated `Host` and `Origin` hostnames allowed beside the public URL's. |
| `MCP_COLLECTOR_PROJECTS` | Subject-to-project map of a hosted Auth0 deployment (above). |
| `MCP_AUTH_PROVIDER`, `MCP_ACCESS_TEAM_DOMAIN`, `MCP_ACCESS_AUD` | `auth0` (default) or `cloudflare-access` with its team origin and application audience (above). |
| `MCP_AUTH_EXTENSIONS` | Comma-separated authorization extensions with their versions (above). |
| `OPENAI_APPS_CHALLENGE` | Token served at `/.well-known/openai-apps-challenge`; unset, the path is `404`. |
| `QYL_OTLP_ENDPOINT` | OTLP base URL for the server's own telemetry; falls back to `QYL_COLLECTOR_URL`. A signal-specific `OTEL_EXPORTER_OTLP_*_ENDPOINT` takes precedence. |
| `QYL_MCP_TELEMETRY` | `0` turns off the server's own spans, metrics and logs. |
| `QYL_MCP_NATIVE_STATE_PATH` | Local execution record; default `~/.qyl/mcp-native-executions.json`. |

## Telemetry

The server exports its own spans, metrics and logs over OTLP as service `qyl.mcp`; `triage` and
`check_telemetry_health` leave that service out unless `service_name` names it. Server spans carry
the negotiated `mcp.protocol.version`, the `jsonrpc.request.id` and the caller's `clientInfo` and
`User-Agent`, never arguments or results. A call the caller has to change (a rejected argument, an
unknown id, missing access, a stale `etag`, an unknown tool) leaves the span status unset; an
unreachable or timed-out Collector, a Collector `5xx` or a contract mismatch is an `ERROR` span with
`error.type=tool_error`. A stdio server flushes its telemetry when the connection closes.

A local server (stdio, or HTTP without `MCP_PUBLIC_URL`) also keeps an execution record of its
latest 1,000 tool calls: tool name, timing, status, error type and a server-generated id, never
arguments, metadata, results or error messages. A hosted server keeps no execution record.

## License

qyl is free: this package is under the MIT license
([LICENSE](https://github.com/ANcpLua/qyl/blob/main/services/qyl.mcp/LICENSE)). Source:
https://github.com/ANcpLua/qyl. Changes per version:
[GitHub releases](https://github.com/ANcpLua/qyl/releases).
