# MCP OAuth

How `qyl.mcp` authenticates end-user clients (Claude Code, Claude Desktop) against Keycloak over HTTP transport, the discovery endpoints it exposes, the audit events it emits, and the failure modes an operator is likely to hit.

Scope: the **authorization_code + PKCE** flow from an MCP client to `mcp.qyl.info`. The separate **client_credentials** flow used by `KeycloakTokenProvider` for qyl.mcp → qyl.collector calls is a different code path and is not covered here.

## Authentication Flow

```
MCP client                     qyl.mcp                        Keycloak
 (Claude Code)                  (mcp.qyl.info)                 (realms/qyl)
 │                              │                              │
 │  GET /mcp                    │                              │
 │  (no token)                  │                              │
 │─────────────────────────────>│                              │
 │                              │                              │
 │  401 + WWW-Authenticate:     │                              │
 │  resource_metadata=".../mcp" │                              │
 │<─────────────────────────────│                              │
 │                              │                              │
 │  GET /.well-known/           │                              │
 │  oauth-protected-resource/mcp│                              │
 │─────────────────────────────>│                              │
 │                              │                              │
 │  { resource, authorization_  │                              │
 │  servers: [keycloak], ... }  │                              │
 │<─────────────────────────────│                              │
 │                              │                              │
 │  GET /.well-known/openid-configuration (issuer)             │
 │────────────────────────────────────────────────────────────>│
 │                              │                              │
 │  POST /clients-registrations/openid-connect (DCR)           │
 │────────────────────────────────────────────────────────────>│
 │                              │                              │
 │  PKCE dance: authorization_endpoint → token_endpoint        │
 │<═══════════════════════════════════════════════════════════>│
 │                              │                              │
 │  GET /mcp                    │                              │
 │  Authorization: Bearer ...   │                              │
 │─────────────────────────────>│                              │
 │                              │  Fetch JWKS + validate sig   │
 │                              │─────────────────────────────>│
 │                              │<─────────────────────────────│
 │  200 OK (MCP handshake)      │                              │
 │<─────────────────────────────│                              │
```

The `WWW-Authenticate` header's `resource_metadata` URL is how clients discover the authorization server per RFC 9728. `ModelContextProtocol.AspNetCore` 1.2.0 registers both the root and resource-scoped well-known paths from `MapMcp("/mcp")`.

## Discovery Endpoints

| URL | Served by | Payload |
|-----|-----------|---------|
| `/.well-known/oauth-protected-resource/mcp` | MCP library 1.2.0 | RFC 9728 `{ resource, authorization_servers, bearer_methods_supported }` |
| `/.well-known/oauth-protected-resource` | MCP library 1.2.0 (root fallback) | Same payload — no qyl alias needed |
| `/realms/qyl/.well-known/openid-configuration` | Keycloak | `issuer`, `authorization_endpoint`, `token_endpoint`, `jwks_uri` |
| `/realms/qyl/protocol/openid-connect/certs` | Keycloak | JWKS for `JwtBearer` signature validation |

The `resource` field in the protected-resource metadata MUST equal the URL the client used (`ClientOAuthProvider.VerifyResourceMatch`). Because both well-known paths return the same payload with `resource` set from `ResolvePublicMcpUrl`, the invariant holds — this is why `QYL_MCP_PUBLIC_URL` is mandatory behind a proxy.

## Environment Variables

| Variable | Required | Purpose |
|----------|----------|---------|
| `QYL_KEYCLOAK_AUTHORITY` | HTTP mode | Issuer URL (e.g. `https://keycloak.example.com/realms/qyl`). Unset = auth disabled, HTTP mode is open. |
| `QYL_KEYCLOAK_AUDIENCE` | with authority | Expected `aud` claim. **Fail-fast**: setting authority without audience aborts startup. |
| `QYL_MCP_PUBLIC_URL` | behind proxy | Public origin for metadata (`https://mcp.qyl.info`). Required when `X-Forwarded-Host` differs from request host. |
| `QYL_KEYCLOAK_CLIENT_ID` | qyl.mcp → qyl.collector | Client credentials flow (service-to-service, not end-user). |
| `QYL_KEYCLOAK_CLIENT_SECRET` | qyl.mcp → qyl.collector | Paired with client id. |

## Invalid-Token Response

```
$ curl -sS -i -H "Authorization: Bearer invalid" https://mcp.qyl.info/mcp

HTTP/2 401
www-authenticate: Bearer resource_metadata="https://mcp.qyl.info/.well-known/oauth-protected-resource/mcp"
content-length: 0
```

Any other status code means the pipeline is misconfigured — re-check `QYL_KEYCLOAK_AUTHORITY` and `endpoint.RequireAuthorization()` in `Hosting/QylMcpHttpHost.cs`.

## Audit Events

Structured log events in the 4001–4099 range, emitted from `Hosting/QylMcpServiceCollectionExtensions.cs`:

| EventId | Level | When | What to check |
|---------|-------|------|---------------|
| 4001 | Info | Protected-resource metadata requested | Healthy — fires once per cold client. |
| 4002 | Info | JWT validated successfully | `sub` = Keycloak user id, `aud` = audience. |
| 4003 | Warn | JWT authentication failed | `ExceptionType` pinpoints cause (see failure modes). |
| 4004 | Warn | JWT valid but role check failed | Well-formed token; principal lacks required role. Authorization, not authentication. |

Query from Aspire Dashboard / Seq / any OTel log backend:

```
EventId.Id >= 4001 AND EventId.Id <= 4099
```

## Failure Modes

### Audience validation refuses to start

**Symptom:** container exits on boot with `InvalidOperationException: Audience validation must be explicit`.
**Cause:** `QYL_KEYCLOAK_AUTHORITY` is set but `QYL_KEYCLOAK_AUDIENCE` is empty.
**Fix:** set `QYL_KEYCLOAK_AUDIENCE=qyl-mcp-user` (or whatever the Keycloak mapper emits). Do not disable the guard.

### DCR (Dynamic Client Registration) rejected

**Symptom:** `claude mcp add` reports "dynamic client registration failed".
**Cause:** realm doesn't accept `POST /realms/qyl/clients-registrations/openid-connect`.
**Fix:** enable at realm level, or pre-register `qyl-mcp` manually from `infra/keycloak/qyl-realm.json`.

```bash
kcadm.sh update realms/qyl -s 'attributes."clientRegistrationAllowed"=true'
```

### Loopback port wildcards rejected

**Symptom:** login succeeds but Keycloak returns "invalid redirect URI".
**Cause:** Keycloak < 25 rejects `http://127.0.0.1/*` wildcards on port.
**Fix:** upgrade to Keycloak 25+, or register explicit loopback ports (`:45678`, `:45679`, …).

### Resource URI mismatch

**Symptom:** OAuth discovery succeeds, token exchange errors "resource URI mismatch".
**Cause:** `resource` in metadata doesn't exactly match the client's request URL. Usually `QYL_MCP_PUBLIC_URL` is unset behind a proxy.
**Fix:** `QYL_MCP_PUBLIC_URL=https://mcp.qyl.info`. `McpHostOptions.ResolvePublicMcpUrl` prefers it over `request.Host`.

### JWKS fetch fails silently

**Symptom:** every token returns 401. Event 4003 repeatedly logs `SecurityTokenSignatureKeyNotFoundException`.
**Cause:** container egress can't reach `/realms/qyl/protocol/openid-connect/certs`.
**Fix:** allowlist the Keycloak host. Verify from the container:

```bash
docker exec qyl-mcp wget -qO- https://keycloak.example.com/realms/qyl/protocol/openid-connect/certs | jq
```

### Token has no `aud` claim

**Symptom:** Event 4003 logs `SecurityTokenInvalidAudienceException`, `aud` is null or unexpected.
**Cause:** Keycloak audience mapper is not attached, or not emitting to the access token.
**Fix:** `infra/keycloak/qyl-realm.json` mapper:

```
protocolMapper: oidc-audience-mapper
config:
  included.custom.audience: qyl-mcp-user
  access.token.claim: "true"
```

### Cold-start past SLO

**Symptom:** first `claude mcp add` against an idle instance times out.
**Cause:** host scaled to zero.
**Fix:** `min_machines_running = 1` (Fly), paid tier (Railway), `minReplicas = 1` (ACA). See `infra/mcp/README.md`.

## Smoke Test

From a clean container, end-to-end dance once per quarter to catch drift:

```bash
docker run --rm -it mcr.microsoft.com/devcontainers/base:ubuntu bash -lc '
  curl -fsSL https://claude.ai/install.sh | sh
  claude mcp add --transport http qyl https://mcp.qyl.info/mcp
'
```

Any step other than the browser login prompt being manual = regression.

## Key Files

| File | Purpose |
|------|---------|
| `src/qyl.mcp/Hosting/QylMcpHttpHost.cs` | Registers `MapMcp("/mcp")` + `RequireAuthorization()` |
| `src/qyl.mcp/Hosting/QylMcpServiceCollectionExtensions.cs` | `JwtBearer` wiring, audit event sources (4001–4099) |
| `src/qyl.mcp/Auth/McpAuthOptions.cs` | `QYL_KEYCLOAK_*` env var names + validation |
| `src/qyl.mcp/Auth/McpAuthHandler.cs` | Authentication handler |
| `src/qyl.mcp/Auth/McpAuthExtensions.cs` | Fail-fast audience guard |
| `src/qyl.mcp/Auth/KeycloakTokenProvider.cs` | Service-to-service flow (out of scope for this doc) |
| `src/qyl.mcp/McpHostOptions.cs` | `ResolvePublicMcpUrl`, `QYL_MCP_PUBLIC_URL` |
| `infra/keycloak/qyl-realm.json` | Realm + client + audience mapper config |
| `infra/mcp/README.md` | Deploy targets + cold-start tuning |

## When This Doc Is Wrong

If the symptom doesn't match any failure mode above: dump the 4001–4099 range for the failing request, paste the JWT into `https://jwt.io` (dev only — never a prod token), compare claims against the realm config, and add the new mode here.
