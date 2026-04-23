# Shipping qyl on Railway

qyl runs as independent Railway services inside one Railway project (`qyl`, project id
`5eaa4020-71d9-4828-89d3-316cb188529e`, environment `production`). Each service has its
`RAILWAY_DOCKERFILE_PATH` pointed at the matching `services/qyl.*/Dockerfile`; the repo
root is the build context for every service (Dockerfiles `COPY packages/ internal/ eng/`).

## Service matrix

| Railway service   | Dockerfile                           | Internal DNS                   | Exposed                                                  | Talks to                                              |
|-------------------|--------------------------------------|--------------------------------|----------------------------------------------------------|-------------------------------------------------------|
| `qyl-api`         | `services/qyl.collector/Dockerfile`  | `qyl-api.railway.internal`     | `qyl-api-production.up.railway.app` + OTLP gRPC/HTTP TCP | DuckDB volume at `/data`                              |
| `qyl-mcp`         | `services/qyl.mcp/Dockerfile`        | `qyl-mcp.railway.internal`     | `mcp.qyl.info` (custom) + `qyl-mcp-production.up.railway.app` | `qyl-api` (currently via public URL — see Gotchas)    |
| `qyl-dashboard`   | `services/qyl.dashboard/Dockerfile`  | `qyl-dashboard.railway.internal` | Public HTTP (nginx)                                    | `qyl-api` (public URL baked into `VITE_API_BASE_URL`) |
| `qyl-loom`        | `services/qyl.loom/Dockerfile`       | `qyl-loom.railway.internal`    | Private HTTP only (no public domain)                     | `qyl-api` (private URL) + OpenAI                      |

> **Naming note.** The collector service is called **`qyl-api`** on Railway, not
> `qyl-collector`. The in-repo project name is still `qyl.collector`; Railway's
> `RAILWAY_SERVICE_NAME` is what you type in `${{service.VAR}}` references.

## One-time setup

```sh
# From the repo root
railway login
railway link                                                  # attach to the existing qyl project

# Either let Railway auto-detect each Dockerfile via RAILWAY_DOCKERFILE_PATH (preferred),
# or point at each service root explicitly on first push:
railway up --service qyl-api        --detach  --path services/qyl.collector
railway up --service qyl-mcp        --detach  --path services/qyl.mcp
railway up --service qyl-dashboard  --detach  --path services/qyl.dashboard
railway up --service qyl-loom       --detach  --path services/qyl.loom
```

After the first deploy, set `RAILWAY_DOCKERFILE_PATH` on each service (see the Variables
table) so future deploys pick the right Dockerfile without needing `--path`.

## Variables

Shown below are the variables actually set on the project today (per the Railway UI) plus
the env vars Railway suggests and we should add.

### `qyl-api` (collector) — 13 service variables

| Variable                       | Value                                         | Purpose                                                 |
|--------------------------------|-----------------------------------------------|---------------------------------------------------------|
| `ASPNETCORE_URLS`              | `http://+:8080`                               | Kestrel bind for the public HTTP API.                   |
| `QYL_PORT`                     | `8080`                                        | Mirrors the HTTP port for qyl-internal consumers.       |
| `QYL_GRPC_PORT`                | `4317`                                        | OTLP/gRPC bind (secondary TCP endpoint on Railway).     |
| `QYL_OTLP_PORT`                | `4318`                                        | OTLP/HTTP bind.                                         |
| `QYL_OTLP_AUTH_MODE`           | `ApiKey`                                      | Enforces OTLP auth in prod. Disable only in dev.        |
| `QYL_OTLP_PRIMARY_API_KEY`     | *(secret)*                                    | Primary key accepted by the OTLP ingestion path.        |
| `QYL_TOKEN`                    | *(secret)*                                    | Shared admin/bearer token for non-OTLP endpoints.       |
| `QYL_DATA_PATH`                | `/data/qyl.duckdb`                            | DuckDB file path on the mounted volume.                 |
| `QYL_RETENTION_DAYS`           | `7`                                           | Ingest-side retention window.                           |
| `QYL_COLLECTOR_URL`            | `http://localhost:5100`                       | Self-reference for in-container tooling.                |
| `QYL_MCP_TRANSPORT`            | `http`                                        | Present here for cross-service parity.                  |
| `RAILWAY_DOCKERFILE_PATH`      | `services/qyl.collector/Dockerfile`           | Tells Railway which Dockerfile to build.                |
| `RAILWAY_HEALTHCHECK_TIMEOUT_SEC` | `60`                                       | Healthcheck grace period.                               |

Suggested (add these):

| Variable                     | Value    | Why                                                                |
|------------------------------|----------|--------------------------------------------------------------------|
| `DOTNET_RUNNING_IN_CONTAINER`| `true`   | Skips culture + ICU workarounds on container startup.              |
| `DOTNET_gcServer`            | `1`      | Server GC for the collector's ingest path.                         |

### `qyl-mcp` — 5 service variables

| Variable                       | Value                                          | Purpose                                                  |
|--------------------------------|------------------------------------------------|----------------------------------------------------------|
| `QYL_COLLECTOR_URL`            | `https://qyl-api-production.up.railway.app`    | Currently set to the **public** URL — see Gotchas.       |
| `QYL_MCP_PUBLIC_URL`           | `https://qyl-mcp-production.up.railway.app`    | Advertised to MCP clients; swap to `mcp.qyl.info` once DNS is stable. |
| `QYL_MCP_TRANSPORT`            | `http`                                         | Streamable HTTP transport.                               |
| `RAILWAY_DOCKERFILE_PATH`      | `services/qyl.mcp/Dockerfile`                  | Build path.                                              |
| `RAILWAY_HEALTHCHECK_TIMEOUT_SEC` | `60`                                        | Healthcheck grace period.                                |

Suggested (add these):

| Variable                     | Value                 | Why                                                    |
|------------------------------|-----------------------|--------------------------------------------------------|
| `ASPNETCORE_URLS`            | `http://+:5200`       | Matches the MCP bind port in `services/qyl.mcp`.       |
| `DOTNET_RUNNING_IN_CONTAINER`| `true`                | Skips culture + ICU workarounds.                       |
| `DOTNET_gcServer`            | `1`                   | Server GC.                                             |

### Project-wide / shared

| Variable                     | Scope                | Purpose                                                  |
|------------------------------|----------------------|----------------------------------------------------------|
| `OPENAI_API_KEY`             | Shared               | Required for `qyl-loom`'s Autofix + Triage pipelines.    |
| `OTEL_EXPORTER_OTLP_ENDPOINT`| Any                  | Forward qyl's own telemetry to a second backend (opt).   |
| `VITE_API_BASE_URL`          | `qyl-dashboard` only | **Public** URL of `qyl-api`; baked at build time.        |

Use Railway's `${{service.VAR}}` reference syntax instead of copy-pasting values, e.g.
`${{qyl-api.RAILWAY_PRIVATE_DOMAIN}}` or `${{qyl-api.QYL_PORT}}`. Placeholders resolve at
deploy time — don't interpolate them locally.

## Persistent storage for `qyl-api`

```sh
railway volume add --service qyl-api --mount /data
railway variables set --service qyl-api QYL_DATA_PATH=/data/qyl.duckdb
```

Without the volume, DuckDB lives in the container filesystem and is wiped on every deploy.
DuckDB 1.5.0 requires glibc — the collector Dockerfile is Debian
(`mcr.microsoft.com/dotnet/aspnet:10.0`). Don't switch to Alpine/musl for `qyl-api`.

## Private networking

Railway gives each service a `*.railway.internal` hostname reachable only by siblings in
the same project. Intended wiring:

```sh
railway variables set --service qyl-mcp  \
  QYL_COLLECTOR_URL='http://${{qyl-api.RAILWAY_PRIVATE_DOMAIN}}:${{qyl-api.QYL_PORT}}'

railway variables set --service qyl-loom \
  QYL_COLLECTOR_URL='http://${{qyl-api.RAILWAY_PRIVATE_DOMAIN}}:${{qyl-api.QYL_PORT}}'
```

> **Current state (as of screenshots):** `qyl-mcp`'s `QYL_COLLECTOR_URL` is
> `https://qyl-api-production.up.railway.app` — the **public** domain. That works but
> loses the private-network benefit (plaintext allowed, no egress, latency). Move to the
> `railway.internal` form once `qyl-api`'s auth behavior over plain HTTP is confirmed.

## OTLP ingestion (multiple TCP endpoints)

Railway supports multiple TCP endpoints per service. For `qyl-api`:

1. Primary HTTP endpoint — auto-assigned; honor `$PORT` (currently `QYL_PORT=8080`).
2. OTLP/gRPC endpoint — **Networking → Add TCP Proxy → 4317**.
3. OTLP/HTTP endpoint — **Networking → Add TCP Proxy → 4318**.

`CollectorKestrelExtensions.ConfigureQylCollectorKestrel` binds all three; Railway's TCP
proxies hand inbound traffic to the same container.

## Custom domains

- `mcp.qyl.info` is attached to `qyl-mcp` (public domain). Railway issues the TLS cert;
  add the CNAME in your DNS provider to the value Railway shows under
  **Settings → Networking → Custom Domain**.
- Update `QYL_MCP_PUBLIC_URL` to `https://mcp.qyl.info` once the cert goes green so MCP
  clients receive the stable hostname.

## Healthchecks

| Service         | Path         | Source                                                                  |
|-----------------|--------------|-------------------------------------------------------------------------|
| `qyl-api`       | `/health`    | `services/qyl.collector/Health/HealthUiEndpoints.cs`                    |
| `qyl-dashboard` | `/healthz`   | `services/qyl.dashboard/nginx.conf.template` (returns 200 from nginx).  |
| `qyl-mcp`       | `/health`    | MCP host endpoint (Streamable transport).                               |
| `qyl-loom`      | `/health`    | `AddQylServiceDefaults` default.                                        |

`RAILWAY_HEALTHCHECK_TIMEOUT_SEC=60` is the grace period before the first successful
check. Railway restarts the container on 3 consecutive failures.

## CI integration

The `.github/workflows/release.yml` tag pipeline already builds + pushes Docker images to
Docker Hub + GHCR. To have Railway redeploy on tag:

1. In each Railway service → **Settings → Source → Image registry** → set to
   `ghcr.io/ancplua/qyl-<service>:latest` (or a tag pattern).
2. Railway polls the registry and pulls new digests within minutes of CI publishing.

Alternative: wire Railway's GitHub integration directly and skip the registry hop — then
Railway builds the Docker images itself from your repo on every push (this is what's
active today, per the `RAILWAY_DOCKERFILE_PATH` settings).

## Gotchas caught during deploys

- **Build context is the repo root**, not the service directory. That's why
  `RAILWAY_DOCKERFILE_PATH` is `services/qyl.<svc>/Dockerfile` — relative paths inside the
  Dockerfile (`COPY packages/...`) work from the repo root context.
- **No OrbStack convenience on Railway.** Railway's BuildKit does a cold build with no
  host caches and no `/var/run/docker.sock` symlink. Stale `<ProjectReference>` paths
  that OrbStack masks locally will fail Railway. Reproduce with a plain
  `docker build -f services/qyl.<svc>/Dockerfile .` from the repo root before pushing.
- **`packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj` is a live drift
  point.** Observed Railway failure:
  `error MSB9008: The referenced project ../../packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj does not exist`
  during `qyl.instrumentation` publish. Current `packages/` contents: `Qyl.Client`,
  `Qyl.Contracts`, `Qyl.OpenTelemetry.Extensions`, `Qyl.Run`, `qyl-client`. Fix the csproj
  input (either re-add the project, or remove the reference + route semconv generation
  elsewhere) — do not add a shim Dockerfile workaround.
- **`qyl-mcp` Dockerfile targets `linux-musl-x64`** (Alpine). That's fine for the MCP
  service because it doesn't touch DuckDB. Don't copy the musl target to `qyl-api` — the
  collector needs glibc for `DuckDB.NET`.
- **`qyl-dashboard` bakes `VITE_API_BASE_URL` at build time.** If you change
  `qyl-api`'s public URL (or swap to `mcp.qyl.info` style custom domain for the API), the
  dashboard needs a fresh build, not just a restart.
- **`qyl-loom` is internal-only.** Don't enable a public endpoint on it — all its
  triggers come from `qyl-api` via HTTP and from queue items, no inbound HTTP from users.
- **`QYL_COLLECTOR_URL` via public URL is working-as-configured but not
  working-as-intended.** When the private-network migration happens, flip `qyl-mcp` and
  `qyl-loom` to `http://${{qyl-api.RAILWAY_PRIVATE_DOMAIN}}:${{qyl-api.QYL_PORT}}` in one
  deploy; don't leave a mixed state.
