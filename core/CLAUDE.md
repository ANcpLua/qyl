# core - TypeSpec Schema

Single source of truth for all types. Everything downstream is generated.

## Directory Structure

```
specs/
  main.tsp              # Entry point (imports all domains)
  common/               # Shared types, errors, pagination
  otel/                 # OTel core: span, logs, metrics, resource
  api/                  # REST routes, SSE streaming
  domains/
    ai/                 # GenAI, code, CLI
    security/           # Network, DNS, TLS
    transport/          # HTTP, RPC, messaging, SignalR
    infra/              # Host, container, K8s, cloud
    runtime/            # Process, dotnet, ASP.NET Core
    data/               # DB, file, VCS
    observe/            # Session, browser, feature-flags
    ops/                # CI/CD, deployment
    identity/           # User, geo

openapi/
  openapi.yaml          # Generated - DO NOT EDIT
```

## Commands

```bash
# Install TypeSpec packages
npm install

# Compile TypeSpec to OpenAPI
npm run compile

# Full regeneration (includes C#, DuckDB, TypeScript)
nuke Generate --force-generate
```

## TypeSpec Packages

| Package | Purpose |
|---------|---------|
| `@typespec/compiler` | Core compiler |
| `@typespec/http` | HTTP decorators |
| `@typespec/rest` | REST patterns |
| `@typespec/openapi` | OpenAPI decorators |
| `@typespec/openapi3` | OpenAPI 3.x emitter |
| `@typespec/versioning` | API versioning |
| `@typespec/sse` | Server-Sent Events |

## Custom Extensions

These `x-*` extensions are read by `SchemaGenerator.cs`:

| Extension | Purpose |
|-----------|---------|
| `x-duckdb-table` | Mark model for DuckDB table generation |
| `x-duckdb-column` | Override column name |
| `x-duckdb-type` | Override DuckDB type (JSON, BLOB) |
| `x-duckdb-primary-key` | Mark primary key column |
| `x-duckdb-index` | Create index on column |
| `x-primitive` | Mark scalar as strongly-typed wrapper |
| `x-promoted` | Promote from attributes_json to column |
| `x-csharp-type` | Override C# type name |

## Storage Tables

Tables marked with `@extension("x-duckdb-table", ...)`:

| Model | Table | Location |
|-------|-------|----------|
| `Span` | spans | otel/span.tsp |
| `LogRecord` | logs | otel/logs.tsp |
| `SessionEntity` | session_entities | domains/observe/session.tsp |
| `ErrorEntity` | errors | domains/observe/error.tsp |
| `TestRunEntity` | test_runs | domains/observe/test.tsp |
| `TestCaseEntity` | test_cases | domains/observe/test.tsp |
| `DeploymentEntity` | deployments | domains/ops/deployment.tsp |

## Code Generation Flow

```
main.tsp
    |
    v
tsp compile main.tsp
    |
    v
openapi/openapi.yaml
    |
    v
nuke Generate
    |
    +-> src/qyl.protocol/*.g.cs
    +-> src/qyl.collector/Storage/DuckDbSchema.g.cs
    +-> src/qyl.dashboard/src/types/api.ts
```

## Adding a New Domain

1. Create `domains/{domain}/{feature}.tsp`
2. Add import in `main.tsp`
3. Define models with appropriate extensions
4. Run `npm run compile`
5. Run `nuke Generate --force-generate`
6. Verify generated files

## API Versioning

```typespec
@versioned(ApiVersions)
@service(#{ title: "QYL Observability API" })
namespace Qyl.Api;

enum ApiVersions {
  v1: "2025-12-01",
  v2: "2026-01-15",
  v3: "2026-01-26",
}
```

## Rules

- **Never edit** `openapi.yaml` directly
- **Never edit** `*.g.cs` or `api.ts` files
- All changes must go through TypeSpec
- Follow OTel Semantic Conventions 1.39
