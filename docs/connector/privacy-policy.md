# qyl MCP Connector — Privacy Policy

> **Status:** Draft v1.0 — 2026-05-26. Prepared for Anthropic Connector
> Directory submission. Pending public hosting at the
> URL declared in `manifest.yaml` (`privacy_policy_url`).

This document describes what data the **qyl MCP connector** accesses on
your behalf when you connect it to Claude, where that data flows, how long
it lives, and how you can sever the connection.

## 1. What this connector is

The qyl MCP connector lets Claude talk to your qyl observability backend
(traces, logs, metrics, error issues, GenAI conversation captures). It
runs as an HTTP service called **qyl-collector** that you (or your
organization) operate. Claude connects to that service over MCP, mints an
opaque token via the Keycloak login flow, and uses the token to invoke
read and write tools defined under `services/qyl.mcp/Tools/`.

A single collector may serve multiple tenants on one host. The
`{tenant}` route segment selects the tenant namespace, but
authorization derives from the authenticated token. The collector MUST
compare the route `{tenant}` with the token-bound `TenantId`; mismatch
returns `403 Forbidden`. The connector does not aggregate data across
deployments, and no token may read another tenant's namespace.

## 2. Data accessed

When Claude invokes a tool through this connector, the connector reads
from the qyl-collector's local DuckDB store. The categories accessed:

| Category | Tables (under `services/qyl.collector/Storage/`) | What it is |
|---|---|---|
| Traces | `spans`, `span_links`, `span_events` | OTLP-emitted distributed-trace spans from your instrumented services. |
| Logs | `logs` | Structured log records exported via OTLP from your instrumented services. |
| Metrics | `metrics_*` | OTLP metrics export rollups (no raw scrape data). |
| Profiles | `profiles` | Continuous-profiling samples (when enabled). |
| Error issues | `errors`, `error_issues`, `error_categories` | Triaged error groupings derived from spans and logs. |
| GenAI conversations | `conversations`, `agent_runs` | Captures of LLM calls + agent tool invocations from your instrumented apps. |
| Tracker stats | `tracker_stats` | Per-service health and tracking rollups. |
| Workflows + fix runs | `workflows`, `fix_runs`, `autofix_steps` | Loom autofix pipeline state (when the Loom skill is enabled). |
| Auth state | `mcp_tokens`, `mcp_pkce_state` | The connector's own opaque-token store. See §5 for the token-lifecycle rules. |

The connector does **not** access:
- The host operating system (filesystem, environment, processes) — its
  surface is HTTP + DuckDB only.
- Source code or git history — Loom skills coordinate with the user's
  IDE through narrow JetBrains/LSP integrations; nothing is exfiltrated.
- Any third-party SaaS — see §3.

The complete tool surface (124 methods, classified as 93 read-only and
31 destructive) is enumerated in
[`tool-annotations-audit.md`](./tool-annotations-audit.md).

## 3. Data flow

```
Your apps  →  qyl-collector (your infra)  →  DuckDB (your infra)
                                            ↑
                                            └── MCP tool calls from Claude
```

- **Egress:** the only outbound HTTP from qyl-collector is to your
  Keycloak instance (for token introspection during refresh / revoke).
- **No third parties:** qyl-collector does not relay your data to
  Anthropic, qyl maintainers, or any analytics/telemetry vendor.
- **Transport:** Claude ↔ qyl-collector traffic uses MCP over HTTPS
  with Bearer-token authentication (opaque tokens minted via the OAuth
  2.1 + PKCE flow described in §5).

## 4. Retention

| Surface | Default retention | Configuration |
|---|---|---|
| Traces / Logs / Metrics | Per-table TTL configured via `qyl.configure_retention` tool. Default is 7 days for traces, 14 days for logs, 30 days for metric rollups. | Tenants change via that tool. |
| Error issues | 90 days from last-seen-at. | Same. |
| GenAI conversations | 30 days. | Same. |
| Opaque MCP tokens | Refresh expiry inherited from Keycloak (typically 30 days). Cleanup service deletes expired rows every 5 minutes. | `QYL_KEYCLOAK_AUTHORITY` / Keycloak realm config. |
| Revoked tokens | 7-day grace period before hard-delete (per Phase 1 cleanup). | Hard-coded in `McpTokenStore.CleanupExpiredAsync`. |
| PKCE state rows | 10 minutes (TTL on the row). | `KeycloakOptions.PkceStateTtl`. |

## 5. Token lifecycle

The connector mints **opaque MCP tokens** that act as bearer credentials
between Claude and qyl-collector. They are NOT JWT — they're 32-byte
random strings with a SHA-256 hash stored in `mcp_tokens.token_hash` for
constant-time lookup.

| Stage | What happens |
|---|---|
| Mint | User completes OAuth 2.1 + PKCE login at Keycloak. The collector receives an authorization code, exchanges it for Keycloak tokens, validates the id_token (signature, audience, issuer, lifetime, nonce binding), encrypts the refresh_token with AES-GCM, and persists a row in `mcp_tokens`. Returns the opaque token to Claude via URL fragment (never query, so it's not in proxy logs). |
| Use | Claude sends the opaque token as `Authorization: Bearer <opaque>` on every MCP request to `/mcp/{tenant}`. Collector looks up via constant-time hash compare and populates `HttpContext.User` with the user's sub + tenant claim. Authorization then compares the route `{tenant}` to the token-bound tenant claim — a mismatch is `403 Forbidden` (the route is addressing only; the token is the authority). |
| Refresh | `POST /auth/refresh` decrypts the stored Keycloak refresh, exchanges it at Keycloak, encrypts the rotated refresh, updates the row. Returns only the new `expires_at` to Claude — the underlying Keycloak refresh is never returned. |
| Revoke | `POST /auth/revoke` calls Keycloak's RFC 7009 revocation endpoint (best-effort) and sets `revoked_at` locally (source of truth). Idempotent — returns 204 even if the token doesn't exist (RFC 7009 §2.2 non-disclosure). |
| Cleanup | Background service deletes expired or grace-period-elapsed rows every 5 minutes. |

The Keycloak refresh token is encrypted at rest with AES-GCM (12-byte
random nonce per envelope, 16-byte authentication tag). The encryption
key is set via `QYL_TOKEN_ENCRYPTION_KEY` and never leaves the
collector's process memory.

## 6. User deletion

To revoke this connector's access:

1. **Immediate:** Call `POST /auth/revoke` with your opaque token, or
   click "Disconnect" in your Claude connector list. This sets
   `revoked_at` on your `mcp_tokens` row, making subsequent MCP calls
   fail with 401.
2. **Within 5 minutes:** the cleanup service starts the 7-day grace
   countdown on the revoked row.
3. **Within 7 days:** the row is hard-deleted from `mcp_tokens` and
   the encrypted refresh envelope is gone.

To delete your tenant's telemetry data outright, contact the operator
of your qyl-collector deployment — that requires direct access to the
collector's DuckDB store and is not exposed through the connector.

## 7. Security posture

- Opaque tokens never logged, never in error responses, never in URLs
  outside the final `#token=` fragment.
- PKCE `code_verifier` only on `IPkceStateStore.StoreAsync` + forwarded
  to Keycloak token endpoint; never logged.
- Refresh tokens encrypted at rest via AES-GCM.
- `ex.Message` never surfaced to clients — exception details go to
  structured logs only.
- Constant-time comparisons for nonce binding (`CryptographicOperations.
  FixedTimeEquals`).

## 8. Changes to this policy

Substantive changes will be reflected in this document's version header
and announced in the qyl release notes for the version that introduces
them.

## 9. Contact

Operational questions about your specific qyl-collector deployment go
to the team that runs it (this connector is self-hosted). Questions
about the qyl project itself: open an issue at
`https://github.com/O-ANcppLua/qyl` (or the relevant fork).
