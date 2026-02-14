# Copilot Instructions — qyl

AI observability platform: OTLP ingestion → DuckDB → real-time dashboard/MCP/Copilot.

## Build & Test

```bash
# Backend
dotnet build                                          # Build all .NET projects
dotnet test                                           # All tests (xUnit v3 + MTP)
dotnet test tests/qyl.collector.tests                 # Single test project
dotnet test --filter "FullyQualifiedName~MyTest"      # Single test by name
dotnet test --filter "FullyQualifiedName~Integration" # Category filter
dotnet run --project src/qyl.collector                # Run backend (ports 5100, 4317)

# Frontend (src/qyl.dashboard/)
npm ci && npm run dev                                 # Dev server (port 5173)
npm run build                                         # Production build
npm run typecheck                                     # TypeScript check
npm run lint                                          # ESLint

# Full pipeline (NUKE)
nuke Full                                             # TypeSpec → Build → Test → Docker
nuke Generate --force-generate                        # Regenerate all types from TypeSpec
nuke Coverage                                         # Tests + Cobertura reports
```

MTP arguments go after `--` separator: `dotnet test -- --report-trx`.

## Architecture

```
qyl.hosting (QylRunner)          ← orchestrates everything
    └── qyl.collector            ← kernel: OTLP ingest, DuckDB, REST API, SSE, embedded dashboard
         ├── qyl.protocol        ← shared types (BCL-only, zero dependencies)
         ├── qyl.dashboard       ← React 19 SPA (embedded in collector at build time)
         ├── qyl.mcp             ← MCP server (stdio, AOT, HTTP-only to collector)
         └── qyl.copilot         ← GitHub Copilot integration (AG-UI/SSE)

qyl.servicedefaults              ← OTel instrumentation library for consumer apps
qyl.servicedefaults.generator    ← Roslyn source gen for GenAI/DB interceptors
qyl.instrumentation.generators   ← DuckDB insert + interceptor source generators
qyl.browser                      ← Browser OTLP SDK (TypeScript, ESM + IIFE)
qyl.watch                        ← Live terminal span viewer (dotnet tool)
qyl.watchdog                     ← Process anomaly detection daemon (dotnet tool)
qyl.cli                          ← One-command instrumentation CLI (dotnet tool)
```

### Dependency Rules

- `collector → protocol`: ProjectReference allowed
- `mcp → protocol`: ProjectReference allowed
- `mcp → collector`: **HTTP only** (no ProjectReference — AOT boundary)
- `protocol → anything`: **forbidden** (must stay BCL-only, zero packages)
- `dashboard → collector`: HTTP at runtime

## TypeSpec-First Design

All types originate in `core/specs/*.tsp`. Generated artifacts are **never edited manually**:

```
core/specs/*.tsp → core/openapi/openapi.yaml → C# (*.g.cs) | DuckDB schema | TypeScript (api.ts) | JSON Schema
```

- `src/qyl.protocol/*.g.cs` — generated record types, enums, scalars
- `src/qyl.collector/Storage/DuckDbSchema.g.cs` — generated DDL
- `src/qyl.dashboard/src/types/api.ts` — generated TS types

To change a type: edit the `.tsp` file, then `nuke Generate --force-generate`.

Custom TypeSpec extensions: `x-duckdb-table`, `x-duckdb-type`, `x-primitive`, `x-promoted`, `x-csharp-type`.

## Key Conventions

### C# (.NET 10, C# 14)

- **LoggerMessage source generator** for all structured logging (no string interpolation in log calls)
- **Static lambdas** for `MapGet`/`MapPost` endpoint handlers
- **`ThrowIfDisposed()` guard** at the start of all public `DuckDbStore` methods
- **Channel-buffered WriteJob pattern** for DuckDB writes (single writer, batched inserts)
- **Central Package Management** — all versions in `Directory.Packages.props` / `Version.props`
- **XML doc comments** on public DuckDbStore methods
- **`WarningsAsErrors`**: CA1816, CA2012, CA2016 (async/dispose rules)
- MSBuild SDKs: `ANcpLua.NET.Sdk`, `ANcpLua.NET.Sdk.Web`, `ANcpLua.NET.Sdk.Test`

### Frontend (React 19, Vite 7, Tailwind CSS 4)

- TanStack Query 5 for server state; Radix UI for accessibility
- Never edit `src/types/api.ts` — regenerate with `npm run generate:types`

### Testing (xUnit v3 + Microsoft Testing Platform)

- In-memory DuckDB via `DuckDbTestHelpers.CreateInMemory()` for test isolation
- Test naming: `Should_X_When_Y` or `Method_Scenario_Expected`
- Prefer integration tests for API endpoints
- No mocking of core domain types
- Assertions via AwesomeAssertions

### Storage (DuckDB)

- UBIGINT timestamps passed as `decimal` for DuckDB.NET compatibility
- Protocol uses `long` (signed 64-bit); collector converts to `ulong` for DuckDB
- Upsert-based ingestion: `ON CONFLICT (span_id) DO UPDATE` (retry-safe)
- Pooled read connections, single writer connection for all writes

## Ports

| Port | Purpose |
|------|---------|
| 5100 | HTTP REST API, SSE, Dashboard, OTLP/HTTP |
| 4317 | gRPC OTLP ingestion |
| 5173 | Dashboard dev server (Vite) |

## Component CLAUDE.md Files

Each component has its own `CLAUDE.md` with detailed patterns. Consult these for component-specific work:
`src/qyl.collector/CLAUDE.md`, `src/qyl.protocol/CLAUDE.md`, `src/qyl.dashboard/CLAUDE.md`, `src/qyl.mcp/CLAUDE.md`, `tests/CLAUDE.md`, `eng/CLAUDE.md`, `core/CLAUDE.md`.
