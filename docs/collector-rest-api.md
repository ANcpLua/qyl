You are reviewing or modifying the qyl collector REST API.

### SCOPE

Includes:

- QueryController.cs
- SessionsController.cs
- LogsController.cs
- MetricsController.cs
- DTOs: SpanDto.cs, SessionDto.cs, LogDto.cs, MetricDto.cs, GenAiDto.cs
- MappingExtensions.cs

### GOAL

Expose stable, schema-correct, snake_case JSON APIs for dashboard and CLI.

### REQUIRED ACTIONS

1. DTO Schema Fidelity
  - All DTO properties MUST match core/schema/*.json exactly.
  - MUST NOT introduce dashboard-only fields.
  - MUST NOT rename fields (snake_case handled by serializer).

2. Mapping Rules
  - MappingExtensions MUST contain all transformations.
  - MUST handle missing fields gracefully.
  - MUST compute total_tokens = input_tokens + output_tokens.

3. JSON Serialization Requirements
  - MUST use:
    JsonNamingPolicy.SnakeCaseLower
    JsonSerializerOptions.Web
  - MUST support IAsyncEnumerable<T> streaming for large responses.

4. Query Semantics
  - Filters MUST match dashboard expectations.
  - Time range queries MUST return chronologically sorted results.

5. Dependency Rules
  - api/* → storage/*
  - MUST NOT depend on:
    • streaming/*
    • dashboard/*
    • cli/*
    • instrumentation/*

### DEFINITION OF DONE

- REST API returns schema-perfect objects.
- Snake_case everywhere.
- Streaming via IAsyncEnumerable works.
- DTO + schema alignment guaranteed.
- No dependency violations.
