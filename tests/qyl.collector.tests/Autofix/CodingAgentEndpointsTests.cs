using AwesomeAssertions;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;
using Qyl.Contracts.Loom;

namespace Qyl.Collector.Tests.Autofix;

/// <summary>
///     Storage-layer tests for the coding agent run operations backing
///     POST/GET/PUT /api/v1/fix-runs/{fixRunId}/coding-agents.
/// </summary>
public sealed class CodingAgentEndpointsTests : IAsyncDisposable
{
    private readonly DuckDbStore _store = new(":memory:");

    public ValueTask DisposeAsync() => _store.DisposeAsync();

    private Task SeedFixRunAsync(string fixRunId) =>
        _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO fix_runs (run_id, issue_id, status, policy)
                              VALUES ($1, 'issue-abc', 'running', 'auto')
                              ON CONFLICT (run_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = fixRunId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

    private static CodingAgentRunRecord Record(string id, string fixRunId, string provider = "Loom") =>
        new()
        {
            Id = id,
            FixRunId = fixRunId,
            Provider = CodingAgentProviderNames.NormalizeSlug(provider),
            Status = "pending",
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };

    [Fact]
    public async Task Insert_ThenGetById_ReturnsPersistedRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedFixRunAsync("fix-001");

        await _store.InsertCodingAgentRunAsync(Record("agent-001", "fix-001", "cursor"), ct);
        var result = await _store.GetCodingAgentRunAsync("agent-001", ct);

        result.Should().NotBeNull();
        result!.FixRunId.Should().Be("fix-001");
        result.Provider.Should().Be("cursor");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task GetById_MissingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        (await _store.GetCodingAgentRunAsync("does-not-exist", ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetRunsForFixRun_ReturnsOnlyMatchingFixRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedFixRunAsync("fix-a");
        await SeedFixRunAsync("fix-b");
        await _store.InsertCodingAgentRunAsync(Record("agent-a", "fix-a"), ct);
        await _store.InsertCodingAgentRunAsync(Record("agent-b", "fix-b"), ct);

        var runs = await _store.GetCodingAgentRunsForFixRunAsync("fix-a", 50, ct);

        runs.Should().ContainSingle();
        runs[0].FixRunId.Should().Be("fix-a");
    }

    [Fact]
    public async Task GetRunsForFixRun_LimitIsRespected()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedFixRunAsync("fix-limit");
        for (var i = 0; i < 5; i++)
            await _store.InsertCodingAgentRunAsync(Record($"agent-l{i}", "fix-limit"), ct);

        var runs = await _store.GetCodingAgentRunsForFixRunAsync("fix-limit", 2, ct);

        runs.Count.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStatus_ChangesStatusAndSetsUrls()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedFixRunAsync("fix-upd");
        await _store.InsertCodingAgentRunAsync(Record("agent-upd", "fix-upd", "cursor"), ct);

        await _store.UpdateCodingAgentRunStatusAsync(
            "agent-upd", "completed",
            prUrl: "https://github.com/acme/repo/pull/42",
            agentUrl: "https://cursor.sh/agents/xyz",
            ct: ct);

        var updated = await _store.GetCodingAgentRunAsync("agent-upd", ct);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be("completed");
        updated.PrUrl.Should().Be("https://github.com/acme/repo/pull/42");
        updated.AgentUrl.Should().Be("https://cursor.sh/agents/xyz");
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStatus_Running_DoesNotSetCompletedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedFixRunAsync("fix-pend");
        await _store.InsertCodingAgentRunAsync(Record("agent-pend", "fix-pend"), ct);

        await _store.UpdateCodingAgentRunStatusAsync("agent-pend", "running", ct: ct);

        var updated = await _store.GetCodingAgentRunAsync("agent-pend", ct);

        updated.Should().NotBeNull();
        updated!.CompletedAt.Should().BeNull();
    }
}
