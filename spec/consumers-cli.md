You are reviewing or modifying the qyl CLI client (qylCli.cs).

### SCOPE

Includes:

- cli/qylCli.cs
- Commands:
  • query sessions
  • query traces
  • export parquet
- Any supporting helpers

### GOAL

Provide a stable, schema-correct command-line interface for interacting with the
collector REST APIs.

### REQUIRED ACTIONS

1. Schema Alignment
  - CLI MUST use DTOs from core/dotnet or auto-generated models.
  - MUST NOT redefine telemetry types.
  - MUST accept server snake_case field names as-is.

2. REST Client Behavior
  - MUST use HttpClient with System.Text.Json using:
    JsonSerializerOptions.Web
    JsonNamingPolicy.SnakeCaseLower
  - MUST stream large responses using IAsyncEnumerable<T>.

3. Command Rules
  - query sessions:
    MUST return complete session aggregates.
    MUST present tokens (input/output/total) correctly.
  - query traces:
    MUST preserve chronological ordering.
  - export parquet:
    MUST use canonical schema; MUST NOT reshape fields.

4. Performance Constraints
  - MUST avoid loading entire result sets into memory.
  - MUST use streaming enumeration + async where supported.

5. UX Requirements
  - CLI commands MUST provide:
    --limit
    --since / --until
    --filter model/provider/operation
    --output json|table|parquet
  - Error messages MUST be structured and human-readable.

6. Dependency Rules
  - cli/* → collector/api (HTTP)
  - cli/* MUST NOT depend on:
    dashboard/*
    instrumentation/*
    collector/processing/*
    collector/storage/*

7. Cross-Cutting Semantic Rules
  - CLI MUST display tokens consistent with collector logic.
  - MUST NOT rename or reinterpret telemetry fields.
  - MUST preserve snake_case serialization for all outputs.

### DEFINITION OF DONE

- CLI commands query API correctly and type-safely.
- Streaming support implemented.
- No schema drift.
- No dependency violations.
- Output formats stable for automation use cases.
