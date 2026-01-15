# core/specs — TypeSpec God Schema

Single source of truth for all types. Everything flows from here.

## identity

```yaml
name: core/specs
type: typespec-schema
role: single-source-of-truth
```

## files

```yaml
entry: main.tsp

structure:
  common/:
    - types.tsp        # Primitives (SpanId, TraceId, etc.)
    - errors.tsp       # Error models
    - pagination.tsp   # Pagination types
    
  otel/:
    - enums.tsp        # SpanKind, StatusCode, SeverityNumber
    - resource.tsp     # OTel Resource model
    - span.tsp         # Span, SpanEvent, SpanLink
    - logs.tsp         # LogRecord
    - metrics.tsp      # Metric models
    
  storage/:
    - storage.tsp      # SpanRecord, SessionSummary, TraceNode
    
  domains/:
    ai/:
      - genai.tsp      # gen_ai.* semantic conventions
      - code.tsp       # code.* attributes
      - cli.tsp        # CLI tool attributes
    # ... other domains
    
  api/:
    - routes.tsp       # REST API routes
    - streaming.tsp    # SSE streaming
```

## extensions

```yaml
x-csharp-type:
  purpose: map to C# type name
  example: '@extension("x-csharp-type", "SpanRecord")'
  
x-duckdb-table:
  purpose: mark model as DuckDB table
  example: '@extension("x-duckdb-table", "spans")'
  
x-duckdb-column:
  purpose: column name override
  example: '@extension("x-duckdb-column", "span_id")'
  
x-duckdb-type:
  purpose: DuckDB type override
  example: '@extension("x-duckdb-type", "VARCHAR")'
  
x-duckdb-primary-key:
  purpose: mark as primary key
  example: '@extension("x-duckdb-primary-key", true)'
  
x-duckdb-index:
  purpose: create index on column
  example: '@extension("x-duckdb-index", "idx_spans_trace_id")'
  
x-primitive:
  purpose: mark as strongly-typed wrapper
  example: '@extension("x-primitive", true)'
  
x-promoted:
  purpose: mark as promoted from attributes_json
  example: '@extension("x-promoted", true)'
```

## compilation

```yaml
command: tsp compile main.tsp
config: tspconfig.yaml
output: ../openapi/openapi.yaml

tspconfig:
  emit:
    - "@typespec/openapi3"
    - "@typespec/json-schema"
  options:
    "@typespec/openapi3":
      output-file: openapi.yaml
      emitter-output-dir: "{project-root}/../openapi"
      openapi-versions: ["3.1.0"]
```

## outputs

```yaml
from-typespec:
  - core/openapi/openapi.yaml
  - core/schemas/*.json

from-openapi:
  - src/qyl.protocol/**/*.g.cs
  - src/qyl.collector/Storage/DuckDbSchema.g.cs
  - src/qyl.dashboard/src/types/api.ts
```

## packages

```yaml
dependencies:
  - "@typespec/compiler": "1.7.0"
  - "@typespec/http": "1.7.0"
  - "@typespec/rest": "0.77.0"
  - "@typespec/openapi": "1.7.0"
  - "@typespec/openapi3": "1.7.0"
  - "@typespec/json-schema": "1.7.0"
  - "@typespec/versioning": "0.77.0"
  - "@typespec/sse": "0.77.0"
  - "@typespec/events": "0.77.0"
```

## rules

```yaml
- never-edit-openapi.yaml: generated from typespec
- never-edit-*.g.cs: generated from openapi
- never-edit-api.ts: generated from openapi
- extensions-required: all storage models need x-duckdb-* extensions
```
