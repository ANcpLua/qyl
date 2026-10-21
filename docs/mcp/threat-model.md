# MCP server threat model

The trust boundaries of the MCP server in `services/qyl.mcp` (server name `qyl.mcp`). Source paths
are relative to `services/qyl.mcp/src/`. How the server is configured and what each tool does is in
[services/qyl.mcp/README.md](../../services/qyl.mcp/README.md).

## System and assets

The server exposes read tools over the telemetry in a qyl Collector and write tools for the
annotations, saved queries and failure status users keep in a project. No tool changes or deletes
recorded telemetry. MCP TypeScript SDK v2 serves the HTTP and stdio protocol; the executable runs on
Bun.

Assets: each project's traces, logs, metrics and sessions; the annotations and saved queries users
create; the Collector credentials; the verified authorization context. Source, tool schemas and the
served skill are public on purpose; telemetry is not.

## Entry points and trust boundaries

| Boundary | Required behavior |
| --- | --- |
| HTTP request → tool call | The configured verifier validates the bearer token and the endpoint's required scope on every request. Discovery metadata and `401` challenges name the same protected resource. Client-supplied identity metadata is untrusted. A write tool also needs `qyl:write` (create, change) or `qyl:delete` (`delete_record`); the SDK's `scopeChallenge` refuses a token without it with `403 insufficient_scope` before the handler, in both protocol eras. stdio carries no token, so no scope check applies there. See `authorization.ts`, `oauth.ts`, `main.ts`, `tool-scopes.ts`. |
| Write tool → Collector | A write reaches only the verified subject's project, through the same access resolution as reads; no argument, `_meta` field or header names a project. A change or delete sends the caller's `etag` as `If-Match` and refuses a value that is no valid HTTP field value. The Collector key needs its `write` and `delete` roles. See `collector-resources.ts`, `write-tools.ts`, `analysis-tools.ts`. |
| Verified subject → Collector access | With `MCP_COLLECTOR_PROJECTS`, only the verified subject selects host-managed project credentials. An invalid map or an unknown subject fails closed, every tool call stays in that scope, and cross-project ids are refused. See `collector-access.ts`, `request-scope.ts`, `collector.ts`. |
| Tool-call telemetry → OTLP destination | With a project map, a tool call's telemetry is exported only with the verified subject's project and key, through a bounded exporter pipeline per configured project; an unassigned subject has no fallback destination. A hosted server keeps no execution record on disk. Arguments, results and identity hints stay out of operation telemetry; setup failures are reported without credentials. See `native-execution.ts`, `telemetry.ts`. |
| Collector result → model and diagnostics | Telemetry and attributes may be hostile or carry secrets. Redaction holds on every result channel and observability path, except explicitly captured HTTP-header and RPC-metadata string arrays, which pass through unchanged because their capture was the producer's choice; other fields and malformed capture shapes keep normal redaction. Telemetry is never executed as instructions, and error details never expose credentials. |

## Deployment assumptions

- Public hosted access is authenticated: Auth0 authenticates hosted users, and the alternative
  Cloudflare Access mode keeps its own verifier. Local stdio and loopback HTTP have an explicit,
  different trust model and are no anonymous production bypass. Without verified OAuth there is no
  scope to check, and the operator's Collector key decides whether writes succeed.
- Synthetic telemetry exists only in tests and is never returned in place of a failed read.
- The unauthenticated challenge asks for `qyl:read qyl:write qyl:delete`. That grants nothing by
  itself: the authorization server and the user's consent decide the token's scopes. A step-up
  challenge lists only the scopes the requested operation lacks.
- Without `MCP_COLLECTOR_PROJECTS` the server runs as one project by design. A present but invalid
  map, or an unmatched subject, never falls back to it.
- Host configuration holds the project assignments and credentials. This project does not claim to
  store no user-related data.
- The server serves MCP `2026-07-28` and 2025-era clients through SDK v2's own legacy serving, not
  through SDK v1. Explicit protocol pins in conformance tests are expected.
- The reviewer account stays confined to sample telemetry. No account identifiers, key values,
  access tokens or reviewer credentials belong in this file.

## Evidence

The authorization, OAuth, Collector-access, redaction and tool-metadata tests cover these
boundaries. The transport smoke runs both protocol eras, and `smoke:projects` runs every tool for
two real Collector projects in both eras and checks that foreign ids and objects are refused. Tests
are evidence for the behavior they exercise, not for deployment settings.

Sources: [MCP authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization),
[MCP security best practices](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices)
and [OpenAI MCP authentication](https://developers.openai.com/plugins/build/auth).
