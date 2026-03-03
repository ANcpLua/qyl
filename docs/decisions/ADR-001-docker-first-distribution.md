# ADR-001: Docker-First Distribution

Status: Accepted
Date: 2026-02-26

## Context

qyl is distributed as multiple components (collector, dashboard, MCP, CLI, watch). Users must understand the architecture to get started. The onboarding friction is too high.

## Decision

qyl ships as a **single Docker image** — a polyglot OTLP collector like Grafana or Jaeger. Any app that speaks OpenTelemetry can send telemetry to qyl, regardless of language.

```bash
docker run -d -p 5100:5100 -p 4317:4317 ghcr.io/ancplua/qyl
```

That's it. Dashboard at `:5100`, OTLP ingestion at `:4317`. Remove container = qyl is gone.

### Polyglot by Default

qyl accepts standard OTLP from any language. No SDK required:

```bash
# Python
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 python app.py

# Node.js
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 node app.js

# Go
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 ./myapp

# .NET
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run

# Any language with OTel SDK
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 <your-app>
```

### .NET Premium SDK (Optional)

For .NET projects, `qyl.servicedefaults` provides a premium experience with compile-time source generators (ADR-003). But the Docker image works without it.

## Constraints

- No Sentry dependency
- No Aspire dependency
- No Azure dependency
- No paid services
- Self-hosted only
- Language-agnostic — OTLP is the only protocol

## What's Inside the Image

| Component | Port | Purpose |
|-----------|------|---------|
| Collector | 5100 (HTTP), 4317 (gRPC) | OTLP ingestion, REST API, SSE |
| Dashboard | 5100 (embedded) | React SPA served by collector |
| MCP Server | stdio (via `docker exec`) | AI agent integration |
| DuckDB | internal | Storage (ephemeral or volume-mounted) |

## What's NOT Inside

| Component | Why |
|-----------|-----|
| qyl.cli | Replaced by standard OTel env vars + optional NuGet (ADR-003) |
| qyl.watch | Separate dotnet tool (optional, for terminal users) |
| qyl.watchdog | Removed from qyl (standalone repo) |

## Acceptance Criteria

```gherkin
GIVEN no qyl installation
WHEN  docker run -d -p 5100:5100 -p 4317:4317 ghcr.io/ancplua/qyl
THEN  GET http://localhost:5100/health returns 200 within 10s
AND   GET http://localhost:5100 returns the dashboard HTML
AND   gRPC health check on localhost:4317 returns SERVING

GIVEN a running qyl container
WHEN  a Python app sends OTLP spans to localhost:4317
THEN  spans appear in the dashboard

GIVEN a running qyl container
WHEN  a .NET app with qyl.servicedefaults sends spans
THEN  spans appear with enriched gen_ai.*/db.* attributes

GIVEN a running qyl container
WHEN  docker rm -f <container>
THEN  ports 5100 and 4317 are free
AND   no qyl processes remain on host
```

## Verification Steps (Agent-Executable)

1. `docker build -f src/qyl.collector/Dockerfile -t qyl .`
2. `docker run -d --name qyl-test -p 5100:5100 -p 4317:4317 qyl`
3. `curl -sf http://localhost:5100/health` → assert 200
4. `curl -sf http://localhost:5100` → assert contains `<div id="root">`
5. Send OTLP span via curl (JSON): `curl -X POST http://localhost:5100/v1/traces -H 'Content-Type: application/json' -d '{...}'` → assert 202
6. `docker rm -f qyl-test`
7. `curl -sf http://localhost:5100/health` → assert connection refused

## Consequences

- qyl is a Grafana/Jaeger alternative, not a .NET-only tool
- Any OTel-instrumented app works out of the box
- .NET gets premium source-generator experience via optional NuGet (ADR-003)
- Dashboard must handle empty state / onboarding (ADR-002)
- MCP accessible via `docker exec qyl qyl-mcp` or network stdio proxy
- Data persistence requires explicit `-v ~/.qyl:/data` mount
