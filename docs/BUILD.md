# Build Guide

> Complete guide: NUKE, Kiota, TypeSpec, Docker, Tests, CI

## Prerequisites

```bash
# Required
dotnet --version    # 10.0.100+
node --version      # 20+
npm --version       # 10+

# Optional
docker --version    # For containers
kiota --version     # For client generation (or use npx)
```

---

## 1. NUKE Build System

### Location

```
eng/build/
├── build.csproj              # NUKE project
├── Build.cs                  # Main build class
├── Build.Frontend.cs         # npm targets
├── Build.TypeSpec.cs         # TypeSpec/Kiota targets
└── Components/               # Reusable components
    ├── IHasSolution.cs       # Path definitions
    ├── ICompile.cs
    ├── ITest.cs
    └── ...
```

### Core Targets

```bash
# From repo root
./eng/build.sh <target>       # macOS/Linux
./eng/build.cmd <target>      # Windows

# Or via dotnet
dotnet run --project eng/build/build.csproj -- <target>
```

| Target | Description |
|--------|-------------|
| `Compile` | Build all .NET projects |
| `Test` | Run all tests |
| `Pack` | Create NuGet packages |
| `Clean` | Clean all outputs |

---

## 2. TypeSpec → OpenAPI

### Pipeline

```
core/specs/*.tsp
      │
      │ tsp compile
      ▼
core/generated/openapi/openapi.yaml
```

### Commands

```bash
# Install TypeSpec deps
cd core/specs && npm install

# Compile
nuke TypeSpecCompile
# or manually:
cd core/specs && npx tsp compile .
```

### Output

```
core/generated/
└── openapi/
    └── openapi.yaml    # OpenAPI 3.1 spec
```

---

## 3. Kiota Client Generation

### Pipeline

```
core/generated/openapi/openapi.yaml
      │
      │ kiota generate
      ▼
┌─────────────────────────────────────────┐
│ core/generated/dotnet/      (C#)        │
│ src/qyl.dashboard/types/generated/ (TS) │
└─────────────────────────────────────────┘
```

### Commands

```bash
# Generate C# client
nuke GenerateCSharp
# or manually:
kiota generate \
  --language csharp \
  --openapi core/generated/openapi/openapi.yaml \
  --output core/generated/dotnet \
  --namespace-name Qyl.Core \
  --class-name QylClient

# Generate TypeScript client
nuke GenerateTypeScript
# or manually:
kiota generate \
  --language typescript \
  --openapi core/generated/openapi/openapi.yaml \
  --output src/qyl.dashboard/src/types/generated \
  --class-name QylClient

# Generate all
nuke GenerateAll
```

### Dashboard Sync

```bash
# Copy generated TS to dashboard
nuke SyncDashboardTypes
```

---

## 4. Frontend Build

### Commands

```bash
# Install dependencies
npm install --prefix src/qyl.dashboard

# Development server (with proxy to collector)
npm run dev --prefix src/qyl.dashboard

# Production build
npm run build --prefix src/qyl.dashboard

# Run tests
npm run test --prefix src/qyl.dashboard

# NUKE target
nuke FrontendBuild
```

### Vite Proxy

```typescript
// vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/api': 'http://localhost:5100',
      '/v1': 'http://localhost:5100',
      '/health': 'http://localhost:5100',
    }
  }
});
```

---

## 5. Docker

### Build Images

```bash
# All images
nuke DockerBuild

# Individual images
docker build -t qyl-collector -f src/qyl.collector/Dockerfile .
docker build -t qyl-dashboard -f src/qyl.dashboard/Dockerfile .
docker build -t qyl-mcp -f src/qyl.mcp/Dockerfile .
```

### Compose (Full Stack)

```bash
# Start all services
docker compose -f eng/compose.yaml up -d

# View logs
docker compose -f eng/compose.yaml logs -f

# Stop
docker compose -f eng/compose.yaml down
```

### compose.yaml

```yaml
services:
  collector:
    build:
      context: ..
      dockerfile: src/qyl.collector/Dockerfile
    ports:
      - "5100:5100"   # REST API
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
    volumes:
      - qyl-data:/data
    environment:
      DuckDb__Path: /data/qyl.duckdb

  dashboard:
    build:
      context: ..
      dockerfile: src/qyl.dashboard/Dockerfile
    ports:
      - "3000:80"
    depends_on:
      - collector

volumes:
  qyl-data:
```

---

## 6. Tests

### Run All Tests

```bash
nuke Test
# or
dotnet test
```

### Test Projects

```
tests/
├── UnitTests/
│   ├── qyl.analyzers.tests/    # Analyzer tests
│   └── qyl.mcp.server.tests/   # MCP tool tests
└── IntegrationTests/           # (future)
```

### Coverage

```bash
nuke Coverage
# Output: Artifacts/coverage/
```

---

## 7. CI/CD (GitHub Actions)

### Workflows

```
.github/workflows/
├── build.yaml      # Build + test on PR
├── release.yaml    # Publish on tag
└── docker.yaml     # Build + push images
```

### Build Workflow

```yaml
name: Build
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - run: ./eng/build.sh Compile
      - run: ./eng/build.sh Test
```

---

## 8. Codex (Schema-Driven Generation)

### Location

```
eng/build/codex/
├── QylSchema.cs           # SINGLE SOURCE OF TRUTH
├── IEmitter.cs            # Emitter interface
├── CSharpEmitter.cs       # Generate C# models
├── DuckDbEmitter.cs       # Generate schema.sql
└── TypeScriptEmitter.cs   # Generate TS types
```

### QylSchema.cs

Defines all:
- Primitives (SessionId, UnixNano, TraceId, SpanId)
- Models (SpanRecord, GenAiSpanData, SessionSummary, TraceNode)
- DuckDB tables with column mappings
- OTel gen_ai.* attributes

### Generate

```bash
nuke CodexGenerate
# Outputs:
#   src/qyl.protocol/Models/*.g.cs
#   src/qyl.collector/Storage/DuckDbSchema.g.cs
#   src/qyl.dashboard/src/types/generated/*.ts
```

---

## 9. Quick Reference

### Daily Development

```bash
# Backend
dotnet run --project src/qyl.collector

# Frontend (separate terminal)
npm run dev --prefix src/qyl.dashboard

# Send test telemetry
curl -X POST http://localhost:4318/v1/traces \
  -H "Content-Type: application/json" \
  -d @test-spans.json
```

### Before PR

```bash
nuke Compile    # Build all
nuke Test       # Run tests
nuke Lint       # Check formatting (if available)
```

### Release

```bash
git tag v1.0.0
git push origin v1.0.0
# CI builds and publishes
```
