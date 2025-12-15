# qyl.collector

@import "../../CLAUDE.md"

## Scope

## Project Info

| Property      | Value   |
|---------------|---------|
| Layer         |         |
| Framework     | net10.0 |
| Workflow      |         |
| Test Coverage | %       |

## Critical Files

| File                    | Reason                                       |
|-------------------------|----------------------------------------------|
| Storage/DuckDbStore.cs  | Core persistence - schema changes break data |
| Storage/DuckDbSchema.cs | DDL definitions - must match DuckDbStore     |
| Program.cs              | DI registration - service configuration      |

## Anti-Patterns (FORBIDDEN)

| Pattern           | Use Instead                       | Severity |
|-------------------|-----------------------------------|----------|
| `DateTime.UtcNow` | `TimeProvider.System.GetUtcNow()` | Error    |
| `object _lock`    | `Lock _lock = new()`              | Error    |

## Required Patterns

| Pattern                         | Description                                    |
|---------------------------------|------------------------------------------------|
| `Lock`                          | Use .NET 9+ Lock class for all synchronization |
| `TypedResults.ServerSentEvents` | Use for all SSE endpoints                      |
