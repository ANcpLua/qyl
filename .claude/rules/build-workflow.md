---
paths:
  - "eng/build/**/*.cs"
  - "core/specs/**/*.tsp"
  - "**/Dockerfile"
---

# Build Workflow Rules

## NUKE Target Dependencies

```
TypeSpecCompile → Generate → Compile → Test
                                ↓
FrontendBuild ------→ DockerImageBuild
```

## Critical Path: Dashboard Embedding

Dashboard embedding happens inside Docker multi-stage build:

```bash
# NUKE builds frontend
nuke FrontendBuild → dist/

# Docker multi-stage build embeds dashboard
nuke DockerImageBuild:
  Stage 1: Build frontend (if not cached)
  Stage 2: Build .NET collector
  Stage 3: Copy dashboard dist/ to wwwroot/
```

**Build order for Docker:**
1. FrontendBuild (creates dist/)
2. DockerImageBuild (multi-stage, embeds dashboard)

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
