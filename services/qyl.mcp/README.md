# qyl-mcp-server

MCP tools over a live [qyl](https://github.com/ANcpLua/qyl) Collector — traces, logs, metrics,
sessions and CI runs — for agents investigating a running system. The Collector computes every
analysis; this server validates, scopes and frames the calls.

```bash
bunx qyl-mcp-server --stdio    # stdio MCP server
bunx qyl-mcp-server            # Streamable HTTP on 127.0.0.1:3001
```

`npx` and `pnpm dlx` also work when Bun 1.4 or newer is installed; the executable runs on Bun, the
library exports `qyl-mcp-server` and `qyl-mcp-server/auth` support Node 24. Point it at a Collector:

```bash
export QYL_COLLECTOR_URL=http://127.0.0.1:5100
export QYL_API_KEY='your-collector-key'   # omit for an unsecured local Collector
```

`QYL_API_KEY` is an *outgoing* Collector credential; it does not authenticate incoming MCP
clients, which is why the local server binds to loopback only.

The hosted instance is `https://mcp.qyl.at/mcp`. It is an OAuth 2.1 resource server: an
unauthenticated request answers `401` with a `WWW-Authenticate` challenge that requests
`qyl:read qyl:write qyl:delete` and points `resource_metadata` at its RFC 9728 document, which names
the authorization server `https://qyl-eu.eu.auth0.com/`; a stock MCP client follows it on its own.

Both use MCP TypeScript SDK v2: Streamable HTTP through `createMcpHandler` and stdio through
`serveStdio`. One factory serves revision `2026-07-28` and negotiates down for 2025-era clients
that send `initialize`. The HTTP server answers `/mcp`, `/healthz`, `/.well-known/openai-apps-challenge`
when configured and, with Auth0, `/.well-known/oauth-protected-resource[/mcp]` and
`/.well-known/oauth-authorization-server`; every other path, `/` included, is `404`, and a disallowed
Host or Origin is `403`.

## Tools

The committed [`tool-manifest.snapshot.json`](tool-manifest.snapshot.json) is the tool contract.

| Purpose | Tools |
| --- | --- |
| Investigate failures | `triage`, `get_failure_context`, `compare_groups`, `compare_traces`, `verify_fix` |
| Explore telemetry | `aggregate_telemetry`, `discover_attributes`, `get_agent_run`, `list_releases`, `get_service_map`, `check_telemetry_health`, `get_mcp_stats` |
| Read executions | `list_sessions`, `list_traces`, `get_trace`, `search_logs`, `ci_log` |
| Read metrics | `list_metrics`, `query_metric` |
| Saved questions | `run_saved_query` |
| Write project records | `annotate`, `save_query`, `delete_record`, `update_failure_status` |

Every result has one shape: `content[0]` is a one-line headline naming the top rows, their ids and
the tool to call next; `content[1]` is the whole result as compact JSON; `structuredContent` is the
same object. No tool advertises an `outputSchema`. A failure is `isError` with the contract's
`ToolError`, `{ error: { code, message, retryable, field, allowed, hint, ... } }`: `field` and
`allowed` name the argument to change and the values or argument names it accepts, whether the
input schema or the Collector rejected it; a `not_found` carries the Collector's `resource_type`,
`resource_id` and message, and `hint` names the next step, such as the tool that lists valid ids.

Read tools accept an absolute RFC 3339 time window or `lookback_minutes` and report the resolved
`window` and `coverage`: examined, matched and returned counts, whether results were left out
(`truncated`), whether the read held every record the answer could come from (`scan_complete`) and
observation time. Unknown counts are null. Filtering happens before pagination; reuse an opaque
`next_cursor` with the same filters to continue a page. Shared matchers support `eq`, `neq`, `in`,
`prefix`, `contains`, `exists`, `gt`, `gte`, `lt` and `lte`, with span, resource or log scope. Span
durations are milliseconds; metrics keep their recorded units. Histogram percentiles identify their
approximation, cumulative counter changes account for resets, and verification reports
insufficient traffic as pending rather than calling an unobserved failure fixed.

`get_trace` returns all spans, errors, errors with ancestors, or the critical path, with selected
attributes and correlated logs. `ci_log` reads telemetry from any CI following its emitter
convention; `service_prefix` defaults to `qyl-ci`. `get_mcp_stats` reports MCP request totals, the
numerator and denominator behind rates, failure classes, per-tool requests, errors and latency,
simultaneous server, transport and method breakdowns and, with `buckets: true`, the window's time
buckets that saw requests. The `qyl-investigate` skill says which
tool to start with; the server serves it through the MCP skills extension (`skills/list`,
`skills/get`, `skill://qyl/qyl-investigate/SKILL.md`), and the build copies it from `skills/` into
`dist/skills`.

Every read tool carries `readOnlyHint: true`. The write tools change only records in the caller's
Collector project: annotations (plain-text notes of at most 2000 characters on a trace, span,
session or failure, with optional labels; a bookmark is a note labelled `bookmark`), saved queries
(named filters for traces, logs or metrics) and the status of recorded failures (`open`,
`resolved`, `ignored`, assignee, ignore-until). `annotate` and `save_query` create a record
without an `id` and change the one an `id` names; `delete_record` removes one annotation or saved
query (`kind` is `annotation` or `saved_query`). Changes and deletes send the current `etag` as
`If-Match`: a stale etag is refused with `precondition_failed`, a duplicate saved-query name with
`conflict`. Creates reuse the stored result for 24 hours; without an explicit
`idempotency_key` the server derives one from the project, operation and request body. No tool
edits or deletes recorded telemetry; retention alone removes it.

`qyl:read` covers reads, `qyl:write` adds creation and updates, and `qyl:delete` permits
`delete_record`. A missing scope returns the OAuth step-up challenge `403`
`insufficient_scope` naming the scopes to request, in both protocol eras, before the handler runs.
Failed calls carry a stable code, retryability, the problem type and every rejected field when
available. Secrets are redacted before model text, structured content or durable
evidence. Every tool runs inside its request scope, forwarding cancellation to the Collector;
requested progress reports completion. Analysis reads require a live Collector; synthetic data
exists only in test fixtures.

## Hosted authentication

The hosted Auth0 profile is **CIMD or DCR + OAuth 2.1 Authorization Code with PKCE S256 + issuer
identification**; the server does not start unless Auth0's metadata advertises all of it, with
`private_key_jwt` (ChatGPT's CIMD client) and `none` (Claude's public CIMD client). The server never
inspects client metadata. Auth0 owns registration and token issuance; the server validates
resource-bound RFC 9068 access tokens (signature, issuer, resource audience, expiry and scopes) on
every MCP request: a request without credentials answers `401` with the bare challenge, no error
code (RFC 6750 §3.1); credentials in the query string answer `400 invalid_request`; a malformed,
invalid or expired token, or one issued for another resource, answers `401 invalid_token`;
insufficient scopes answer `403 insufficient_scope`. Every refusal carries the `WWW-Authenticate`
challenge, in the SDK's grammar, with the scopes to sign in for and the `resource_metadata` URL. Only the qyl production Auth0 issuer is accepted. Every request needs
`qyl:read`; a tool call that writes or deletes also checks `qyl:write` or `qyl:delete`, and its
step-up `403` names only the missing scope, so a client that re-authorizes for it must keep
`qyl:read`. `/.well-known/oauth-protected-resource/mcp` and
`/.well-known/oauth-protected-resource` describe the same `/mcp` resource.

Operating a hosted deployment:

1. Register the resource audience `<public-url>/mcp`, RS256 access tokens and the permissions
   `qyl:read`, `qyl:write` and `qyl:delete`.
2. Provision each client through its current CIMD document or DCR, keeping its actual callback and
   authentication method. When DCR is enabled, set `dynamic_client_registration_security_mode` to
   `strict`; the DCR endpoint is open to registration, so keep third-party API default permissions
   empty.
3. `qyl:read` exposes traces, logs, metrics, sessions and CI evidence; grant it only to intended
   clients and users. `qyl:write` and `qyl:delete` reach only annotations, saved queries and failure
   status in the caller's project, and the Collector key the server uses needs the Collector's
   `write` and `delete` roles.
4. Configure `MCP_PUBLIC_URL`, `MCP_BIND_HOST`, allowed hosts and origins, and the Collector URL and
   key. Non-loopback serving requires a public URL and hosted authorization.

`eng/auth0` in the repository records and reconciles the tenant's API scopes, named client grants
and mapped-user permissions.

Optional authorization extensions are selected explicitly:

```sh
MCP_AUTH_EXTENSIONS=enterprise-managed-authorization@1.0.0,oauth-client-credentials@1.0.0
```

`enterprise-managed-authorization@1.0.0` is stable and needs the JWT bearer and ID-JAG grant
profile; `oauth-client-credentials@1.0.0` is a draft and needs client credentials with
`private_key_jwt` (Auth0 Enterprise). Unknown versions and conflicting provider requirements are
rejected; neither replaces the core verifier. Embedders compose further modules through
`qyl-mcp-server/auth`.

The alternative `MCP_AUTH_PROVIDER=cloudflare-access` mode (with `MCP_ACCESS_TEAM_DOMAIN`,
`MCP_ACCESS_AUD` and `MCP_PUBLIC_URL`) validates `Cf-Access-Jwt-Assertion`: issuer, application
audience, expiry and subject, with RS256 signatures. Its assertion carries no qyl scopes, so it
grants the read tools only, and Auth0-backed Events cannot be enabled in it.

## Configuration

| Variable | Meaning |
| --- | --- |
| `QYL_COLLECTOR_URL` | Collector base URL; default `http://127.0.0.1:5100`. Also the OTLP base when set. |
| `QYL_API_KEY` | Collector credential for reads and OTLP; the write tools need its `write` and `delete` roles. Outgoing only. |
| `QYL_PROJECT` | Server-owned Collector project scope, for reads and for the server's own OTLP exports (`X-Qyl-Project` and resource `qyl.project.id`). Unset, the Collector's default project applies. Never accepted as tool input. |
| `MCP_COLLECTOR_PROJECTS` | Optional secret JSON mapping verified Auth0 subjects to project-bound Collector keys (below). |
| `MCP_PUBLIC_URL` | HTTPS origin, without a path, of a hosted deployment; turns on hosted authorization, sets the resource audience and the canonical `<origin>/mcp` URL, adds its host to the allowlists and disables the local execution record. |
| `MCP_BIND_HOST`, `PORT` | Listener address and port; default `127.0.0.1:3001`. |
| `MCP_ALLOWED_HOSTS`, `MCP_ALLOWED_ORIGIN_HOSTS` | Comma-separated request and browser-origin hostnames added to the public URL's hostname. |
| `MCP_AUTH_PROVIDER`, `MCP_AUTH_EXTENSIONS` | Authorization mode and optional extensions (above). |
| `MCP_EVENTS_STORE` | Persistent Events subscription file; unset disables Events. |
| `MCP_EVENTS_AUTH0_CLIENT_ID`, `MCP_EVENTS_AUTH0_CLIENT_SECRET` | Management API application with `read:users`, `read:clients`, `read:client_grants`, `read:grants`; resolves CIMD identities and keeps Events access checks current without storing subscriber tokens. |
| `MCP_EVENTS_POLL_MS` | Collector polling interval for Events, at least 5000 ms; default 30000 ms. |
| `OPENAI_APPS_CHALLENGE` | Text served at `/.well-known/openai-apps-challenge`; `404` while blank. |
| `QYL_OTLP_ENDPOINT` | Optional OTLP base for self-telemetry; signal-specific `OTEL_*` endpoints take precedence. |
| `QYL_MCP_TELEMETRY=0` | Disable MCP spans, metrics and operation logs. |
| `QYL_MCP_NATIVE_STATE_PATH` | Location of the local execution record; default `~/.qyl/mcp-native-executions.json`. |

With `MCP_COLLECTOR_PROJECTS` set, only the verified SDK `AuthInfo.extra.subject` selects the
project; tool inputs and request metadata cannot override it. An enabled map rejects unmapped
callers and never falls back to `QYL_API_KEY`; missing, malformed, duplicate or conflicting entries
fail validation. It needs the hosted Auth0 configuration. Shape (placeholder values):

```json
[
  { "project": "default", "apiKey": "OWNER_COLLECTOR_KEY", "subjects": ["auth0|owner-example"] },
  { "project": "review", "apiKey": "REVIEW_COLLECTOR_KEY", "subjects": ["auth0|reviewer-example"] }
]
```

Tool-call OTLP exports use the mapped project's credential; an unassigned caller gets no fallback
export destination. Project keys are redacted from text and structured results.

## MCP Events

On authenticated Auth0 deployments with `MCP_EVENTS_STORE`, clients can subscribe to `trace.error`,
optionally filtered by `service_name`. The server verifies callbacks, signs deliveries, checks
ongoing authorization, expires subscriptions and cancels delivery on unsubscribe or confirmed
revocation. Subscription state is bounded and holds no OAuth tokens or copied telemetry.

## Execution records and telemetry

A local server (`--stdio`, or HTTP without `MCP_PUBLIC_URL`) records each inbound `tools/call` —
tool name, timing, status, error type and a server-generated ID, never arguments, metadata, result
bodies or error messages — atomically in the execution record, keeping the latest 1,000. A hosted
deployment records nothing to disk. A completed or failed record that cannot be written never
changes the call's outcome: the tool's result or error is returned, a structured `warn` line goes
to stderr and the operation span carries `error.type=evidence_persistence_failed`; a record that
cannot be written before the tool runs refuses the call. OTLP exports carry operation timing,
status and trace correlation, without argument or result bodies. The server span names the
negotiated `mcp.protocol.version`, the `jsonrpc.request.id` and the caller (`user_agent.name` and
`user_agent.version` from the client's `clientInfo`, `user_agent.original` from the HTTP
`User-Agent`). A call the caller has to change — arguments the schema or the Collector rejects,
an id that does not exist, missing access, a stale etag, an unknown tool — leaves the span's
status unset and carries no `error.type`, as the MCP conventions count such a request no failure of
the server; an unreachable or timed-out Collector, a Collector 5xx or a contract mismatch is an
`ERROR` span with `error.type=tool_error`. A stdio server flushes its exports when the connection
closes, so short sessions are recorded too.

## Checks

From `services/qyl.mcp` after `bun install --frozen-lockfile && bun run build`:
`bun run test:built` (unit tests), `bun run smoke` (stdio transport), `bun run smoke:quiet`
(an 18-second quiet response over HTTP), `bun run test:answer-key` (seeded OTLP through a fresh
Collector, then every analysis check through MCP) and `bun run smoke:projects` (two real
Collector projects, every tool, both protocol eras). The last two use the Collector's Release
build in `artifacts/bin/qyl.collector/release/`; `QYL_COLLECTOR_DLL` selects another build.
`QYL_BIN=/path/to/qyl bun run smoke:live` calls every tool against a Collector started by an
installed qyl tool. `mise run hosted-eval` checks the hosted server with a real login
([eval/](eval/)).

## Release notes

[CHANGELOG.md](https://github.com/ANcpLua/qyl/blob/main/CHANGELOG.md)
