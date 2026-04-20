# MCP OAuth runbook — `mcp.qyl.info`

**Audience:** on-call operator debugging an OAuth failure against `qyl.mcp` in HTTP mode.
**Scope:** end-user `authorization_code + PKCE` flow (Claude Code / Claude Desktop → Keycloak → `mcp.qyl.info`). The
separate **service-to-service** `client_credentials` flow used by `KeycloakTokenProvider` (MCP → collector) is
intentionally out of scope — different code path, different failure modes.

## TL;DR commands

```bash
# 1. Is the server healthy?
curl -sSf https://mcp.qyl.info/healthz

# 2. Does OAuth discovery work? Both paths should return the same JSON.
curl -sS https://mcp.qyl.info/.well-known/oauth-protected-resource/mcp | jq
curl -sS https://mcp.qyl.info/.well-known/oauth-protected-resource | jq
# Both are served by ModelContextProtocol.AspNetCore 1.2.0 and return the same
# payload — no redirect, no alias in the qyl layer.

# 3. What does an invalid token look like from the server's POV?
curl -sS -i -H "Authorization: Bearer invalid-token" https://mcp.qyl.info/mcp | head

# 4. What does the Keycloak realm advertise?
curl -sS https://keycloak.example.com/realms/qyl/.well-known/openid-configuration | jq
```

## Expected discovery URLs

| URL                                                                        | Served by                                                                                      | Purpose                                                                                 |
|----------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------|
| `https://mcp.qyl.info/.well-known/oauth-protected-resource/mcp`            | `ModelContextProtocol.AspNetCore` 1.2.0 (resource-scoped route registered by `MapMcp("/mcp")`) | Returns `{resource, authorization_servers, bearer_methods_supported, ...}` per RFC 9728 |
| `https://mcp.qyl.info/.well-known/oauth-protected-resource`                | `ModelContextProtocol.AspNetCore` 1.2.0 (root fallback, also registered by `MapMcp`)           | Same payload as above. Confirmed empirically — no alias needed in `QylMcpHttpHost`.     |
| `https://keycloak.example.com/realms/qyl/.well-known/openid-configuration` | Keycloak (OIDC discovery)                                                                      | Returns `issuer`, `authorization_endpoint`, `token_endpoint`, `jwks_uri`, etc.          |
| `https://keycloak.example.com/realms/qyl/protocol/openid-connect/certs`    | Keycloak (JWKS)                                                                                | Fetched by `JwtBearer` to validate token signatures                                     |

**RFC 9728 compliance note:** the `resource` field in the protected-resource metadata MUST equal the URL the client
used. The MCP library's `ClientOAuthProvider.VerifyResourceMatch` enforces this. Because both root and resource-scoped
paths return the **same** payload with a single `resource` field set from `ResolvePublicMcpUrl`, the invariant holds;
this is also why `QYL_MCP_PUBLIC_URL` must be set in production.

## Expected invalid-token response

```
curl -sS -i -H "Authorization: Bearer invalid" https://mcp.qyl.info/mcp

HTTP/2 401
www-authenticate: Bearer resource_metadata="https://mcp.qyl.info/.well-known/oauth-protected-resource/mcp"
content-length: 0
```

Key headers:

- `401 Unauthorized` — any other code indicates a misconfigured auth pipeline.
- `WWW-Authenticate: Bearer resource_metadata="..."` — the `resource_metadata` URL is how MCP clients find the
  authorization server per RFC 9728.

If the response is `404` or `200`, the MCP endpoint isn't actually guarded — re-check `QYL_KEYCLOAK_AUTHORITY` and
`endpoint.RequireAuthorization()` in `QylMcpHttpHost.cs:66`.

## Structured log events

`src/qyl.mcp/Hosting/QylMcpServiceCollectionExtensions.cs` emits four events in the 4001-4099 range:

| EventId | Level       | When                                  | What to do                                                                                                                                                           |
|---------|-------------|---------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 4001    | Information | Protected-resource metadata requested | Healthy traffic — this fires once per cold client.                                                                                                                   |
| 4002    | Information | JWT validated successfully            | Healthy. `sub` = user's Keycloak id, `aud` = configured audience.                                                                                                    |
| 4003    | Warning     | JWT authentication failed             | Token invalid. Inspect `ExceptionType` / `ExceptionMessage` — `SecurityTokenInvalidAudienceException` means the token's `aud` doesn't match `QYL_KEYCLOAK_AUDIENCE`. |
| 4004    | Warning     | JWT valid but role check failed       | Token is well-formed but the principal lacks a required role. Authorization policy, not authentication.                                                              |

Query these in Aspire Dashboard / Seq / whatever log backend is wired:

```
EventId.Id >= 4001 AND EventId.Id <= 4099
```

## Known failure modes

### 1. "Audience validation must be explicit — refusing to start"

**Symptom:** container exits immediately on startup with `InvalidOperationException`.
**Cause:** `QYL_KEYCLOAK_AUTHORITY` is set but `QYL_KEYCLOAK_AUDIENCE` is empty.
**Fix:** set `QYL_KEYCLOAK_AUDIENCE=qyl-mcp-user` (or whatever audience the Keycloak mapper emits). This is the Spec 2
fail-fast guard — it's doing its job. Never unset the guard; add the audience.

### 2. DCR (Dynamic Client Registration) disabled

**Symptom:** Claude Code reports "dynamic client registration failed" when running `claude mcp add`.
**Cause:** the Keycloak realm doesn't accept `POST /realms/qyl/clients-registrations/openid-connect`.
**Fix:** enable DCR at the realm level.

```bash
kcadm.sh update realms/qyl -s 'attributes."clientRegistrationAllowed"=true'
# then verify
curl -sSf https://keycloak.example.com/realms/qyl/clients-registrations/openid-connect \
  -H 'Content-Type: application/json' \
  -d '{"client_name":"test","redirect_uris":["http://127.0.0.1/cb"]}' \
  | jq
```

If DCR is a hard "no" from your Keycloak admins, pre-register `qyl-mcp` manually per `infra/keycloak/qyl-realm.json` and
hand the `client_id` to each user. DCR is better, but not a blocker.

### 3. Loopback port wildcards rejected (Keycloak < 25)

**Symptom:** Claude Code gets to the Keycloak login page but returns "invalid redirect URI" after login.
**Cause:** older Keycloak rejects `http://127.0.0.1/*` as a wildcard on the port.
**Fix:** upgrade Keycloak to 25+, or register explicit loopback ports (`http://127.0.0.1:45678/callback`,
`http://127.0.0.1:45679/callback`, ...) — brittle but works.

### 4. Resource mismatch — `ClientOAuthProvider.VerifyResourceMatch` fails

**Symptom:** Claude Code discovers OAuth metadata but then errors out on token exchange with "resource URI mismatch".
**Cause:** the `resource` field in the protected-resource metadata doesn't exactly match the URL the client used.
Usually caused by `QYL_MCP_PUBLIC_URL` being unset when the container sees an internal hostname via `X-Forwarded-Host`.
**Fix:** set `QYL_MCP_PUBLIC_URL=https://mcp.qyl.info` in the container env. `McpHostOptions.ResolvePublicMcpUrl`
prefers this over `request.Host`.

### 5. JWKS fetch fails — silent handshake failure

**Symptom:** all tokens return 401 regardless of validity. Event 4003 repeatedly logs
`SecurityTokenSignatureKeyNotFoundException`.
**Cause:** the `qyl.mcp` container can't reach `https://keycloak.example.com/realms/qyl/protocol/openid-connect/certs`.
Common on private networks where the container has outbound restrictions.
**Fix:** whitelist the Keycloak host from the container's egress. Test with:

```bash
docker exec qyl-mcp wget -qO- https://keycloak.example.com/realms/qyl/protocol/openid-connect/certs | jq
```

### 6. Cold-start past SLO

**Symptom:** first `claude mcp add` against an idle instance times out.
**Cause:** host scaled to zero; cold-start is 5-15s.
**Fix:** `min_machines_running = 1` (Fly), paid tier (Railway), `minReplicas = 1` (ACA). See `infra/mcp/README.md`.

### 7. Token has no `aud` claim

**Symptom:** Event 4003 repeatedly logs `SecurityTokenInvalidAudienceException` with `aud` being null or a string that
isn't `qyl-mcp-user`.
**Cause:** the Keycloak audience mapper isn't attached to the client or isn't emitting to the access token.
**Fix:** re-check the mapper config in `infra/keycloak/qyl-realm.json`:

```
protocolMapper: oidc-audience-mapper
config:
  included.custom.audience: qyl-mcp-user
  access.token.claim: "true"   # must be true; id.token.claim can be false
```

## Incident drill

Once per quarter, run the full dance from a clean container to catch drift:

```bash
docker run --rm -it mcr.microsoft.com/devcontainers/base:ubuntu bash -lc '
  curl -fsSL https://claude.ai/install.sh | sh
  claude mcp add --transport http qyl https://mcp.qyl.info/mcp
'
```

If any step other than "browser login" is manual, something has regressed.

## When this runbook is wrong

When the symptoms don't match anything here: dump the auth-path logs (`EventId.Id between 4001 and 4099`) for the
failing request, inspect the JWT on `https://jwt.io` (paste only in dev — never in prod), compare claims to the Keycloak
realm config, and add the new failure mode to this runbook so the next operator benefits.
