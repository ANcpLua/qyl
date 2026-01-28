# tests

Test infrastructure using xUnit v3 with Microsoft Testing Platform.

## identity

```yaml
sdk: ANcpLua.NET.Sdk.Test
framework: xUnit v3 (3.2.2)
runner: Microsoft.Testing.Platform v2
```

## structure

```yaml
qyl.collector.tests/
  Diagnostics/       # Diagnostic tests
  Helpers/           # Test utilities (DuckDbTestHelpers)
  Ingestion/         # OTLP parsing tests
  Integration/       # End-to-end API tests
  Query/             # Query service tests
  Storage/           # DuckDB storage tests
```

## commands

```yaml
all: dotnet test
filter: dotnet test --filter "FullyQualifiedName~Integration"
coverage: nuke Coverage
```

## patterns

```csharp
// xUnit v3 with MTP
public class MyTests
{
    [Fact]
    public async Task Should_DoSomething()
    {
        // Arrange, Act, Assert
    }
}

// DuckDB test helper
using var db = DuckDbTestHelpers.CreateInMemory();
```

## packages

```yaml
- xunit.v3.mtp-v2
- Microsoft.Testing.Extensions.TrxReport
- Microsoft.AspNetCore.Mvc.Testing
```

## rules

- Use descriptive test names (Should_X_When_Y)
- In-memory DuckDB for isolation
- No mocking of core types
