using DuckDB.NET.Native;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class DuckDbFailureClassificationTests
{
    [Fact]
    public void Every_duckdb_155_error_type_has_an_explicit_classification()
    {
        foreach (var errorType in Enum.GetValues<DuckDBErrorType>())
        {
            _ = DuckDbFailures.Classify(errorType);
        }
    }

    [Theory]
    [InlineData(DuckDBErrorType.Connection, "RetryableTransient")]
    [InlineData(DuckDBErrorType.Transaction, "Concurrency")]
    [InlineData(DuckDBErrorType.Interrupt, "Cancellation")]
    [InlineData(DuckDBErrorType.Fatal, "Corruption")]
    [InlineData(DuckDBErrorType.Constraint, "InvalidData")]
    [InlineData(DuckDBErrorType.Catalog, "SchemaIncompatibility")]
    [InlineData(DuckDBErrorType.OutOfMemory, "ResourceExhaustion")]
    [InlineData(DuckDBErrorType.Executor, "ProgrammerError")]
    public void Representative_native_failures_map_without_message_parsing(
        DuckDBErrorType errorType,
        string expected) =>
        Assert.Equal(expected, DuckDbFailures.Classify(errorType).ToString());

    [Fact]
    public void Deterministic_constraint_and_schema_failures_never_retry()
    {
        Assert.Equal(
            DuckDbFailureKind.InvalidData,
            DuckDbFailures.Classify(DuckDBErrorType.Constraint));
        Assert.Equal(
            DuckDbFailureKind.SchemaIncompatibility,
            DuckDbFailures.Classify(DuckDBErrorType.Catalog));
    }
}
