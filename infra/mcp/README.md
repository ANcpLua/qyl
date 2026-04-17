# qyl.mcp deployment ops guide

Status: **SCAFFOLD**. This directory documents what ops must do to make `mcp.qyl.info` live. No Terraform / Pulumi / Bicep has been committed yet — that is a separate spec once the host is chosen.

The container image is built from `src/qyl.mcp/Dockerfile` (Alpine-based, native-AOT-capable, non-root user).

## Pick a host

Three candidates, all Dockerfile-native:

| Host | Pros | Cons | Recommended when |
|---|---|---|---|
| **Fly.io** | Single Dockerfile deploy (`flyctl launch`), edge routing, automatic TLS via Let's Encrypt, cheap persistent tier, anycast ingress. | Manual cold-start tuning (`min_machines_running`). Fly Machines can take 2-10s to wake. | Solo operator wants "closest to `docker run` in the cloud" with minimal glue. |
| **Railway** | One-click GitHub deploy, managed Postgres/Redis if needed, $5/mo starter. | Cold-start on free tier. Less control over edge/region. | Prototyping and low-ops; trading control for speed. |
| **Azure Container Apps** | Enterprise-grade autoscale, integrates with Entra ID, KEDA events. Good fit if you already run other qyl services on Azure. | Cold-start SLO needs dedicated plan (`minReplicas >= 1`). CLI is heavier than `flyctl`. | You already have Azure tenancy and want to co-locate with existing resources. |

None of the above sign this off — the decision is yours. The runbook below is host-agnostic.

## TLS — do not hand-roll certs

- **Fly.io**: `fly certs add mcp.qyl.info` → Fly issues a Let's Encrypt cert and renews it. Verify with `fly certs show mcp.qyl.info`.
- **Railway**: add the custom domain in the project settings → Railway issues via Let's Encrypt.
- **Azure Container Apps**: use managed certs (public preview) or bind an App Gateway / Front Door with a managed cert.

The app does NOT terminate TLS. `ASPNETCORE_URLS=http://+:8080` in the container is correct — the host's edge layer handles 443 → 8080.

## DNS — CNAME to the host edge

```
mcp.qyl.info  CNAME  <host-specific-edge>
```

- Fly.io: `<app-name>.fly.dev`
- Railway: `<service>.up.railway.app` (or Railway-provided apex).
- Azure Container Apps: `<app>.<region>.azurecontainerapps.io`.

After the CNAME propagates, validate:

```bash
dig +short mcp.qyl.info
curl -sS https://mcp.qyl.info/healthz
# → Healthy
```

If the `/healthz` probe returns HTML instead of `Healthy`, your request is hitting a host landing page — the app isn't wired in. Check the host's routing console.

## Health probe

The container exposes `/healthz` (mapped in `QylMcpHttpHost.cs`). Host-side config:

- **Fly.io** (`fly.toml`):
  ```toml
  [http_service.checks]
    method = "GET"
    path = "/healthz"
    interval = "10s"
    timeout = "2s"
    grace_period = "30s"
  ```
- **Railway**: configure under Service → Settings → Healthcheck Path = `/healthz`.
- **ACA**: set probe in the YAML under `template.containers[].probes`.

## Cold-start SLO

For OAuth discovery UX, the first request after idle should finish in under ~3s. Set:

- **Fly.io**: `min_machines_running = 1` in `fly.toml`.
- **Railway**: paid tier ($5+), not free.
- **ACA**: `scale.minReplicas = 1`.

Without a warm instance, the Discovery hit (`/.well-known/oauth-protected-resource`) + token validation round-trip to Keycloak (`/realms/qyl/.well-known/openid-configuration` discovery) can stretch to 10s+, and Claude Code's timeout will fire.

## Environment variables (required in the container)

| Variable | Value | Why |
|---|---|---|
| `QYL_MCP_TRANSPORT` | `http` | Selects `QylMcpHttpHost` over stdio. |
| `QYL_MCP_PATH` | `/mcp` (default) | Must match the path clients use in `claude mcp add --transport http qyl https://mcp.qyl.info/mcp`. |
| `QYL_MCP_PUBLIC_URL` | `https://mcp.qyl.info` | Used by `ResolvePublicMcpUrl` so the OAuth metadata resource field matches the public URL even when the container sees an internal hostname. |
| `QYL_KEYCLOAK_AUTHORITY` | `https://keycloak.example.com/realms/qyl` | Turns on end-user JwtBearer validation. |
| `QYL_KEYCLOAK_AUDIENCE` | `qyl-mcp-user` | **Required.** Without this, the app fails fast at startup (see Spec 2 audience guard). |
| `QYL_COLLECTOR_URL` | `https://collector.internal:5100` | Backend the MCP tools call. Keep internal — not public. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://aspire-dashboard:4317` or your OTLP backend | qyl is observability; every request should be traced. |
| `ASPNETCORE_URLS` | `http://+:8080` (Dockerfile default) | Host edge terminates TLS. |

## Secrets

- Keycloak does NOT hand out client secrets for the public `qyl-mcp` client (PKCE only, no secret).
- The container needs no OAuth credentials itself — it only validates incoming tokens against the realm's JWKS endpoint (public).
- `QYL_MCP_TOKEN` (API key for the legacy Aspire-pattern header) is optional; leave unset to force OAuth.

## After deploy — smoke test from a clean machine

Use `mcr.microsoft.com/devcontainers/base:ubuntu` with only Claude Code installed, then:

```bash
claude mcp add --transport http qyl https://mcp.qyl.info/mcp
# → browser opens → Keycloak login → tokens stored → `/mcp` lists tools
curl -sS -H "Authorization: Bearer invalid-token" https://mcp.qyl.info/mcp -i | head
# → HTTP/2 401 + WWW-Authenticate: Bearer resource_metadata=...
```

If either of those fails, open `docs/mcp-oauth-runbook.md` and walk through the failure modes.

## What is NOT in scope here

- Terraform / Pulumi / Bicep. Adding IaC is a follow-up spec; today, the host CLI + this document are the source of truth.
- Multi-region. Single region is fine for v1.
- mTLS between `qyl.mcp` and `qyl.collector`. The collector uses `QYL_KEYCLOAK_AUDIENCE` + API key today; upgrading to mTLS is a separate thread.
