using DuckDB.NET.Data;
using qyl.collector.Storage;

namespace qyl.collector.tests.Helpers;

/// <summary>
///     Helper methods for DuckDB test operations.
/// </summary>
internal static class DuckDbTestHelpers
{
    /// <summary>Creates a new in-memory DuckDbStore with default settings.</summary>
    public static DuckDbStore CreateInMemoryStore(
        int jobQueueCapacity = TestConstants.DefaultJobQueueCapacity,
        int maxConcurrentReads = TestConstants.DefaultMaxConcurrentReads,
        int maxRetainedReadConnections = TestConstants.DefaultMaxRetainedReadConnections)
    {
        return new DuckDbStore(
            TestConstants.InMemoryDb,
            jobQueueCapacity,
            maxConcurrentReads,
            maxRetainedReadConnections);
    }

    /// <summary>Waits for schema initialization.</summary>
    public static Task WaitForSchemaInit()
    {
        return Task.Delay(TestConstants.SchemaInitDelayMs);
    }

    /// <summary>Waits for batch processing.</summary>
    public static Task WaitForBatch()
    {
        return Task.Delay(TestConstants.BatchProcessingDelayMs);
    }

    /// <summary>Waits for archive processing.</summary>
    public static Task WaitForArchive()
    {
        return Task.Delay(TestConstants.ArchiveProcessingDelayMs);
    }

    /// <summary>Writes a batch synchronously (waits for completion).</summary>
    public static Task EnqueueAndWaitAsync(DuckDbStore store, SpanBatch batch,
        int delayMs = 0) // delayMs kept for API compatibility but no longer needed
    {
        return store.WriteBatchAsync(batch);
    }

    /// <summary>Writes a single span synchronously (waits for completion).</summary>
    public static Task EnqueueAndWaitAsync(DuckDbStore store, SpanStorageRow span,
        int delayMs = 0) // delayMs kept for API compatibility but no longer needed
    {
        return store.WriteBatchAsync(new SpanBatch([span]));
    }

    /// <summary>Creates a temporary directory for parquet tests.</summary>
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duckdb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Safely deletes a temporary directory.</summary>
    public static void CleanupTempDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            /* Best effort cleanup */
        }
    }

    /// <summary>Gets parquet files from a directory.</summary>
    public static string[] GetParquetFiles(string directory)
    {
        return Directory.GetFiles(directory, "*.parquet");
    }
}

/// <summary>
///     Extension methods for DuckDB test queries.
/// </summary>
internal static class DuckDbQueryExtensions
{
    /// <summary>Gets the table columns as (Name, Type) tuples.</summary>
    public static async Task<List<(string Name, string Type)>> GetTableColumnsAsync(
        this DuckDBConnection connection,
        string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = $1
            ORDER BY ordinal_position";
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = tableName
        });

        var columns = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add((reader.GetString(0), reader.GetString(1)));
        return columns;
    }

    /// <summary>Checks if a table exists.</summary>
    public static async Task<bool> TableExistsAsync(
        this DuckDBConnection connection,
        string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_name = $1";
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = tableName
        });

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        return count == 1;
    }

    /// <summary>Gets the index count for a table.</summary>
    public static async Task<long> GetIndexCountAsync(
        this DuckDBConnection connection,
        string tableName)
    {
        await using var cmd = connection.CreateCommand();
        // DuckDB uses duckdb_indexes() function instead of information_schema.statistics
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM duckdb_indexes()
            WHERE table_name = $1";
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = tableName
        });

        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    /// <summary>Counts spans with specific trace and span ID.</summary>
    public static async Task<long> CountSpansAsync(
        this DuckDBConnection connection,
        string traceId,
        string spanId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM spans
            WHERE trace_id = $1 AND span_id = $2";
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = traceId
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = spanId
        });

        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}

/// <summary>
///     Disposable wrapper for temporary directories in tests.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = DuckDbTestHelpers.CreateTempDirectory();

    public void Dispose()
    {
        DuckDbTestHelpers.CleanupTempDirectory(Path);
    }

    public string[] GetParquetFiles()
    {
        return DuckDbTestHelpers.GetParquetFiles(Path);
    }
}