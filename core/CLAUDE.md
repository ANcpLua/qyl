# core/specs — TypeSpec God Schema

Single source of truth for all types.

## identity

```yaml
name: core/specs
type: typespec-schema
version: "2.0.0"
role: single-source-of-truth
semconv: OTel 1.39
```

## packages

```yaml
# from package.json
"@typespec/compiler": "1.8.0"
"@typespec/events": "0.78.0"
"@typespec/http": "1.8.0"
"@typespec/json-schema": "1.8.0"
"@typespec/openapi": "1.8.0"
"@typespec/openapi3": "1.8.0"
"@typespec/rest": "0.78.0"
"@typespec/sse": "0.78.0"
"@typespec/streams": "0.78.0"
"@typespec/versioning": "0.78.0"
"openapi-typescript": "^7.10.1"
```

## tspconfig

```yaml
emit:
  - "@typespec/openapi3"
  - "@typespec/json-schema"

options:
  "@typespec/openapi3":
    output-file: openapi.yaml
    emitter-output-dir: "{project-root}/../openapi"
    file-type: yaml
    openapi-versions: ["3.1.0"]
    
  "@typespec/json-schema":
    emitter-output-dir: "{project-root}/../schemas"
    file-type: json
    bundleId: qyl-telemetry

linter:
  extends:
    - "@typespec/http/all"
```

## file-structure

```yaml
core/specs/:
  main.tsp              # Entry point (48 domain files)
  package.json
  tspconfig.yaml
  
  common/:
    types.tsp           # Primitives (SpanId, TraceId, etc.)
    errors.tsp
    pagination.tsp
    
  otel/:
    enums.tsp           # SpanKind, StatusCode, SeverityNumber
    resource.tsp        # OTel Resource
    span.tsp            # Span, SpanEvent, SpanLink
    logs.tsp            # LogRecord
    metrics.tsp
    
  storage/:
    storage.tsp         # SpanRecord, SessionSummary (x-duckdb-table)
    
  domains/:
    ai/:
      genai.tsp         # gen_ai.* semconv
      code.tsp
      cli.tsp
    security/:
      network.tsp
      dns.tsp
      tls.tsp
      security-rule.tsp
    transport/:
      http.tsp
      rpc.tsp
      messaging.tsp
      url.tsp
      signalr.tsp
      kestrel.tsp
      user-agent.tsp
    infra/:
      host.tsp
      container.tsp
      k8s.tsp
      cloud.tsp
      faas.tsp
      os.tsp
      webengine.tsp
    runtime/:
      process.tsp
      system.tsp
      thread.tsp
      dotnet.tsp
      aspnetcore.tsp
    data/:
      db.tsp
      file.tsp
      elasticsearch.tsp
      vcs.tsp
      artifact.tsp
    observe/:
      session.tsp
      browser.tsp
      feature-flags.tsp
      exceptions.tsp
      otel.tsp
      log.tsp
      error.tsp
      test.tsp
    ops/:
      cicd.tsp
      deployment.tsp
    identity/:
      user.tsp
      geo.tsp
      
  api/:
    routes.tsp          # REST API definitions
    streaming.tsp       # SSE
```

## extensions

```yaml
x-csharp-type:
  purpose: override C# type name
  usage: '@extension("x-csharp-type", "SpanRecord")'
  read-by: SchemaGenerator.ResolveCSharpType()
  
x-duckdb-table:
  purpose: mark model as DuckDB table
  usage: '@extension("x-duckdb-table", "spans")'
  read-by: SchemaGenerator.GenerateDuckDb()
  output: CREATE TABLE IF NOT EXISTS {value}
  
x-duckdb-column:
  purpose: override column name
  usage: '@extension("x-duckdb-column", "span_id")'
  default: ToSnakeCase(propertyName)
  
x-duckdb-type:
  purpose: override DuckDB type
  usage: '@extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")'
  
x-duckdb-primary-key:
  purpose: mark as primary key
  usage: '@extension("x-duckdb-primary-key", true)'
  output: PRIMARY KEY ({column})
  
x-duckdb-index:
  purpose: create index
  usage: '@extension("x-duckdb-index", "idx_spans_trace_id")'
  output: CREATE INDEX IF NOT EXISTS {value} ON {table}({column})
  
x-primitive:
  purpose: mark as strongly-typed wrapper
  usage: '@extension("x-primitive", true)'
  read-by: schema.IsScalar
  
x-promoted:
  purpose: mark as promoted from attributes_json to column
  usage: '@extension("x-promoted", true)'
  doc-only: true
  
x-enum-varnames:
  purpose: provide enum member names
  usage: '@extension("x-enum-varnames", "Unspecified,Internal,Server,Client")'
  read-by: SchemaDefinition.GetEnumVarNames()
```

## example-model

```yaml
typespec: |
  @doc("Storage row for spans table")
  @extension("x-duckdb-table", "spans")
  @extension("x-csharp-type", "SpanRecord")
  model SpanRecord {
    @doc("Unique span identifier")
    @key
    @extension("x-duckdb-column", "span_id")
    @extension("x-duckdb-primary-key", true)
    spanId: SpanId;
    
    @doc("Trace identifier")
    @extension("x-duckdb-column", "trace_id")
    @extension("x-duckdb-index", "idx_spans_trace_id")
    traceId: TraceId;
    
    @doc("GenAI provider name")
    @extension("x-duckdb-column", "gen_ai_provider_name")
    @extension("x-promoted", true)
    genAiProviderName?: string;
  }

generates:
  csharp: |
    public sealed record SpanRecord
    {
        [JsonPropertyName("span_id")]
        public required SpanId SpanId { get; init; }
        
        [JsonPropertyName("trace_id")]
        public required TraceId TraceId { get; init; }
        
        [JsonPropertyName("gen_ai_provider_name")]
        public string? GenAiProviderName { get; init; }
    }
    
  duckdb: |
    CREATE TABLE IF NOT EXISTS spans (
        span_id VARCHAR PRIMARY KEY,
        trace_id VARCHAR NOT NULL,
        gen_ai_provider_name VARCHAR,
        ...
    );
    CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
```

## compilation

```yaml
command: npm run compile
# or: tsp compile main.tsp

output:
  - ../openapi/openapi.yaml
  - ../schemas/*.json

post-process:
  - nuke Generate (reads openapi.yaml → C#/DuckDB)
  - npm run generate:types (reads openapi.yaml → TypeScript)
```

## rules

```yaml
- never edit openapi.yaml manually
- never edit *.g.cs manually
- never edit api.ts manually
- all storage models need x-duckdb-* extensions
- run nuke Generate after TypeSpec changes
```
