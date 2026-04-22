# Shipping qyl on Railway

qyl runs as **four independent Railway services** inside one Railway project. Each service
points its "Root Directory" at the matching `services/qyl.*/` folder; the per-service
`railway.toml` there handles build + healthcheck + restart policy.

## Service matrix

| Railway service   | Root Directory               | Exposes               | Talks to                                      |
|-------------------|------------------------------|-----------------------|-----------------------------------------------|
| `qyl-collector`   | `services/qyl.collector/`    | Public HTTP + optional OTLP gRPC | DuckDB volume                         |
| `qyl-dashboard`   | `services/qyl.dashboard/`    | Public HTTP (nginx)   | `qyl-collector` (public URL via `VITE_API_BASE_URL`) |
| `qyl-mcp`         | `services/qyl.mcp/`          | Public HTTP (MCP Streamable) | `qyl-collector` (private URL)          |
| `qyl-loom`        | `services/qyl.loom/`         | Private HTTP only     | `qyl-collector` (private URL) + OpenAI        |

## One-time setup

```sh
# From the repo root
railway login
railway link                                                  # attach to a new/existing project

# Create each service, pointing its root at the right directory
railway up --service qyl-collector --detach  --path services/qyl.collector
railway up --service qyl-dashboard --detach  --path services/qyl.dashboard
railway up --service qyl-mcp       --detach  --path services/qyl.mcp
railway up --service qyl-loom      --detach  --path services/qyl.loom
```

Each service will auto-detect the per-directory `railway.toml` and use the matching
`Dockerfile`. The Dockerfiles already `COPY` from the repo root (`packages/`, `internal/`,
`eng/`), which is why Railway clones the whole repo into every service's build context.

## Variables

| Variable                     | Scope           | Purpose                                                  |
|------------------------------|-----------------|----------------------------------------------------------|
| `PORT`                       | Auto / per-svc  | Railway injects; every service honors it.                |
| `QYL_DATA_PATH`              | `qyl-collector` | Set to `/data/qyl.duckdb`; pair with a Railway volume mounted at `/data`. |
| `QYL_COLLECTOR_URL`          | `qyl-mcp`, `qyl-loom` | `http://qyl-collector.railway.internal:$PORT` — private networking. |
| `VITE_API_BASE_URL`          | `qyl-dashboard` | **Public** URL of `qyl-collector` (baked at build time). |
| `OPENAI_API_KEY`             | `qyl-loom` (+ shared) | Required for Autofix + Triage pipelines.           |
| `OTEL_EXPORTER_OTLP_ENDPOINT`| Any             | Forward qyl's own telemetry to a second backend (optional). |

**Shared variables** — Railway lets you define project-wide variables with a `${{...}}`
reference syntax. Use this for `OPENAI_API_KEY` and any API keys that every service needs.

## Persistent storage for the collector

```sh
railway volume add --service qyl-collector --mount /data
railway variables set --service qyl-collector QYL_DATA_PATH=/data/qyl.duckdb
```

Without the volume, DuckDB lives in the container filesystem and is wiped on every deploy.

## Private networking

Railway gives each service a `*.railway.internal` hostname reachable only by siblings in
the same project. qyl services use that instead of public URLs to keep OTLP + MCP traffic
off the public internet:

```sh
railway variables set --service qyl-mcp  QYL_COLLECTOR_URL='http://qyl-collector.railway.internal:${{qyl-collector.PORT}}'
railway variables set --service qyl-loom QYL_COLLECTOR_URL='http://qyl-collector.railway.internal:${{qyl-collector.PORT}}'
```

The `${{...}}` placeholders are resolved by Railway at deploy time — don't interpolate
them locally.

## OTLP ingestion (two TCP endpoints)

Railway supports multiple TCP endpoints per service since 2025-Q3. For `qyl-collector`:

1. Primary HTTP endpoint — auto-assigned on `$PORT`.
2. Secondary OTLP/gRPC endpoint — add via **Networking → Add TCP Proxy → 4317**.

The collector binds both in `CollectorKestrelExtensions.ConfigureQylCollectorKestrel`;
Railway's TCP proxy hands inbound gRPC traffic to the same container.

## Healthchecks

| Service         | Path         | Source                                                                  |
|-----------------|--------------|-------------------------------------------------------------------------|
| `qyl-collector` | `/health`    | `services/qyl.collector/Health/HealthUiEndpoints.cs`                    |
| `qyl-dashboard` | `/healthz`   | `services/qyl.dashboard/nginx.conf.template` (returns 200 from nginx).  |
| `qyl-mcp`       | `/health`    | MCP host endpoint (Streamable transport).                               |
| `qyl-loom`      | `/health`    | `AddQylServiceDefaults` default.                                        |

Railway restarts the container automatically on 3 consecutive failures.

## CI integration

The `.github/workflows/release.yml` tag pipeline already builds + pushes Docker images to
Docker Hub + GHCR. To have Railway redeploy on tag:

1. In each Railway service → **Settings → Source → Image registry** → set to
   `ghcr.io/ancplua/qyl-<service>:latest` (or a tag pattern).
2. Railway polls the registry and pulls new digests within minutes of CI publishing.

Alternative: wire Railway's GitHub integration directly and skip the registry hop — then
Railway builds the Docker images itself from your repo on every push.

## Gotchas caught during first deploy

- **Build context is the repo root**, not the service directory. That's why `dockerfilePath`
  in each `railway.toml` is `services/qyl.<svc>/Dockerfile` — relative paths inside the
  Dockerfile (`COPY packages/...`) work from the repo root context.
- **`qyl-dashboard` bakes `VITE_API_BASE_URL` at build time.** If you change the collector's
  public URL, the dashboard needs a fresh build, not just a restart.
- **DuckDB requires glibc.** The collector Dockerfile is `mcr.microsoft.com/dotnet/aspnet:10.0`
  (Debian), not Alpine. Don't "optimize" this away.
- **`qyl-loom` is internal-only.** Don't enable a public endpoint on it — all its triggers
  come from the collector via HTTP and from queue items, no inbound HTTP from users.
