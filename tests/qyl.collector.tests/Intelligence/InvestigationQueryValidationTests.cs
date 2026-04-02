using Qyl.Collector.Storage;
using Qyl.Contracts.Intelligence;
using Xunit;

namespace Qyl.Collector.Tests.Intelligence;

/// <summary>
///     Validates that all DuckDB queries in investigation strategies are
///     syntactically valid against the telemetry data model schema.
///     Table names, column names, and types must match.
/// </summary>
public sealed class InvestigationQueryValidationTests
{
    [Theory]
    [MemberData(nameof(AllSqlQueries))]
    public async Task Query_IsValidDuckDbSql(string strategyId, string action, string query)
    {
        if (query is "pattern_specific_recommendation")
            return; // Not SQL — sentinel for non-query steps

        // Replace parameter placeholders with literals so DuckDB can parse
        var testQuery = query
            .Replace("?", "'test'")
            .Replace("IN ('test')", "IN ('test1','test2')");

        // Wrap in EXPLAIN to validate syntax without execution
        var explainQuery = $"EXPLAIN {testQuery}";

        await using var store = new DuckDbStore(":memory:");
        using var lease = await store.GetReadConnectionAsync(TestContext.Current.CancellationToken);
        using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = explainQuery;

        var ex = await Record.ExceptionAsync(async () =>
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.True(ex is null,
            $"Strategy '{strategyId}' step '{action}' has invalid SQL:\n{query}\nError: {ex?.Message}");
    }

    public static TheoryData<string, string, string> AllSqlQueries()
    {
        var data = new TheoryData<string, string, string>();

        foreach (var strategy in InvestigationStrategies.All)
        foreach (var step in strategy.Steps)
            data.Add(strategy.Id, step.Action, step.Query);

        return data;
    }
}
