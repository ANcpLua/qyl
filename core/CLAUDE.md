# core

TypeSpec schema and generated outputs. Single source of truth for all types.

## structure

```yaml
specs/           # TypeSpec source files
  main.tsp       # Entry point (48 imports across 9 domains)
  common/        # Shared types, errors, pagination
  otel/          # OTel core: span, logs, metrics, resource
  domains/       # Domain-specific attributes
    ai/          # GenAI, code, CLI
    security/    # Network, DNS, TLS
    transport/   # HTTP, RPC, messaging, SignalR
    infra/       # Host, container, K8s, cloud
    runtime/     # Process, dotnet, ASP.NET Core
    data/        # DB, file, VCS
    observe/     # Session, browser, feature-flags
    ops/         # CI/CD, deployment
    identity/    # User, geo
  api/           # REST routes, SSE streaming

openapi/         # Generated OpenAPI spec
  openapi.yaml   # DO NOT EDIT - generated from TypeSpec

schemas/         # JSON schemas (if needed)
```

## typespec-packages

```yaml
compiler: "@typespec/compiler"
http: "@typespec/http"
rest: "@typespec/rest"
openapi: "@typespec/openapi"
openapi3: "@typespec/openapi3"
versioning: "@typespec/versioning"
sse: "@typespec/sse"
events: "@typespec/events"
```

## custom-extensions

```yaml
x-duckdb-table: marks model for DuckDB table generation
x-duckdb-column: column name override
x-duckdb-type: DuckDB type override (e.g., JSON, BLOB)
x-duckdb-primary-key: marks primary key column
x-duckdb-index: creates index on column
x-primitive: marks scalar as strongly-typed wrapper
x-promoted: promoted from attributes_json to column
x-csharp-type: C# type name override
x-enum-varnames: enum member names for codegen
```

## storage-tables

```yaml
spans: Span model (otel/span.tsp)
logs: LogRecord model (otel/logs.tsp)
session_entities: SessionEntity (domains/observe/session.tsp)
errors: ErrorEntity (domains/observe/error.tsp)
test_runs: TestRunEntity (domains/observe/test.tsp)
test_cases: TestCaseEntity (domains/observe/test.tsp)
deployments: DeploymentEntity (domains/ops/deployment.tsp)
```

## commands

```yaml
compile: npm run compile  # TypeSpec -> OpenAPI
install: npm install      # Install TypeSpec packages
```

## codegen-flow

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
    +-> src/qyl.protocol/*.g.cs (Scalars, Enums, Models)
    +-> src/qyl.collector/Storage/DuckDbSchema.g.cs
    +-> src/qyl.dashboard/src/types/api.ts
```

## rules

- NEVER edit openapi.yaml directly - edit TypeSpec and recompile
- All x-* extensions are read by SchemaGenerator.cs
- OTel semconv 1.39 compliance required
