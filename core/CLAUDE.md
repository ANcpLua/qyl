# core - TypeSpec Schema

Single source of truth for all types. Everything downstream is generated.

## Structure

```
specs/
  main.tsp              # Entry point
  common/               # Shared types, errors, pagination
  otel/                 # Span, logs, metrics, resource
  api/                  # REST routes, SSE streaming
  domains/              # Domain-specific semconv extensions

openapi/
  openapi.yaml          # Generated — DO NOT EDIT
```

## Commands

```bash
npm install                          # Install TypeSpec packages
npm run compile                      # TypeSpec -> OpenAPI
nuke Generate --force-generate       # Full regen (C#, DuckDB, TS)
```

## Custom Extensions (read by SchemaGenerator.cs)

| Extension              | Purpose                                |
|------------------------|----------------------------------------|
| `x-duckdb-table`       | Mark model for DuckDB table gen        |
| `x-duckdb-column`      | Override column name                   |
| `x-duckdb-type`        | Override DuckDB type (JSON, BLOB)      |
| `x-duckdb-primary-key` | Primary key column                     |
| `x-duckdb-index`       | Create index                           |
| `x-primitive`          | Strongly-typed scalar wrapper          |
| `x-promoted`           | Promote from attributes_json to column |
| `x-csharp-type`        | Override C# type name                  |

## Storage Tables

| Model              | Table            | Source                      |
|--------------------|------------------|-----------------------------|
| `Span`             | spans            | otel/span.tsp               |
| `LogRecord`        | logs             | otel/logs.tsp               |
| `SessionEntity`    | session_entities | domains/observe/session.tsp |
| `ErrorEntity`      | errors           | domains/observe/error.tsp   |
| `TestRunEntity`    | test_runs        | domains/observe/test.tsp    |
| `TestCaseEntity`   | test_cases       | domains/observe/test.tsp    |
| `DeploymentEntity` | deployments      | domains/ops/deployment.tsp  |

## Code Generation Output

```
main.tsp -> openapi.yaml -> src/qyl.protocol/*.g.cs
                          -> src/qyl.collector/Storage/DuckDbSchema.g.cs
                          -> src/qyl.dashboard/src/types/api.ts
```

## Rules

- Never edit `openapi.yaml`, `*.g.cs`, or `api.ts` directly
- All changes go through TypeSpec
- Follow OTel Semantic Conventions 1.39
