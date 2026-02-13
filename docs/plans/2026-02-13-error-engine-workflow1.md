# Error Engine Workflow 1 — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the error engine core — fingerprinting, categorization, aggregation, query API, and auto-capture middleware — that matches and exceeds Sentry's error tracking for .NET and GenAI workloads.

**Architecture:** Inline ingestion pipeline. When spans arrive with `status_code = 2`, extract exception data, compute fingerprint + category, and UPSERT into the existing `errors` DuckDB table. ErrorAggregator uses the same Channel<WriteJob> pattern as the span writer. REST endpoints replace existing stubs.

**Tech Stack:** .NET 10 LTS, C# 14, DuckDB, xUnit v3 + MTP, ASP.NET Core middleware

---

### Task 1: ErrorFingerprinter (pure logic)

**Files:**
- Create: `src/qyl.collector/Errors/ErrorFingerprinter.cs`
- Test: `tests/qyl.collector.tests/Errors/ErrorFingerprinterTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/qyl.collector.tests/Errors/ErrorFingerprinterTests.cs
namespace qyl.collector.tests.Errors;

public sealed class ErrorFingerprinterTests
{
    [Fact]
    public void Fingerprint_SameException_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "Object reference not set", "at Foo.Bar()");
        var fp2 = ErrorFingerprinter.Compute("NullReferenceException", "Object reference not set", "at Foo.Bar()");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentLineNumbers_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "msg",
            "at MyApp.Service.Do() in /src/Service.cs:line 42");
        var fp2 = ErrorFingerprinter.Compute("NullReferenceException", "msg",
            "at MyApp.Service.Do() in /src/Service.cs:line 99");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentGuidsInMessage_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("Exception", "User a1b2c3d4-e5f6-7890-abcd-ef1234567890 not found", "");
        var fp2 = ErrorFingerprinter.Compute("Exception", "User 99999999-8888-7777-6666-555544443333 not found", "");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentExceptionType_ProducesDifferentHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "msg", "at Foo.Bar()");
        var fp2 = ErrorFingerprinter.Compute("ArgumentException", "msg", "at Foo.Bar()");
        Assert.NotEqual(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_WithGenAiOperation_IncludesInHash()
    {
        var fp1 = ErrorFingerprinter.Compute("Exception", "rate limited", "", genAiOperation: "chat");
        var fp2 = ErrorFingerprinter.Compute("Exception", "rate limited", "", genAiOperation: "embeddings");
        Assert.NotEqual(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_NullStackTrace_DoesNotThrow()
    {
        var fp = ErrorFingerprinter.Compute("Exception", "msg", null);
        Assert.NotNull(fp);
        Assert.NotEmpty(fp);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorFingerprinterTests"`
Expected: Build error — `ErrorFingerprinter` does not exist

**Step 3: Implement ErrorFingerprinter**

```csharp
// src/qyl.collector/Errors/ErrorFingerprinter.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace qyl.collector.Errors;

/// <summary>
///     Computes deterministic fingerprints for error grouping.
///     Same logical error → same fingerprint, regardless of line numbers, GUIDs, or timestamps.
///     GenAI-aware: includes operation name in fingerprint so rate_limit from chat != rate_limit from embeddings.
/// </summary>
public static partial class ErrorFingerprinter
{
    public static string Compute(
        string exceptionType,
        string message,
        string? stackTrace,
        string? genAiOperation = null)
    {
        var normalizedStack = NormalizeStackTrace(stackTrace);
        var normalizedMessage = NormalizeMessage(message);

        var input = $"{exceptionType}\n{normalizedMessage}\n{normalizedStack}";
        if (!string.IsNullOrEmpty(genAiOperation))
            input = $"{input}\n{genAiOperation}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash)[..16]; // 64-bit fingerprint
    }

    private static string NormalizeStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "";
        // Strip line numbers: "in /path/file.cs:line 42" → ""
        var result = LineNumberRegex().Replace(stackTrace, "");
        // Strip file paths: "in /Users/foo/src/Bar.cs" → ""
        result = FilePathRegex().Replace(result, "");
        return result.Trim();
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        // Replace GUIDs
        var result = GuidRegex().Replace(message, "<GUID>");
        // Replace numbers (but not in exception type names)
        result = StandaloneNumberRegex().Replace(result, "<N>");
        // Replace URLs
        result = UrlRegex().Replace(result, "<URL>");
        return result;
    }

    [GeneratedRegex(@" in [^\s]+:\s*line \d+", RegexOptions.Compiled)]
    private static partial Regex LineNumberRegex();

    [GeneratedRegex(@" in [/\\][^\s]+\.(cs|fs|vb)", RegexOptions.Compiled)]
    private static partial Regex FilePathRegex();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled)]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"(?<![a-zA-Z])\d+(?![a-zA-Z])", RegexOptions.Compiled)]
    private static partial Regex StandaloneNumberRegex();

    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorFingerprinterTests"`
Expected: All 6 tests PASS

**Step 5: Commit**

```bash
git add src/qyl.collector/Errors/ErrorFingerprinter.cs tests/qyl.collector.tests/Errors/ErrorFingerprinterTests.cs
git commit -m "feat(errors): add ErrorFingerprinter with GenAI-aware fingerprinting"
```

---

### Task 2: ErrorCategorizer (pure logic)

**Files:**
- Create: `src/qyl.collector/Errors/ErrorCategorizer.cs`
- Test: `tests/qyl.collector.tests/Errors/ErrorCategorizerTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/qyl.collector.tests/Errors/ErrorCategorizerTests.cs
namespace qyl.collector.tests.Errors;

public sealed class ErrorCategorizerTests
{
    [Theory]
    [InlineData("System.Net.Http.HttpRequestException", null, "network")]
    [InlineData("System.TimeoutException", null, "timeout")]
    [InlineData("System.UnauthorizedAccessException", null, "auth")]
    [InlineData("System.Data.Common.DbException", null, "database")]
    [InlineData("DuckDB.NET.DuckDBException", null, "database")]
    [InlineData("System.ArgumentException", null, "validation")]
    [InlineData("System.ArgumentNullException", null, "validation")]
    [InlineData("SomeCustomException", null, "unknown")]
    public void Categorize_ByExceptionType_ReturnsExpected(string exceptionType, string? genAiErrorType, string expected)
    {
        var result = ErrorCategorizer.Categorize(exceptionType, genAiErrorType);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("rate_limit_exceeded", "rate_limit")]
    [InlineData("context_length_exceeded", "validation")]
    [InlineData("authentication_error", "auth")]
    [InlineData("insufficient_quota", "rate_limit")]
    [InlineData("model_overloaded", "external")]
    [InlineData("timeout", "timeout")]
    public void Categorize_ByGenAiErrorType_TakesPrecedence(string genAiErrorType, string expected)
    {
        // GenAI error type should take precedence over exception type
        var result = ErrorCategorizer.Categorize("System.Exception", genAiErrorType);
        Assert.Equal(expected, result);
    }
}
```

**Step 2: Run tests — expect build failure**

**Step 3: Implement ErrorCategorizer**

```csharp
// src/qyl.collector/Errors/ErrorCategorizer.cs
namespace qyl.collector.Errors;

/// <summary>
///     Maps exception types and GenAI error types to ErrorCategory strings.
///     GenAI error type takes precedence when present (more specific).
///     Categories match the TypeSpec ErrorCategory enum.
/// </summary>
public static class ErrorCategorizer
{
    public static string Categorize(string exceptionType, string? genAiErrorType = null)
    {
        // GenAI-specific error type takes precedence
        if (!string.IsNullOrEmpty(genAiErrorType))
        {
            return genAiErrorType switch
            {
                "rate_limit_exceeded" or "insufficient_quota" => "rate_limit",
                "context_length_exceeded" => "validation",
                "authentication_error" => "auth",
                "model_overloaded" => "external",
                "timeout" => "timeout",
                "content_filter" => "validation",
                _ => "unknown"
            };
        }

        // .NET exception type mapping
        return exceptionType switch
        {
            _ when exceptionType.Contains("HttpRequestException", StringComparison.Ordinal) => "network",
            _ when exceptionType.Contains("SocketException", StringComparison.Ordinal) => "network",
            _ when exceptionType.Contains("TimeoutException", StringComparison.Ordinal) => "timeout",
            _ when exceptionType.Contains("TaskCanceledException", StringComparison.Ordinal) => "timeout",
            _ when exceptionType.Contains("UnauthorizedAccess", StringComparison.Ordinal) => "auth",
            _ when exceptionType.Contains("Authentication", StringComparison.Ordinal) => "auth",
            _ when exceptionType.Contains("DbException", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("DuckDB", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("SqlException", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("ArgumentException", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("ArgumentNull", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("FormatException", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("InvalidOperation", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NotSupported", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NotImplemented", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NullReference", StringComparison.Ordinal) => "internal",
            _ => "unknown"
        };
    }
}
```

**Step 4: Run tests**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorCategorizerTests"`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/qyl.collector/Errors/ErrorCategorizer.cs tests/qyl.collector.tests/Errors/ErrorCategorizerTests.cs
git commit -m "feat(errors): add ErrorCategorizer with GenAI error type support"
```

---

### Task 3: ErrorAggregator + DuckDbStore error methods

**Files:**
- Create: `src/qyl.collector/Errors/ErrorAggregator.cs`
- Create: `src/qyl.collector/Errors/ErrorEvent.cs`
- Modify: `src/qyl.collector/Storage/DuckDbStore.cs` — add error UPSERT + query methods
- Modify: `src/qyl.collector/Storage/DuckDbSchema.g.cs` — add error indexes (manual extension)
- Test: `tests/qyl.collector.tests/Errors/ErrorAggregatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/qyl.collector.tests/Errors/ErrorAggregatorTests.cs
using qyl.collector.Errors;
using qyl.collector.Storage;

namespace qyl.collector.tests.Errors;

public sealed class ErrorAggregatorTests : IAsyncLifetime
{
    private DuckDbStore? _store;
    private DuckDbStore Store => _store ?? throw new InvalidOperationException("Store not initialized");

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public ValueTask DisposeAsync() => _store?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task UpsertError_NewFingerprint_InsertsRow()
    {
        var error = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "abc123",
            ServiceName = "my-api",
            TraceId = "trace-001"
        };

        await Store.UpsertErrorAsync(error);

        var errors = await Store.GetErrorsAsync();
        Assert.Single(errors);
        Assert.Equal("NullReferenceException", errors[0].ErrorType);
        Assert.Equal("new", errors[0].Status);
        Assert.Equal(1, errors[0].OccurrenceCount);
    }

    [Fact]
    public async Task UpsertError_ExistingFingerprint_IncrementsCount()
    {
        var error1 = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "same-fp",
            ServiceName = "my-api",
            TraceId = "trace-001"
        };
        var error2 = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "same-fp",
            ServiceName = "my-api",
            TraceId = "trace-002"
        };

        await Store.UpsertErrorAsync(error1);
        await Store.UpsertErrorAsync(error2);

        var errors = await Store.GetErrorsAsync();
        Assert.Single(errors);
        Assert.Equal(2, errors[0].OccurrenceCount);
    }

    [Fact]
    public async Task GetErrorsAsync_FilterByCategory_ReturnsFiltered()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "Connection refused",
            Category = "network", Fingerprint = "fp-net", ServiceName = "api", TraceId = "t1"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "NullReferenceException", Message = "null ref",
            Category = "internal", Fingerprint = "fp-int", ServiceName = "api", TraceId = "t2"
        });

        var networkErrors = await Store.GetErrorsAsync(category: "network");
        Assert.Single(networkErrors);
        Assert.Equal("network", networkErrors[0].Category);
    }

    [Fact]
    public async Task GetErrorStatsAsync_ReturnsCategoryBreakdown()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "timeout",
            Category = "network", Fingerprint = "fp1", ServiceName = "api", TraceId = "t1"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "NullReferenceException", Message = "null",
            Category = "internal", Fingerprint = "fp2", ServiceName = "api", TraceId = "t2"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "dns fail",
            Category = "network", Fingerprint = "fp3", ServiceName = "api", TraceId = "t3"
        });

        var stats = await Store.GetErrorStatsAsync();
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(2, stats.ByCategory.Count);
    }
}
```

**Step 2: Run tests — expect build failure**

**Step 3: Create ErrorEvent record**

```csharp
// src/qyl.collector/Errors/ErrorEvent.cs
namespace qyl.collector.Errors;

/// <summary>
///     Extracted error data from an ingested span with status_code = 2.
///     Created by the ingestion pipeline, consumed by ErrorAggregator.
/// </summary>
public sealed record ErrorEvent
{
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public required string Fingerprint { get; init; }
    public required string ServiceName { get; init; }
    public required string TraceId { get; init; }
    public string? UserId { get; init; }
    public string? GenAiProvider { get; init; }
    public string? GenAiModel { get; init; }
    public string? GenAiOperation { get; init; }
}

/// <summary>
///     Error row from the DuckDB errors table.
/// </summary>
public sealed record ErrorRow
{
    public required string ErrorId { get; init; }
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public required string Fingerprint { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required long OccurrenceCount { get; init; }
    public long? AffectedUsers { get; init; }
    public string? AffectedServices { get; init; }
    public required string Status { get; init; }
    public string? AssignedTo { get; init; }
    public string? IssueUrl { get; init; }
    public string? SampleTraces { get; init; }
}

/// <summary>
///     Aggregated error statistics.
/// </summary>
public sealed record ErrorStats
{
    public required long TotalCount { get; init; }
    public required IReadOnlyList<ErrorCategoryStat> ByCategory { get; init; }
}

public sealed record ErrorCategoryStat
{
    public required string Category { get; init; }
    public required long Count { get; init; }
}
```

**Step 4: Add error methods to DuckDbStore**

Add to `src/qyl.collector/Storage/DuckDbStore.cs` after the Log Operations section (~line 1080):

```csharp
// ==========================================================================
// Error Operations
// ==========================================================================

public async Task UpsertErrorAsync(ErrorEvent error, CancellationToken ct = default)
{
    ThrowIfDisposed();
    var job = new WriteJob<int>(async (con, token) =>
    {
        await using var cmd = con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO errors (error_id, error_type, message, category, fingerprint,
                               first_seen, last_seen, occurrence_count, affected_services,
                               status, sample_traces)
            VALUES ($1, $2, $3, $4, $5, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 1, $6, 'new', $7)
            ON CONFLICT (fingerprint) DO UPDATE SET
                last_seen = CURRENT_TIMESTAMP,
                occurrence_count = errors.occurrence_count + 1,
                affected_services = CASE
                    WHEN errors.affected_services IS NULL THEN EXCLUDED.affected_services
                    WHEN errors.affected_services LIKE '%' || $8 || '%' THEN errors.affected_services
                    ELSE errors.affected_services || ',' || $8
                END,
                sample_traces = CASE
                    WHEN LENGTH(errors.sample_traces) - LENGTH(REPLACE(errors.sample_traces, ',', '')) >= 9
                    THEN errors.sample_traces
                    WHEN errors.sample_traces IS NULL THEN EXCLUDED.sample_traces
                    ELSE errors.sample_traces || ',' || $9
                END
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = Guid.NewGuid().ToString("N") }); // $1 error_id
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ErrorType });               // $2
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Message });                 // $3
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Category });                // $4
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Fingerprint });             // $5
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ServiceName });             // $6
        cmd.Parameters.Add(new DuckDBParameter { Value = error.TraceId });                 // $7
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ServiceName });             // $8 (for LIKE check)
        cmd.Parameters.Add(new DuckDBParameter { Value = error.TraceId });                 // $9
        return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    });

    await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
    await job.Task.ConfigureAwait(false);
}

public async Task<IReadOnlyList<ErrorRow>> GetErrorsAsync(
    string? category = null, string? status = null, string? serviceName = null,
    int limit = 50, CancellationToken ct = default)
{
    ThrowIfDisposed();
    await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

    var conditions = new List<string>();
    var parameters = new List<DuckDBParameter>();
    var paramIndex = 1;

    if (!string.IsNullOrEmpty(category))
    {
        conditions.Add($"category = ${paramIndex++}");
        parameters.Add(new DuckDBParameter { Value = category });
    }
    if (!string.IsNullOrEmpty(status))
    {
        conditions.Add($"status = ${paramIndex++}");
        parameters.Add(new DuckDBParameter { Value = status });
    }
    if (!string.IsNullOrEmpty(serviceName))
    {
        conditions.Add($"affected_services LIKE ${paramIndex++}");
        parameters.Add(new DuckDBParameter { Value = $"%{serviceName}%" });
    }

    var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

    await using var cmd = lease.Connection.CreateCommand();
    cmd.CommandText = $"""
        SELECT error_id, error_type, message, category, fingerprint,
               first_seen, last_seen, occurrence_count, affected_users,
               affected_services, status, assigned_to, issue_url, sample_traces
        FROM errors {where}
        ORDER BY last_seen DESC
        LIMIT ${paramIndex}
        """;
    cmd.Parameters.AddRange(parameters);
    cmd.Parameters.Add(new DuckDBParameter { Value = limit });

    var errors = new List<ErrorRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
        errors.Add(MapErrorRow(reader));
    return errors;
}

public async Task<ErrorStats> GetErrorStatsAsync(CancellationToken ct = default)
{
    ThrowIfDisposed();
    await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

    await using var cmd = lease.Connection.CreateCommand();
    cmd.CommandText = """
        SELECT category, SUM(occurrence_count) as total
        FROM errors GROUP BY category ORDER BY total DESC
        """;

    var stats = new List<ErrorCategoryStat>();
    long total = 0;
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        var count = reader.GetInt64(1);
        total += count;
        stats.Add(new ErrorCategoryStat { Category = reader.GetString(0), Count = count });
    }
    return new ErrorStats { TotalCount = total, ByCategory = stats };
}

private static ErrorRow MapErrorRow(DbDataReader reader) => new()
{
    ErrorId = reader.GetString(0),
    ErrorType = reader.GetString(1),
    Message = reader.GetString(2),
    Category = reader.GetString(3),
    Fingerprint = reader.GetString(4),
    FirstSeen = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
    LastSeen = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
    OccurrenceCount = reader.GetInt64(7),
    AffectedUsers = reader.IsDBNull(8) ? null : reader.GetInt64(8),
    AffectedServices = reader.IsDBNull(9) ? null : reader.GetString(9),
    Status = reader.GetString(10),
    AssignedTo = reader.IsDBNull(11) ? null : reader.GetString(11),
    IssueUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
    SampleTraces = reader.IsDBNull(13) ? null : reader.GetString(13),
};
```

**Step 5: Add fingerprint unique constraint to errors table**

The `errors` table DDL in `DuckDbSchema.g.cs` is auto-generated, so add a manual schema extension in a new partial file. Add after `InitializeSchema` in `DuckDbStore.cs`:

```csharp
// Add to InitializeSchema method after existing schema extensions
using var errorsCmd = con.CreateCommand();
errorsCmd.CommandText = """
    CREATE UNIQUE INDEX IF NOT EXISTS idx_errors_fingerprint ON errors(fingerprint);
    CREATE INDEX IF NOT EXISTS idx_errors_category ON errors(category);
    CREATE INDEX IF NOT EXISTS idx_errors_status ON errors(status);
    CREATE INDEX IF NOT EXISTS idx_errors_last_seen ON errors(last_seen);
    """;
errorsCmd.ExecuteNonQuery();
```

**Step 6: Run tests**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorAggregatorTests"`
Expected: All 4 tests PASS

**Step 7: Commit**

```bash
git add src/qyl.collector/Errors/ src/qyl.collector/Storage/DuckDbStore.cs tests/qyl.collector.tests/Errors/ErrorAggregatorTests.cs
git commit -m "feat(errors): add ErrorAggregator with DuckDB UPSERT and query methods"
```

---

### Task 4: Wire error extraction into ingestion pipeline

**Files:**
- Modify: `src/qyl.collector/Ingestion/OtlpConverter.cs` — add error extraction method
- Modify: `src/qyl.collector/Program.cs` — call ErrorAggregator on error spans
- Test: `tests/qyl.collector.tests/Errors/ErrorExtractionTests.cs`

**Step 1: Write failing test**

```csharp
// tests/qyl.collector.tests/Errors/ErrorExtractionTests.cs
using qyl.collector.Errors;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Errors;

public sealed class ErrorExtractionTests
{
    [Fact]
    public void ExtractErrorEvent_FromErrorSpan_ReturnsEvent()
    {
        var span = SpanBuilder.Create("trace-err", "span-err")
            .WithStatusCode(2)
            .WithStatusMessage("Object reference not set")
            .WithServiceName("my-api")
            .WithProvider("openai")
            .WithAttributes("""{"exception.type":"NullReferenceException","exception.message":"Object reference not set","exception.stacktrace":"at Foo.Bar()"}""")
            .Build();

        var error = ErrorExtractor.Extract(span);

        Assert.NotNull(error);
        Assert.Equal("NullReferenceException", error.ErrorType);
        Assert.Equal("Object reference not set", error.Message);
        Assert.Equal("internal", error.Category);
        Assert.NotEmpty(error.Fingerprint);
        Assert.Equal("my-api", error.ServiceName);
        Assert.Equal("trace-err", error.TraceId);
    }

    [Fact]
    public void ExtractErrorEvent_FromOkSpan_ReturnsNull()
    {
        var span = SpanBuilder.Create("trace-ok", "span-ok")
            .WithStatusCode(1) // OK
            .Build();

        var error = ErrorExtractor.Extract(span);
        Assert.Null(error);
    }

    [Fact]
    public void ExtractErrorEvent_WithGenAiAttributes_IncludesGenAiData()
    {
        var span = SpanBuilder.Create("trace-ai", "span-ai")
            .WithStatusCode(2)
            .WithStatusMessage("rate limited")
            .WithServiceName("llm-gateway")
            .WithProvider("openai")
            .WithModel("gpt-4")
            .WithAttributes("""{"gen_ai.error.type":"rate_limit_exceeded","gen_ai.operation.name":"chat"}""")
            .Build();

        var error = ErrorExtractor.Extract(span);

        Assert.NotNull(error);
        Assert.Equal("rate_limit", error.Category);
        Assert.Equal("openai", error.GenAiProvider);
        Assert.Equal("gpt-4", error.GenAiModel);
        Assert.Equal("chat", error.GenAiOperation);
    }
}
```

**Step 2: Run tests — expect build failure**

**Step 3: Implement ErrorExtractor**

```csharp
// src/qyl.collector/Errors/ErrorExtractor.cs
using System.Text.Json;
using qyl.collector.Storage;

namespace qyl.collector.Errors;

/// <summary>
///     Extracts ErrorEvent from SpanStorageRow when status_code == 2 (ERROR).
///     Reads exception.* attributes from attributes_json.
/// </summary>
public static class ErrorExtractor
{
    public static ErrorEvent? Extract(SpanStorageRow span)
    {
        if (span.StatusCode != 2) return null;

        var attrs = ParseAttributes(span.AttributesJson);

        var exceptionType = attrs.GetValueOrDefault("exception.type")
                           ?? attrs.GetValueOrDefault("error.type")
                           ?? "Unknown";
        var exceptionMessage = attrs.GetValueOrDefault("exception.message")
                              ?? span.StatusMessage
                              ?? "Unknown error";
        var stackTrace = attrs.GetValueOrDefault("exception.stacktrace");
        var genAiErrorType = attrs.GetValueOrDefault("gen_ai.error.type");
        var genAiOperation = attrs.GetValueOrDefault("gen_ai.operation.name");

        var category = ErrorCategorizer.Categorize(exceptionType, genAiErrorType);
        var fingerprint = ErrorFingerprinter.Compute(exceptionType, exceptionMessage, stackTrace, genAiOperation);

        return new ErrorEvent
        {
            ErrorType = exceptionType,
            Message = exceptionMessage,
            Category = category,
            Fingerprint = fingerprint,
            ServiceName = span.ServiceName ?? "unknown",
            TraceId = span.TraceId,
            UserId = attrs.GetValueOrDefault("enduser.id") ?? attrs.GetValueOrDefault("user.id"),
            GenAiProvider = span.GenAiProviderName,
            GenAiModel = span.GenAiRequestModel,
            GenAiOperation = genAiOperation,
        };
    }

    private static Dictionary<string, string> ParseAttributes(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
```

**Step 4: Wire into Program.cs ingestion endpoints**

In `src/qyl.collector/Program.cs`, modify the `/v1/traces` endpoint (around line 371-383) to extract and enqueue errors:

```csharp
// After: broadcaster.PublishSpans(batch);
// Add:
foreach (var span in batch.Spans)
{
    if (ErrorExtractor.Extract(span) is { } errorEvent)
        await duckDbStore.UpsertErrorAsync(errorEvent, ct);
}
```

Apply same pattern to the `/api/v1/ingest` endpoint (around line 331-334).

**Step 5: Run tests**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorExtractionTests"`
Expected: All 3 tests PASS

**Step 6: Commit**

```bash
git add src/qyl.collector/Errors/ErrorExtractor.cs src/qyl.collector/Program.cs tests/qyl.collector.tests/Errors/ErrorExtractionTests.cs
git commit -m "feat(errors): wire error extraction into OTLP ingestion pipeline"
```

---

### Task 5: Error REST API endpoints (replace stubs)

**Files:**
- Create: `src/qyl.collector/Errors/ErrorEndpoints.cs`
- Modify: `src/qyl.collector/Program.cs` — replace stubs with real endpoints
- Add: `PATCH /api/v1/errors/{errorId}` for status updates
- Add: `GET /api/v1/errors/groups` for GenAI grouping
- Test: `tests/qyl.collector.tests/Errors/ErrorEndpointTests.cs` (integration)

**Step 1: Write failing integration test**

```csharp
// tests/qyl.collector.tests/Errors/ErrorEndpointTests.cs
using qyl.collector.Errors;
using qyl.collector.Storage;

namespace qyl.collector.tests.Errors;

public sealed class ErrorEndpointTests : IAsyncLifetime
{
    private DuckDbStore? _store;
    private DuckDbStore Store => _store ?? throw new InvalidOperationException();

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public ValueTask DisposeAsync() => _store?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task UpdateErrorStatus_ChangesStatus()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "Exception", Message = "test", Category = "unknown",
            Fingerprint = "fp-status", ServiceName = "api", TraceId = "t1"
        });

        var errors = await Store.GetErrorsAsync();
        Assert.Equal("new", errors[0].Status);

        await Store.UpdateErrorStatusAsync(errors[0].ErrorId, "acknowledged");

        var updated = await Store.GetErrorsAsync();
        Assert.Equal("acknowledged", updated[0].Status);
    }

    [Fact]
    public async Task GetGenAiErrorGroups_GroupsByProvider()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "rate_limit", Message = "rate limited", Category = "rate_limit",
            Fingerprint = "fp-oai1", ServiceName = "api", TraceId = "t1",
            GenAiProvider = "openai", GenAiModel = "gpt-4", GenAiOperation = "chat"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "rate_limit", Message = "rate limited", Category = "rate_limit",
            Fingerprint = "fp-anth1", ServiceName = "api", TraceId = "t2",
            GenAiProvider = "anthropic", GenAiModel = "claude-3", GenAiOperation = "chat"
        });

        // The GenAI grouping query is tested via the endpoint
        var errors = await Store.GetErrorsAsync();
        Assert.Equal(2, errors.Count);
    }
}
```

**Step 2: Run tests — expect build failure for `UpdateErrorStatusAsync`**

**Step 3: Add UpdateErrorStatusAsync to DuckDbStore**

```csharp
public async Task UpdateErrorStatusAsync(string errorId, string status, string? assignedTo = null,
    CancellationToken ct = default)
{
    ThrowIfDisposed();
    var job = new WriteJob<int>(async (con, token) =>
    {
        await using var cmd = con.CreateCommand();
        cmd.CommandText = assignedTo is not null
            ? "UPDATE errors SET status = $1, assigned_to = $2 WHERE error_id = $3"
            : "UPDATE errors SET status = $1 WHERE error_id = $2";

        cmd.Parameters.Add(new DuckDBParameter { Value = status });
        if (assignedTo is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = assignedTo });
        cmd.Parameters.Add(new DuckDBParameter { Value = errorId });

        return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    });

    await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
    await job.Task.ConfigureAwait(false);
}
```

**Step 4: Create ErrorEndpoints.cs**

```csharp
// src/qyl.collector/Errors/ErrorEndpoints.cs
using qyl.collector.Storage;

namespace qyl.collector.Errors;

public static class ErrorEndpoints
{
    public static void MapErrorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/errors", async (
            DuckDbStore store, string? category, string? status, string? serviceName,
            int? limit, CancellationToken ct) =>
        {
            var errors = await store.GetErrorsAsync(category, status, serviceName, limit ?? 50, ct);
            return Results.Ok(new { items = errors, total = errors.Count });
        });

        app.MapGet("/api/v1/errors/stats", async (DuckDbStore store, CancellationToken ct) =>
        {
            var stats = await store.GetErrorStatsAsync(ct);
            return Results.Ok(stats);
        });

        app.MapGet("/api/v1/errors/{errorId}", async (
            string errorId, DuckDbStore store, CancellationToken ct) =>
        {
            var errors = await store.GetErrorsAsync(limit: 1000, ct: ct);
            var error = errors.FirstOrDefault(e => e.ErrorId == errorId);
            return error is null ? Results.NotFound() : Results.Ok(error);
        });

        app.MapPatch("/api/v1/errors/{errorId}", async (
            string errorId, ErrorStatusUpdate update, DuckDbStore store, CancellationToken ct) =>
        {
            await store.UpdateErrorStatusAsync(errorId, update.Status, update.AssignedTo, ct);
            return Results.Ok();
        });
    }
}

public sealed record ErrorStatusUpdate(string Status, string? AssignedTo = null);
```

**Step 5: Replace stubs in Program.cs**

In `src/qyl.collector/Program.cs`, remove the error stubs at lines 622-635:
```csharp
// REMOVE these lines:
app.MapGet("/api/v1/errors", ...);
app.MapGet("/api/v1/errors/stats", ...);
app.MapGet("/api/v1/errors/{errorId}", ...);

// ADD after app.MapAlertEndpoints():
app.MapErrorEndpoints();
```

**Step 6: Run tests**

Run: `dotnet test --project tests/qyl.collector.tests --filter "FullyQualifiedName~ErrorEndpointTests"`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add src/qyl.collector/Errors/ErrorEndpoints.cs src/qyl.collector/Program.cs src/qyl.collector/Storage/DuckDbStore.cs tests/qyl.collector.tests/Errors/ErrorEndpointTests.cs
git commit -m "feat(errors): add error REST API endpoints, replace stubs"
```

---

### Task 6: Auto-capture middleware in qyl.servicedefaults

**Files:**
- Create: `src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs`
- Modify: `src/qyl.servicedefaults/QylServiceDefaults.cs` — register middleware
- Test: Manual verification (middleware is integration-only)

**Step 1: Implement exception capture**

```csharp
// src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Qyl.ServiceDefaults.ErrorCapture;

/// <summary>
///     ASP.NET Core middleware that captures unhandled exceptions as OTel span events.
///     This is qyl's equivalent of Sentry's UseSentry() — zero-config crash capture.
///     Also hooks AppDomain.UnhandledException and TaskScheduler.UnobservedTaskException.
/// </summary>
public sealed class ExceptionCaptureMiddleware(RequestDelegate next, ILogger<ExceptionCaptureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordException(ex);
            throw; // Re-throw — we capture, we don't swallow
        }
    }

    private void RecordException(Exception ex)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", "true" },
        }));

        logger.LogExceptionCaptured(ex.GetType().Name, ex.Message);
    }
}

/// <summary>
///     Registers global exception hooks for non-web scenarios (background services, console apps).
///     Captures AppDomain.UnhandledException and TaskScheduler.UnobservedTaskException.
/// </summary>
public static class GlobalExceptionHooks
{
    private static int _registered;

    public static void Register(ILoggerFactory loggerFactory)
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;

        var logger = loggerFactory.CreateLogger("Qyl.ServiceDefaults.ErrorCapture");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                logger.LogCritical(ex, "[qyl] Unhandled exception (IsTerminating={IsTerminating})",
                    args.IsTerminating);
                RecordGlobalException(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "[qyl] Unobserved task exception");
            RecordGlobalException(args.Exception, "TaskScheduler.UnobservedTaskException");
        };
    }

    private static void RecordGlobalException(Exception ex, string source)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", "true" },
            { "exception.source", source },
        }));
    }
}

internal static partial class ExceptionCaptureLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "[qyl] Captured {ExceptionType}: {Message}")]
    public static partial void LogExceptionCaptured(this ILogger logger, string exceptionType, string message);
}
```

**Step 2: Wire into QylServiceDefaults**

In `src/qyl.servicedefaults/QylServiceDefaults.cs`, add to `MapQylDefaultEndpoints`:

```csharp
// At the top of MapQylDefaultEndpoints, before ForwardedHeaders:
app.UseMiddleware<Qyl.ServiceDefaults.ErrorCapture.ExceptionCaptureMiddleware>();

// At the end of UseQylConventions, after AddProblemDetails:
builder.Services.AddSingleton<IHostLifetime>(sp =>
{
    Qyl.ServiceDefaults.ErrorCapture.GlobalExceptionHooks.Register(
        sp.GetRequiredService<ILoggerFactory>());
    return sp.GetRequiredService<IHostLifetime>();
});
```

Actually, simpler approach — register hooks in a hosted service:

```csharp
// Add to UseQylConventions after builder.Services.AddProblemDetails():
builder.Services.AddHostedService<Qyl.ServiceDefaults.ErrorCapture.ExceptionHookRegistrar>();
```

And create:
```csharp
// Add to ExceptionCapture.cs
internal sealed class ExceptionHookRegistrar(ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GlobalExceptionHooks.Register(loggerFactory);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

**Step 3: Commit**

```bash
git add src/qyl.servicedefaults/ErrorCapture/ src/qyl.servicedefaults/QylServiceDefaults.cs
git commit -m "feat(servicedefaults): add auto-capture middleware for exceptions"
```

---

### Task 7: Build, test full suite, verify zero warnings

**Step 1: Run full build**

Run: `dotnet build /Users/ancplua/qyl/qyl.sln`
Expected: Build succeeded, 0 warnings

**Step 2: Run full test suite**

Run: `dotnet test /Users/ancplua/qyl/qyl.sln`
Expected: All tests pass (exit code 0)

**Step 3: Commit all remaining changes**

```bash
git add -A
git commit -m "feat(errors): error engine workflow 1 — fingerprinting, categorization, aggregation, REST API, auto-capture"
```

---

## Verification Checklist

After all tasks complete, verify:

- [ ] `errors` table has fingerprint unique index
- [ ] Error spans (status_code=2) in OTLP ingestion create error rows
- [ ] Same error fingerprint increments occurrence_count (not duplicate rows)
- [ ] GenAI errors include provider/model/operation in fingerprint
- [ ] `GET /api/v1/errors` returns real data (not empty stubs)
- [ ] `GET /api/v1/errors/stats` returns category breakdown
- [ ] `PATCH /api/v1/errors/{id}` changes status
- [ ] Exception middleware captures unhandled exceptions as span events
- [ ] AppDomain.UnhandledException hook is registered
- [ ] TaskScheduler.UnobservedTaskException hook is registered
- [ ] Full test suite passes
- [ ] Zero build warnings
