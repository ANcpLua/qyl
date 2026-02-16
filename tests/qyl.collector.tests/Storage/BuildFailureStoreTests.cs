using DuckDB.NET.Data;
using qyl.collector.Storage;

namespace qyl.collector.tests.Storage;

public sealed class BuildFailureStoreTests : IAsyncLifetime
{
    private string? _dbPath;
    private DuckDbBuildFailureStore? _store;

    public async ValueTask InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"qyl-build-failures-{Guid.NewGuid():N}.duckdb");

        await using var con = new DuckDBConnection($"DataSource={_dbPath}");
        await con.OpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS build_failures (
                              id VARCHAR PRIMARY KEY,
                              timestamp TIMESTAMP NOT NULL,
                              target VARCHAR NOT NULL,
                              exit_code INTEGER NOT NULL,
                              binlog_path VARCHAR,
                              error_summary TEXT,
                              property_issues_json JSON,
                              env_reads_json JSON,
                              call_stack_json JSON,
                              duration_ms INTEGER,
                              created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                          );
                          """;
        await cmd.ExecuteNonQueryAsync();

        _store = new DuckDbBuildFailureStore(_dbPath, 2);
    }

    public ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
            File.Delete(_dbPath);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task InsertAndGet_RoundTripsRecord()
    {
        var store = _store ?? throw new InvalidOperationException("Store not initialized");

        var id = await store.InsertAsync(new BuildFailureRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = TimeProvider.System.GetUtcNow(),
            Target = "build",
            ExitCode = 1,
            ErrorSummary = "CS0246",
            PropertyIssuesJson = "[\"Property X missing\"]"
        });

        var row = await store.GetAsync(id);

        Assert.NotNull(row);
        Assert.Equal("build", row.Target);
        Assert.Equal(1, row.ExitCode);
        Assert.Contains("CS0246", row.ErrorSummary);
    }

    [Fact]
    public async Task Insert_EnforcesRetention()
    {
        var store = _store ?? throw new InvalidOperationException("Store not initialized");

        for (var i = 0; i < 4; i++)
            await store.InsertAsync(new BuildFailureRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = TimeProvider.System.GetUtcNow().AddMinutes(i),
                Target = "build",
                ExitCode = 1,
                ErrorSummary = $"error-{i}"
            });

        var rows = await store.ListAsync(10);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, static r => r.ErrorSummary == "error-3");
        Assert.Contains(rows, static r => r.ErrorSummary == "error-2");
    }
}