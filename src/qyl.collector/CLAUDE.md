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
