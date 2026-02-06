---
name: deploy
description: Deploy the qyl observability platform. Use when the user says "deploy", "deploy qyl", "push to production", "run qyl", or "go live".
---

# Deploy qyl

## Prerequisites Check

```bash
dotnet --version
docker --version
```

If .NET not installed: visit https://dot.net/download
If Docker not installed: visit https://docs.docker.com/get-docker/

## Build & Test

```bash
dotnet build
dotnet test
```

## Deployment Options

**Docker (production):**
```bash
docker run -d \
  --name qyl-collector \
  -p 5100:5100 \
  -p 4317:4317 \
  -v ~/.qyl:/data \
  ghcr.io/ancplua/qyl:latest
```

**Local development:**
```bash
dotnet run --project src/qyl.collector
```

**Dashboard dev server (separate terminal):**
```bash
cd src/qyl.dashboard && npm run dev
```

## After Deployment

- Collector API: http://localhost:5100
- OTLP gRPC endpoint: http://localhost:4317
- Dashboard: http://localhost:5173 (dev) or http://localhost:5100 (embedded)
- Health check: `curl http://localhost:5100/health`
- Mention `docker logs qyl-collector` for debugging if needed
