# tests - Test Infrastructure

Test projects using xUnit v3 with Microsoft Testing Platform.

## Identity

| Property  | Value                         |
|-----------|-------------------------------|
| SDK       | ANcpLua.NET.Sdk.Test          |
| Framework | xUnit v3 (3.2.2)              |
| Runner    | Microsoft Testing Platform v2 |

## Test Projects

```
qyl.collector.tests/
  Diagnostics/           # Diagnostic tests
  Helpers/               # Test utilities (DuckDbTestHelpers)
  Ingestion/             # OTLP parsing tests
  Integration/           # End-to-end API tests
  Query/                 # Query service tests
  Storage/               # DuckDB storage tests
```

## Commands

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/qyl.collector.tests

# Filter tests
dotnet test --filter "FullyQualifiedName~Integration"

# With coverage
nuke Coverage
```

## Test Patterns

### Basic Test

```csharp
public class SessionQueryTests
{
    [Fact]
    public async Task GetSession_WhenExists_ReturnsSession()
    {
        // Arrange
        using var db = DuckDbTestHelpers.CreateInMemory();
        var service = new SessionQueryService(db);

        // Act
        var result = await service.GetSessionAsync("session-123");

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be("session-123");
    }
}
```

### DuckDB Test Helper

```csharp
// In-memory database for isolation
using var db = DuckDbTestHelpers.CreateInMemory();

// Pre-seed test data
await db.InsertSpansAsync(new[]
{
    new SpanRecord { TraceId = "trace-1", SpanId = "span-1" }
});
```

### Integration Test with WebApplicationFactory

```csharp
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTraces_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/traces");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Packages

| Package                                  | Purpose                  |
|------------------------------------------|--------------------------|
| `xunit.v3.mtp-v2`                        | xUnit v3 with MTP        |
| `Microsoft.Testing.Extensions.TrxReport` | TRX report generation    |
| `Microsoft.AspNetCore.Mvc.Testing`       | Integration test factory |
| `AwesomeAssertions`                      | Fluent assertions        |

## Naming Conventions

Use descriptive test names following `Should_X_When_Y`:

```csharp
[Fact]
public async Task Should_ReturnNotFound_When_TraceIdDoesNotExist()

[Fact]
public async Task Should_FilterByServiceName_When_ProviderSpecified()

[Fact]
public async Task Should_IncludeChildSpans_When_FetchingTrace()
```

## Rules

- Use in-memory DuckDB for test isolation
- No mocking of core domain types
- Prefer integration tests over unit tests for API endpoints
- Clean up test data in `Dispose()` if using shared fixtures