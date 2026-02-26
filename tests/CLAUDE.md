# tests - Test Infrastructure

xUnit v3 with Microsoft Testing Platform.

## Identity

| Property   | Value                         |
|------------|-------------------------------|
| SDK        | ANcpLua.NET.Sdk.Test          |
| Framework  | xUnit v3                      |
| Runner     | Microsoft Testing Platform v2 |
| Assertions | AwesomeAssertions             |

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

## ADR Verification Tests

Each ADR has acceptance criteria that translate to tests:

| ADR | Test Focus |
|-----|-----------|
| ADR-001 | Health endpoints return 200, OTLP ingestion works, dashboard serves HTML |
| ADR-002 | No token → onboarding response, with token → repos endpoint, OTLP has no auth gate |
| ADR-003 | Source generators emit interceptors, build succeeds with/without package |
| ADR-005 | Chat endpoint with LLM returns response, without LLM → agent disabled message |

Visual verification via Playwright MCP (browser: msedge) for dashboard rendering.

## Known Issue: dotnet test with slnx

`dotnet test qyl.slnx` fails because `qyl.dashboard.esproj` triggers esbuild version mismatch.
Always target the test project directly:
```bash
dotnet test --project tests/qyl.collector.tests/qyl.collector.tests.csproj --no-build
```

## Rules

- In-memory DuckDB for test isolation (`DuckDbTestHelpers.CreateInMemory()`)
- No mocking of core domain types
- Prefer integration tests for API endpoints
- Test names: `Should_X_When_Y` or `Method_Scenario_Expected`
