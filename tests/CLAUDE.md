# tests — xUnit v3 with Microsoft Testing Platform

@import "../CLAUDE.md"

## Overview

Test projects using **xUnit v3** with **Microsoft Testing Platform (MTP)**. All test projects are executables that can
run standalone or via `dotnet test`.

## Test Framework Stack

| Component                                   | Version | Purpose                          |
|---------------------------------------------|---------|----------------------------------|
| `xunit.v3`                                  | 3.2.0   | Test framework + MTP integration |
| `Microsoft.Testing.Extensions.TrxReport`    | 1.6.2   | TRX result generation            |
| `Microsoft.Testing.Extensions.CodeCoverage` | 17.14.2 | Native code coverage             |

## Project Structure

```
tests/
├── Directory.Build.props      # Shared test config (OutputType=Exe, xunit.v3)
├── xunit.v3.runner.json       # Shared xUnit v3 configuration
├── qyl.collector.tests/       # DuckDbStore integration tests
│   └── Storage/
│       └── DuckDbStoreTests.cs
└── UnitTests/
    └── qyl.mcp.server.tests/  # MCP server unit tests
```

## Key xUnit v3 Changes

### IAsyncLifetime Uses ValueTask

```csharp
// xUnit v3 requires ValueTask, not Task
public sealed class MyTests : IAsyncLifetime
{
    public async ValueTask InitializeAsync() { }  // Changed from Task
    public async ValueTask DisposeAsync() { }     // Changed from Task
}
```

### Test Execution

Tests run as executables via MTP:

```bash
# Direct execution (recommended)
./tests/qyl.collector.tests/bin/Debug/net10.0/qyl.collector.tests

# List tests
./tests/qyl.collector.tests/bin/Debug/net10.0/qyl.collector.tests -list tests

# Filter tests (xUnit v3 query syntax)
./tests/qyl.collector.tests/bin/Debug/net10.0/qyl.collector.tests \
  -filter "/*/qyl.collector.tests.Storage/DuckDbStoreTests/*"

# With TRX report
./tests/qyl.collector.tests/bin/Debug/net10.0/qyl.collector.tests \
  --report-trx --report-trx-filename results.trx

# Via Nuke
./eng/build.sh Test
./eng/build.sh Coverage
```

### Filter Syntax

xUnit v3 uses query filter language (`/assembly/namespace/class/method`):

```bash
# By class
-filter "/*/qyl.collector.tests.Storage/DuckDbStoreTests/*"

# By method pattern
-filter "/*/*/*/Insert*"

# Simple filters (alternative)
-class "qyl.collector.tests.Storage.DuckDbStoreTests"
-method "InsertSpan*"
```

## Nuke Build Integration

The Nuke build system (`eng/build/`) provides MTP-aware test targets:

| Target             | Description                           |
|--------------------|---------------------------------------|
| `Test`             | Run all tests with MTP                |
| `UnitTests`        | Run `*.Unit.*` namespace tests        |
| `IntegrationTests` | Run `*.Integration.*` namespace tests |
| `Coverage`         | Run tests with code coverage          |

### MTP Arguments Builder

`MtpExtensions.cs` provides fluent builder for MTP CLI args:

```csharp
var mtp = MtpExtensions.Mtp()
    .ResultsDirectory(TestResultsDirectory)
    .ReportTrx("results.trx")
    .FilterNamespace("*.Unit.*")
    .CoverageCobertura(coverageOutput)
    .StopOnFail()
    .ShowLiveOutput();
```

## Configuration

### xunit.v3.runner.json (Shared)

```json
{
  "$schema": "https://xunit.net/schema/v3/xunit.v3.runner.schema.json",
  "diagnostics": false,
  "methodDisplay": "method",
  "parallelAlgorithm": "conservative",
  "maxParallelThreads": 0,
  "stopOnFail": false
}
```

### Directory.Build.props

```xml

<PropertyGroup>
    <OutputType>Exe</OutputType>           <!-- MTP requires Exe -->
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707;VSTHRD111</NoWarn>
</PropertyGroup>

<ItemGroup>
<PackageReference Include="xunit.v3"/>
<PackageReference Include="Microsoft.Testing.Extensions.TrxReport"/>
<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage"/>
</ItemGroup>
```

## Test Helpers (`Helpers/`)

Reusable test infrastructure to reduce boilerplate.

### TestConstants.cs

Shared constants for all test data:

```csharp
// Database
TestConstants.InMemoryDb           // ":memory:"
TestConstants.DefaultJobQueueCapacity  // 100

// Processing delays
TestConstants.SchemaInitDelayMs    // 100
TestConstants.BatchProcessingDelayMs   // 200

// IDs
TestConstants.SessionDefault       // "session-001"
TestConstants.TraceDefault         // "trace-001"
TestConstants.ProviderOpenAi       // "openai"

// Values
TestConstants.TokensInDefault      // 50
TestConstants.CostDefault          // 0.02m
```

### SpanBuilder.cs

Fluent builder for `SpanRecord`:

```csharp
// Basic span
var span = SpanBuilder.Create("trace-001", "span-001")
    .WithSessionId("session-001")
    .WithTiming(DateTime.UtcNow, durationMs: 100)
    .Build();

// GenAI span with defaults
var genAiSpan = SpanBuilder.GenAi("trace-001", "span-001")
    .WithSessionId("session-001")
    .Build();

// Minimal span
var minimal = SpanBuilder.Minimal("trace-001", "span-001").Build();
```

### SpanFactory.cs

Factory methods for common test scenarios:

```csharp
// Batch of sequential spans
var batch = SpanFactory.CreateBatch(traceId, sessionId, count: 5, baseTime);

// Trace hierarchy (root + children)
var hierarchy = SpanFactory.CreateHierarchy(traceId, baseTime, childCount: 2);

// Archive test data (old + new spans)
var archive = SpanFactory.CreateArchiveTestData(sessionId, now, oldDays: 2);

// GenAI stats test data
var stats = SpanFactory.CreateGenAiStats(sessionId, baseTime);

// Large JSON data span
var large = SpanFactory.CreateLargeDataSpan(traceId, spanId, padding: 10000);
```

### DuckDbTestHelpers.cs

Helper methods and extensions:

```csharp
// Create store
var store = DuckDbTestHelpers.CreateInMemoryStore();

// Enqueue and wait
await DuckDbTestHelpers.EnqueueAndWaitAsync(store, span);
await DuckDbTestHelpers.EnqueueAndWaitAsync(store, batch, delayMs: 300);

// Temp directory (auto-cleanup)
using var tempDir = new TempDirectory();
var files = tempDir.GetParquetFiles();

// Query extensions
var columns = await connection.GetTableColumnsAsync("spans");
var exists = await connection.TableExistsAsync("sessions");
var count = await connection.CountSpansAsync(traceId, spanId);
var duration = await connection.GetDurationMsAsync(spanId);
```

## Test Patterns

### In-Memory DuckDB Tests

```csharp
public sealed class DuckDbStoreTests : IAsyncLifetime
{
    private DuckDbStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
    }

    [Fact]
    public async Task InsertSpan_RoundTrip()
    {
        var span = SpanBuilder.GenAi(TestConstants.TraceDefault, TestConstants.SpanDefault)
            .WithSessionId(TestConstants.SessionDefault)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);
        var results = await _store.GetSpansBySessionAsync(TestConstants.SessionDefault);

        Assert.Single(results);
    }
}
```

### Test Categories via Namespaces

```
tests/
├── UnitTests/           # Fast, isolated tests
│   └── *.Unit.*
└── IntegrationTests/    # Tests with external deps
    └── *.Integration.*
```

## Coverage

Native MTP coverage via `Microsoft.Testing.Extensions.CodeCoverage`:

```bash
# Direct
./tests/.../qyl.collector.tests \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-output coverage.xml

# Via Nuke
./eng/build.sh Coverage
```

---
agent: 'agent'
tools: ['changes', 'search/codebase', 'edit/editFiles', 'problems', 'search']
description: 'Get best practices for XUnit unit testing, including data-driven tests'
---

# XUnit Best Practices

Your goal is to help me write effective unit tests with XUnit, covering both standard and data-driven testing
approaches.

## Project Setup

- Use a separate test project with naming convention `[ProjectName].Tests`
- Reference Microsoft.NET.Test.Sdk, xunit, and xunit.runner.visualstudio packages
- Create test classes that match the classes being tested (e.g., `CalculatorTests` for `Calculator`)
- Use .NET SDK test commands: `dotnet test` for running tests

## Test Structure

- No test class attributes required (unlike MSTest/NUnit)
- Use fact-based tests with `[Fact]` attribute for simple tests
- Follow the Arrange-Act-Assert (AAA) pattern
- Name tests using the pattern `MethodName_Scenario_ExpectedBehavior`
- Use constructor for setup and `IDisposable.Dispose()` for teardown
- Use `IClassFixture<T>` for shared context between tests in a class
- Use `ICollectionFixture<T>` for shared context between multiple test classes

## Standard Tests

- Keep tests focused on a single behavior
- Avoid testing multiple behaviors in one test method
- Use clear assertions that express intent
- Include only the assertions needed to verify the test case
- Make tests independent and idempotent (can run in any order)
- Avoid test interdependencies

## Data-Driven Tests

- Use `[Theory]` combined with data source attributes
- Use `[InlineData]` for inline test data
- Use `[MemberData]` for method-based test data
- Use `[ClassData]` for class-based test data
- Create custom data attributes by implementing `DataAttribute`
- Use meaningful parameter names in data-driven tests

## Assertions

- Use `Assert.Equal` for value equality
- Use `Assert.Same` for reference equality
- Use `Assert.True`/`Assert.False` for boolean conditions
- Use `Assert.Contains`/`Assert.DoesNotContain` for collections
- Use `Assert.Matches`/`Assert.DoesNotMatch` for regex pattern matching
- Use `Assert.Throws<T>` or `await Assert.ThrowsAsync<T>` to test exceptions
- Use fluent assertions library for more readable assertions

## Mocking and Isolation

- Consider using Moq or NSubstitute alongside XUnit
- Mock dependencies to isolate units under test
- Use interfaces to facilitate mocking
- Consider using a DI container for complex test setups

## Test Organization

- Group tests by feature or component
- Use `[Trait("Category", "CategoryName")]` for categorization
- Use collection fixtures to group tests with shared dependencies
- Consider output helpers (`ITestOutputHelper`) for test diagnostics
- Skip tests conditionally with `Skip = "reason"` in fact/theory attributes
-

## Migration from xUnit v2

| xUnit v2                    | xUnit v3                      |
|-----------------------------|-------------------------------|
| `Task InitializeAsync()`    | `ValueTask InitializeAsync()` |
| `Task DisposeAsync()`       | `ValueTask DisposeAsync()`    |
| `xunit.runner.json`         | `xunit.v3.runner.json`        |
| `Microsoft.NET.Test.Sdk`    | Not needed (MTP built-in)     |
| `xunit.runner.visualstudio` | Not needed (MTP built-in)     |
| VSTest adapter              | Microsoft Testing Platform    |

## Troubleshooting

### "unknown option: --list-tests"

Use xUnit v3 syntax: `-list tests` (not `--list-tests`)

### Tests not discovered

Ensure `<OutputType>Exe</OutputType>` is set in project.

### IAsyncLifetime errors

Change `Task` to `ValueTask` for `InitializeAsync`/`DisposeAsync`.
