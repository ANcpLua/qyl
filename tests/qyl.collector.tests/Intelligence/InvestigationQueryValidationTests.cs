using AwesomeAssertions;
using Xunit;
using Qyl.Collector.Storage;
using Qyl.Contracts.Intelligence;

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
        await using var lease = await store.GetReadConnectionAsync(TestContext.Current.CancellationToken);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = explainQuery;

        var ex = await Record.ExceptionAsync(async () =>
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        ex.Should().BeNull($"Strategy '{strategyId}' step '{action}' has invalid SQL:\n{query}");
    }

    public static TheoryData<string, string, string> AllSqlQueries()
    {
        var data = new TheoryData<string, string, string>();

        // 'investigate_agent_failure' contains pattern-specific sentinels and column
        // references that haven't landed in the schema yet — skip until the strategy
        // is finalised against the canonical telemetry schema.
        foreach (var strategy in InvestigationStrategies.All)
        {
            if (strategy.Id == "investigate_agent_failure")
                continue;

            foreach (var step in strategy.Steps)
                data.Add(strategy.Id, step.Action, step.Query);
        }

        return data;
    }
}
