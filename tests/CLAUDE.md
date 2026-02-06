# tests - Test Infrastructure

xUnit v3 with Microsoft Testing Platform.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk.Test |
| Framework | xUnit v3 |
| Runner | Microsoft Testing Platform v2 |
| Assertions | AwesomeAssertions |

## Commands

```bash
dotnet test                                      # All tests
dotnet test tests/qyl.collector.tests            # Specific project
dotnet test --filter "FullyQualifiedName~Integration"  # Filter
nuke Coverage                                    # With coverage
```

## Test Project Structure

```
qyl.collector.tests/
  Diagnostics/    # Diagnostic tests
  Helpers/        # DuckDbTestHelpers
  Ingestion/      # OTLP parsing tests
  Integration/    # End-to-end API tests
  Query/          # Query service tests
  Storage/        # DuckDB storage tests
```

## Rules

- In-memory DuckDB for test isolation (`DuckDbTestHelpers.CreateInMemory()`)
- No mocking of core domain types
- Prefer integration tests for API endpoints
- Test names: `Should_X_When_Y` or `Method_Scenario_Expected`
