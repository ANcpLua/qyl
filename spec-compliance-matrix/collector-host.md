You are reviewing or modifying the qyl collector receivers subsystem.

### SCOPE

Includes:

- receivers/http/*
- receivers/grpc/*
- HttpReceiver.cs
- GrpcReceiver.cs
- OTLP/HTTP ingestion
- OTLP/gRPC ingestion
- JSON ingestion paths
- Integration with HostConfig.cs

### GOAL

Ensure receivers ingest spans/logs/metrics correctly, efficiently, consistently,
and in compliance with qyl core schemas and OpenTelemetry semantic conventions.

### REQUIRED ACTIONS

1. Validate OTLP compliance
  - HTTP receiver MUST parse OTLP/JSON exactly as defined in OTel.
  - gRPC receiver MUST parse OTLP/Protobuf exactly as defined.

2. Validate attribute decoding
  - All keys MUST remain snake_case (SnakeCaseLower downstream).
  - MUST NOT rename attributes.
  - MUST NOT introduce attributes not present in schema/*.json.

3. Validate UTF-8 Parsing Rules
  - Numeric fields MUST use IUtf8SpanParsable<T>.
  - MUST avoid intermediate string allocations.

4. Validate Async Semantics
  - MUST use Task.WhenEach() when fan-in across multiple ingestion channels.
  - MUST NOT use Task.WhenAny loops.

5. Validate Memory Safety
  - MUST avoid large allocations; streaming-friendly operations only.
  - MUST avoid ToList(), ToArray() unless required.

6. Validate Concurrency Guarantees
  - MUST NOT introduce shared mutable state.
  - MUST hand off payloads to the processing pipeline without mutation.

7. Validate Schema Alignment
  - All incoming OTLP data MUST map to schema/span.json, metric.json, log.json.
  - Missing required fields MUST be flagged for processing->Normalizer.

8. Validate Collector Dependency Rules
  - receivers/* → processing/*
  - receivers MUST NOT depend on:
    • storage/*
    • api/*
    • streaming/*
    • dashboard/*
    • cli/*

9. Validate Receiver Error Handling
  - Errors MUST be reported via structured logs.
  - MUST NOT throw untyped exceptions.

### DEFINITION OF DONE

- All ingestion paths validated end-to-end.
- UTF-8 parsing optimized.
- Task.WhenEach used.
- No GroupBy/ToDictionary performance traps.
- No dependency rule violations.
- All schemas adhered to.
- No semantic drift or attribute renaming.
