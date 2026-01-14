# God Schema Consolidation Plan

> **Goal**: Single source of truth in `core/specs/` (48 TypeSpec files)

## Current State

```
Two TypeSpec sources (redundant):

schema/                          core/specs/
├── main.tsp                     ├── main.tsp (48 files)
├── models.tsp (5 storage)       ├── otel/span.tsp
├── primitives.tsp               ├── otel/logs.tsp
└── enums.tsp                    └── domains/**/*Entity.tsp
     │                                │
     ▼                                ▼
  DuckDB DDL                     OpenAPI → TS types
  C# records                     (no storage gen)
```

## Target State

```
Single source in core/specs/:

core/specs/
├── main.tsp
├── common/types.tsp ← Merge schema/primitives.tsp
├── otel/
│   ├── enums.tsp ← Merge schema/enums.tsp
│   ├── span.tsp  + @extension("x-duckdb-table", "spans")
│   └── logs.tsp  + @extension("x-duckdb-table", "logs")
└── domains/
    ├── observe/session.tsp + x-duckdb-table
    ├── observe/error.tsp   + x-duckdb-table
    ├── observe/test.tsp    + x-duckdb-table
    └── ops/deployment.tsp  + x-duckdb-table
           │
           ▼
    Single Build: nuke TypeSpecCompile
           │
           ├──► OpenAPI 3.1 → dashboard/src/types/api.ts
           ├──► C# records  → protocol/*.g.cs
           └──► DuckDB DDL  → collector/Storage/*.g.cs
```

## Migration Steps

### Phase 1: Merge Storage Annotations (DONE)

Added `x-duckdb-table` to 5 entity models in `core/specs/`:

| Model            | Table            | File                        |
|------------------|------------------|-----------------------------|
| DeploymentEntity | deployments      | domains/ops/deployment.tsp  |
| ErrorEntity      | errors           | domains/observe/error.tsp   |
| TestRunEntity    | test_runs        | domains/observe/test.tsp    |
| TestCaseEntity   | test_cases       | domains/observe/test.tsp    |
| SessionEntity    | session_entities | domains/observe/session.tsp |

### Phase 2: Merge Primitives

Merge `schema/primitives.tsp` → `core/specs/common/types.tsp`:

```typescript
// Add to common/types.tsp:
@jsonSchema
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionId")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(64)")
scalar SessionId extends string;

@jsonSchema
@TypeSpec.OpenAPI.extension("x-csharp-type", "ulong")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
scalar UnixNano extends int64;

// etc.
```

### Phase 3: Merge Storage Models

Migrate SpanRecord → enhance `core/specs/otel/span.tsp::Span`:

```typescript
@doc("OpenTelemetry Span")
@extension("x-duckdb-table", "spans")
@extension("x-csharp-type", "SpanRecord")
model Span {
  @extension("x-duckdb-column", "span_id")
  @extension("x-duckdb-primary-key", true)
  spanId: SpanId;

  @extension("x-duckdb-column", "trace_id")
  @extension("x-duckdb-index", "idx_spans_trace_id")
  traceId: TraceId;

  // ... promoted columns with x-extension annotations
}
```

### Phase 4: Update Build System

Modify `eng/build/Build.TypeSpec.cs`:

```csharp
// Change source from schema/ to core/specs/
Target TypeSpecCompile => _ => _
    .Executes(() =>
    {
        var specsDir = RootDirectory / "core" / "specs";
        // Compile with DuckDB emitter
    });
```

### Phase 5: Delete Redundant Files

```bash
rm -rf schema/
git add -A && git commit -m "refactor: consolidate God Schema into core/specs"
```

## DuckDB Tables After Consolidation

| Table            | Source                      | Primary Key   |
|------------------|-----------------------------|---------------|
| spans            | otel/span.tsp               | span_id       |
| logs             | otel/logs.tsp               | log_id        |
| session_entities | domains/observe/session.tsp | session_id    |
| errors           | domains/observe/error.tsp   | error_id      |
| test_runs        | domains/observe/test.tsp    | run_id        |
| test_cases       | domains/observe/test.tsp    | case_id       |
| deployments      | domains/ops/deployment.tsp  | deployment_id |

## Validation Checklist

- [ ] TypeSpec compiles without errors: `cd core/specs && npm run compile`
- [ ] OpenAPI generates: `nuke TypeSpecCompile`
- [ ] Dashboard types generate: `cd src/qyl.dashboard && npm run generate:ts`
- [ ] C# records generate: `nuke Generate`
- [ ] DuckDB DDL generates: `nuke Generate`
- [ ] All tests pass: `dotnet test`
- [ ] schema/ directory deleted
