---
paths:
  - "eng/build/**/*.cs"
  - "core/specs/**/*.tsp"
  - "**/Dockerfile"
---

# Build Workflow Rules

## NUKE Target Dependencies

```
TypeSpecCompile → Generate → Compile
                             ↓
DashboardBuild → DashboardEmbed → Publish → DockerBuild
```

## Critical Path: Dashboard Embedding

```bash
npm run build → dist/
               ↓
nuke DashboardEmbed → collector/wwwroot/
                     ↓
dotnet publish → includes embedded dashboard
```

**MUST happen in this order:**
1. DashboardBuild (creates dist/)
2. Compile (builds collector)
3. DashboardEmbed (copies dist/ to wwwroot/)
4. Publish (packages everything)

## Code Generation Flow

```bash
# 1. Edit TypeSpec
vim core/specs/main.tsp

# 2. Compile to OpenAPI
npm run compile  # → openapi.yaml

# 3. Generate C#/DuckDB/TS
nuke Generate --force-generate

# 4. CI enforcement
nuke Generate  # fails if stale files detected
```

## Docker Build

```yaml
base-images:
  build: mcr.microsoft.com/dotnet/sdk:10.0
  runtime: mcr.microsoft.com/dotnet/aspnet:10.0
  dashboard: node:22-slim

ports:
  - 5100 (HTTP)
  - 4317 (gRPC)

volumes:
  - /data (DuckDB persistence)

critical: wwwroot/ MUST be included in final image
```
