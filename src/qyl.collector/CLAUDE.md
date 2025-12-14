# qyl.collector

@import "../../CLAUDE.md"

## Scope

Backend service: OTLP ingestion, DuckDB storage, REST/SSE APIs

## Project Info

| Property | Value |
|----------|-------|
| Layer | backend |
| Framework | net10.0 |
| Workflow | explore-plan-code-commit |
| Test Coverage | 80% |

## Critical Files

| File | Reason |
|------|--------|
| Storage/DuckDbStore.cs | Core persistence - schema changes break data |
| Storage/DuckDbSchema.cs | DDL definitions - must match DuckDbStore |
| Ingestion/GenAiExtractor.cs | OTel 1.38 compliance - attribute mapping |
| Program.cs | DI registration - service configuration |

## Anti-Patterns (FORBIDDEN)

| Pattern | Use Instead | Severity |
|---------|-------------|----------|
| `DateTime.Now` | `TimeProvider.System.GetLocalNow()` | error |
| `DateTime.UtcNow` | `TimeProvider.System.GetUtcNow()` | error |
| `lock(object)` | `Lock.EnterScope()` | error |
| `gen_ai.system` | `gen_ai.provider.name` | error |

## Required Patterns

| Pattern | Description |
|---------|-------------|
| `Lock` | .NET 9+ Lock class for thread-safe sync |
| `FrozenSet` | Immutable lookup sets for static data |
| `TimeProvider` | Testable time abstraction |
