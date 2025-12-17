# ADR-0002: VS-01 Span Ingestion

## Metadata

| Field      | Value      |
|------------|------------|
| Status     | Draft      |
| Date       | 2025-12-16 |
| Slice      | VS-01      |
| Priority   | P0         |
| Depends On | -          |
| Supersedes | -          |

## Context

qyl muss OpenTelemetry Spans empfangen, gen_ai.* Attribute extrahieren und in DuckDB speichern. Dies ist der
fundamentale Slice, von dem alle anderen abhängen.

## Decision

Implementierung eines OTLP-kompatiblen Receivers mit:

- HTTP Endpoint auf Port 4318 (`POST /v1/traces`)
- gRPC Service auf Port 4317 (`TraceService.Export`)
- Zero-allocation JSON Parsing für Performance
- Schema-Normalisierung (deprecated Attributes → v1.38)
- Batch-Insert via DuckDB Appender

## Layers

### 1. TypeSpec (Contract)

```yaml
files:
  - core/specs/storage/tables.tsp        # spans table definition
  - core/specs/models/spans.tsp          # SpanRecord model
  - core/specs/otel/semconv.tsp          # gen_ai.* attributes (v1.38)
generates:
  - core/generated/duckdb/schema.sql     # CREATE TABLE spans
  - core/generated/duckdb/DuckDbSchema.g.cs  # FrozenDictionary mappings
```

### 2. Protocol Layer (Shared Types)

```yaml
files:
  - src/qyl.protocol/Models/SpanRecord.cs       # Flattened span for DuckDB
  - src/qyl.protocol/Models/GenAiSpanData.cs    # Extracted gen_ai.* data
  - src/qyl.protocol/Attributes/GenAiAttributes.cs  # OTel 1.38 constants
  - src/qyl.protocol/Primitives/UnixNano.cs     # Unix timestamp nanoseconds
  - src/qyl.protocol/Primitives/SessionId.cs    # Session identifier
patterns:
  - "readonly record struct für Value Types"
  - "FrozenSet<string> für Attribute-Lookup"
```

### 3. Storage Layer

```yaml
files:
  - src/qyl.collector/Storage/DuckDbSchema.cs   # DDL + migrations (SINGLE SOURCE)
  - src/qyl.collector/Storage/DuckDbStore.cs    # ISpanStore implementation
  - src/qyl.collector/Storage/SpanRecord.cs     # Span data model
  - src/qyl.collector/Storage/SpanBatch.cs      # Batch insert model
methods:
  - "InitializeAsync()"           # CREATE TABLE IF NOT EXISTS
  - "InsertSpansAsync(spans)"     # Batch insert via Appender
  - "GetSpanAsync(spanId)"        # Single span lookup
patterns:
  - "Lock.EnterScope() for writes"
  - "FrozenDictionary for attribute column mapping"
  - "DuckDB Appender for bulk inserts"
```

### 4. Ingestion Layer

```yaml
files:
  - src/qyl.collector/Ingestion/OtlpJsonSpanParser.cs   # Zero-allocation JSON parser
  - src/qyl.collector/Ingestion/OtlpAttributes.cs       # OTLP attribute constants
  - src/qyl.collector/Ingestion/OtlpTypes.cs            # OTLP DTOs
  - src/qyl.collector/Ingestion/SchemaNormalizer.cs     # Migrate to v1.38
  - src/qyl.collector/GenAiExtractor.cs                 # Extract gen_ai.* attributes
  - src/qyl.collector/Pipeline/SpanIngestionPipeline.cs # Channel<T> backpressure
patterns:
  - "Channel<SpanBatch> for backpressure"
  - "Zero-allocation JSON parsing"
  - "FrozenDictionary for deprecated attribute mapping"
```

### 5. API Layer

```yaml
endpoints:
  - "POST /v1/traces (OTLP HTTP, port 4318)"
  - "gRPC TraceService.Export (port 4317)"
files:
  - src/qyl.collector/Program.cs                # Endpoint registration
response:
  - "200 OK + empty body (OTLP spec)"
  - "400 Bad Request + ErrorResponse"
```

### 6. MCP Layer

```yaml
note: "VS-01 hat keine MCP Tools (nur Ingestion)"
```

### 7. Dashboard Layer

```yaml
note: "VS-01 hat keine Dashboard Components (nur Backend)"
```

## Acceptance Criteria

- [ ] TypeSpec `core/specs/storage/tables.tsp` compiliert ohne Fehler
- [ ] DuckDB schema.sql wird generiert
- [ ] `POST /v1/traces` akzeptiert OTLP JSON und gibt 200 zurück
- [ ] gRPC `TraceService.Export` funktioniert
- [ ] Spans erscheinen in DuckDB `spans` Tabelle
- [ ] gen_ai.* Attribute werden zu promoted columns extrahiert
- [ ] Deprecated Attributes migriert (prompt_tokens → input_tokens)
- [ ] Channel-basierte Pipeline verhindert Backpressure-Probleme
- [ ] Tests für OtlpJsonSpanParser vorhanden und grün
- [ ] Tests für GenAiExtractor vorhanden und grün
- [ ] Tests für DuckDbStore vorhanden und grün

## Test Files

```yaml
unit_tests:
  - tests/qyl.collector.tests/Ingestion/OtlpJsonSpanParserTests.cs
  - tests/qyl.collector.tests/GenAiExtractorTests.cs
  - tests/qyl.collector.tests/Ingestion/SchemaNormalizerTests.cs
integration_tests:
  - tests/qyl.collector.tests/Storage/DuckDbStoreTests.cs
  - tests/qyl.collector.tests/OtlpIngestionTests.cs
```

## DuckDB Schema (spans table)

> **Note**: Column names use OTel 1.38 dot-notation (quoted in DuckDB).

```sql
CREATE TABLE IF NOT EXISTS spans (
    -- Core identifiers
    trace_id VARCHAR NOT NULL,
    span_id VARCHAR NOT NULL,
    parent_span_id VARCHAR,

    -- Core span fields
    name VARCHAR NOT NULL,
    kind VARCHAR,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    duration_ms DOUBLE GENERATED ALWAYS AS (
        EXTRACT(EPOCH FROM (end_time - start_time)) * 1000
    ),
    status_code INT,
    status_message VARCHAR,

    -- Resource attributes (OTel 1.38)
    "service.name" VARCHAR,

    -- Session tracking (OTel 1.38)
    "session.id" VARCHAR,

    -- GenAI attributes (OTel 1.38) - BIGINT for token counts
    "gen_ai.provider.name" VARCHAR,
    "gen_ai.request.model" VARCHAR,
    "gen_ai.response.model" VARCHAR,
    "gen_ai.operation.name" VARCHAR,
    "gen_ai.usage.input_tokens" BIGINT,
    "gen_ai.usage.output_tokens" BIGINT,

    -- qyl extensions
    cost_usd DECIMAL(10,6),
    eval_score FLOAT,
    eval_reason VARCHAR,

    -- Flexible storage
    attributes JSON,
    events JSON,

    PRIMARY KEY (trace_id, span_id)
);

CREATE INDEX idx_spans_time ON spans (start_time);
CREATE INDEX idx_spans_session ON spans ("session.id");
CREATE INDEX idx_spans_provider ON spans ("gen_ai.provider.name") WHERE "gen_ai.provider.name" IS NOT NULL;
CREATE INDEX idx_spans_service ON spans ("service.name");
```

## Schema Normalization (v1.38 Migrations)

```yaml
migrations:
  - from: "gen_ai.system"
    to: "gen_ai.provider.name"
    since: "1.37"

  - from: "gen_ai.usage.prompt_tokens"
    to: "gen_ai.usage.input_tokens"
    since: "1.38"

  - from: "gen_ai.usage.completion_tokens"
    to: "gen_ai.usage.output_tokens"
    since: "1.38"

  - from: "gen_ai.request.max_tokens"
    to: "gen_ai.request.max_output_tokens"
    since: "1.38"
```

## Consequences

### Positive

- **Fundament für alle Slices**: Ohne VS-01 funktioniert nichts
- **Performance**: Zero-allocation Parsing + DuckDB Appender
- **Standards-konform**: OTLP HTTP + gRPC
- **Zukunftssicher**: Schema-Normalisierung auf v1.38

### Negative

- **Komplexität**: Viele Files für einen "einfachen" Receiver
- **Migration bei Schema-Änderungen**: DuckDB Schema-Updates benötigen Migration

### Risks

| Risk                          | Impact | Likelihood | Mitigation                           |
|-------------------------------|--------|------------|--------------------------------------|
| DuckDB Schema Breaking Change | High   | Low        | Schema-Versioning in DuckDbSchema.cs |
| OTLP Spec Update              | Medium | Medium     | Abstraktion über OtlpTypes.cs        |
| Backpressure bei Lastspitzen  | High   | Medium     | Bounded Channel + Monitoring         |

## References

- [OpenTelemetry Protocol Specification](https://opentelemetry.io/docs/specs/otlp/)
- [OTel Semantic Conventions v1.38](https://opentelemetry.io/docs/specs/semconv/)
- [DuckDB.NET Documentation](https://duckdb.net/)
- [qyl-architecture.yaml](../../qyl-architecture.yaml) Section: storage, otel_semconv
