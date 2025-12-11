# qyl. v2.0 — AI Observability Platform

Vendor-neutral OpenTelemetry GenAI collector. Single binary, embedded DuckDB, real-time SSE.

```
SCHEMA                          RUNTIME                           CONSUMERS
──────                          ───────                           ─────────
core/specs/*.tsp (54 files)     OTLP (gRPC/HTTP)                  Dashboard
       │                              │                           CLI
       ▼ Kiota                        ▼                           MCP
┌──────────────┐            ╔═══════════════════╗
│ C# / TS / Py │            ║  qyl.collector    ║──▶ SSE/REST
└──────────────┘            ║  (Native AOT)     ║
                            ╠═══════════════════╣
                            ║ GenAiExtractor    ║──▶ DuckDB (hot)
                            ║ SessionAggregator ║──▶ Parquet (cold)
                            ║ SseHub            ║──▶ EventStream
                            ╚═══════════════════╝
```

## Stack

| Component | Tech             | Version |
|-----------|------------------|---------|
| Backend   | .NET Native AOT  | 10.0    |
| Frontend  | React + Tailwind | 19 / v4 |
| Storage   | DuckDB embedded  | —       |
| Schema    | TypeSpec → Kiota | 1.7.0   |
| Build     | NUKE             | 9.x     |

## Quick Start

```bash
# Backend
dotnet run --project src/qyl.collector

# Frontend (separate terminal)
npm run dev --prefix src/qyl.dashboard

# Demo producer
dotnet run --project src/qyl.demo
```

## NUKE Build Commands

```bash
# Install NUKE (if needed)
dotnet tool install Nuke.GlobalTool -g

# Common targets
nuke Compile              # Build all .NET projects
nuke Test                 # Run all tests
nuke Coverage             # Run tests with coverage

# Frontend targets
nuke FrontendBuild        # Build dashboard for production
nuke FrontendTest         # Run Vitest tests
nuke FrontendLint         # ESLint check
nuke FrontendDev          # Start Vite dev server

# Code generation (TypeSpec → Kiota)
nuke GenerateAll          # Generate C#, Python, TypeScript clients
nuke TypeSpecCompile      # TypeSpec → OpenAPI 3.1
nuke GenerateCSharp       # Generate C# client
nuke GeneratePython       # Generate Python client
nuke GenerateTypeScript   # Generate TypeScript client
nuke TypeSpecInfo         # Show generation status

# Docker
nuke DockerBuild          # Build collector image
nuke DockerUp             # Start with docker-compose

# CI
nuke Ci                   # Full backend pipeline
nuke Full                 # Backend + Frontend pipeline
```

## TypeSpec Schema (54 files)

```
core/specs/
├── main.tsp                    ← entry point
├── tspconfig.yaml              ← compiler config
├── common/   (3)  types, errors, pagination
├── otel/     (5)  enums, resource, span, logs, metrics
├── api/      (2)  routes, streaming
└── domains/
    ├── ai/        (3)  genai, code, cli
    ├── security/  (4)  network, dns, tls, security-rule
    ├── transport/ (7)  http, rpc, messaging, url, signalr, kestrel, user-agent
    ├── infra/     (7)  host, container, k8s, cloud, faas, os, webengine
    ├── runtime/   (5)  process, system, thread, dotnet, aspnetcore
    ├── data/      (5)  db, file, elasticsearch, vcs, artifact
    ├── observe/   (8)  session, browser, feature-flags, exceptions, otel, log, error, test
    ├── ops/       (2)  cicd, deployment
    └── identity/  (2)  user, geo
```

Generated outputs:

- `core/openapi/openapi.yaml` — OpenAPI 3.1 spec (~188KB)
- `core/generated/dotnet/` — C# client (183 files)
- `core/generated/python/` — Python client (169 files)
- `core/generated/typescript/` — TypeScript client (70 files)

## Key Files

| Purpose        | Path                                            |
|----------------|-------------------------------------------------|
| API Entry      | `src/qyl.collector/Program.cs`                  |
| GenAI Extract  | `src/qyl.collector/Ingestion/GenAiExtractor.cs` |
| Storage        | `src/qyl.collector/Storage/DuckDbStore.cs`      |
| SSE Hub        | `src/qyl.collector/Realtime/SseHub.cs`          |
| Dashboard      | `src/qyl.dashboard/src/App.tsx`                 |
| Build Config   | `eng/build/Build.cs`                            |
| TypeSpec Build | `eng/build/Build.TypeSpec.cs`                   |

## Frontend (React 19)

```
src/qyl.dashboard/
├── src/
│   ├── pages/           # 6 pages (GenAI, Traces, Logs, Metrics, Resources, Settings)
│   ├── components/
│   │   ├── layout/      # DashboardLayout, Sidebar, TopBar
│   │   └── ui/          # shadcn/ui components
│   ├── hooks/           # use-telemetry, use-keyboard-shortcuts, use-theme
│   └── types/           # TypeScript definitions
├── package.json
└── vite.config.ts
```

**Stack:** React 19, TypeScript 5.9, Tailwind v4, TanStack Query 5, Radix UI, Vite 7

## .NET 10 APIs

| API                    | Usage                                       |
|------------------------|---------------------------------------------|
| `Task.WhenEach()`      | Concurrent receiver intake                  |
| `Lock`                 | Zero-alloc sync (SessionAggregator, SseHub) |
| `SearchValues<char>`   | SIMD delimiter/char detection               |
| Direct `StartsWith`    | Prefix detection (gen_ai.*, agents.*)       |
| `CountBy/AggregateBy`  | Single-pass statistics                      |
| `TypedResults.SSE`     | Native Server-Sent Events                   |
| `HybridCache`          | Stampede-proof caching                      |

## API

```
POST /v1/traces           OTLP ingest
GET  /api/v1/sessions     List sessions
GET  /api/v1/live         SSE stream
GET  /health              Health check
```

## OTel v1.38

Required GenAI: `gen_ai.provider.name`, `gen_ai.request.model`, `gen_ai.usage.{input,output}_tokens`

## Documentation

| File                                 | Purpose                            |
|--------------------------------------|------------------------------------|
| `CLAUDE.md`                          | AI development guide (start here)  |
| `spec-compliance-matrix/schema.yaml` | TypeSpec schema, .NET APIs, rules  |
| `spec-compliance-matrix/UML.md`      | OTel 1.38 attributes, architecture |
