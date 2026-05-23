using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Integration.Tests.Storage;

/// <summary>
/// Real-DuckDB integration coverage for the <c>span_clusters</c> adapter on
/// <see cref="DuckDbStore"/> — both the read selector
/// <see cref="DuckDbStore.GetUnclusteredChatSpansAsync"/> and the upsert writer
/// <see cref="DuckDbStore.UpsertSpanClustersAsync"/>. The read path leans on
/// DuckDB's <c>json_extract_string</c> function applied to dotted telemetry
/// keys in a plain <c>VARCHAR</c> column plus a <c>LEFT JOIN … IS NULL</c>
/// anti-pattern — both rely on engine semantics that cannot be checked with mocks. The write
/// path is an <c>ON CONFLICT (span_id) DO UPDATE</c> that has to overwrite every
/// non-PK column, including <c>computed_at</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SpanClustersIntegrationTests : IAsyncDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"qyl-spanclusters-{Guid.NewGuid():N}.duckdb");

    private readonly DuckDbStore _store;

    public SpanClustersIntegrationTests() => _store = new DuckDbStore(_dbPath);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_returns_span_with_required_gen_ai_attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        var inputMessages = """[{"role":"user","content":"hello"}]""";
        await SeedChatSpanAsync(
            "span-ok",
            startNs: 1_000,
            attributesJson: $$"""{"gen_ai.operation.name":"chat","gen_ai.input.messages":{{System.Text.Json.JsonSerializer.Serialize(inputMessages)}}}""",
            ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Should().ContainSingle("only one seeded span satisfies the gen_ai predicates");
        rows[0].SpanId.Should().Be("span-ok");
        rows[0].InputMessages.Should().Be(inputMessages,
            "the third projected column extracts the dotted `gen_ai.input.messages` attribute as a JSON string value");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_excludes_span_missing_gen_ai_operation_name()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChatSpanAsync(
            "span-no-op",
            startNs: 1_000,
            attributesJson: """{"gen_ai.input.messages":"[{\"role\":\"user\"}]","http.method":"POST"}""",
            ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Should().BeEmpty(
            "the `gen_ai.operation.name IS NOT NULL` predicate must reject HTTP / non-chat spans");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_excludes_span_missing_input_messages()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChatSpanAsync(
            "span-no-input",
            startNs: 1_000,
            attributesJson: """{"gen_ai.operation.name":"chat"}""",
            ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Should().BeEmpty(
            "without `gen_ai.input.messages` the clustering worker has nothing to embed; the predicate must filter early");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_excludes_span_with_null_attributes_json()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChatSpanAsync("span-null-attrs", startNs: 1_000, attributesJson: null, ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Should().BeEmpty(
            "`json_extract_string(NULL, path)` evaluates to NULL, which the `IS NOT NULL` predicate rejects");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_excludes_span_already_present_in_span_clusters()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChatSpanAsync(
            "span-already-clustered",
            startNs: 1_000,
            attributesJson: """{"gen_ai.operation.name":"chat","gen_ai.input.messages":"[{\"role\":\"user\",\"content\":\"hi\"}]"}""",
            ct);

        await _store.UpsertSpanClustersAsync(
            [new SpanClusterRow(
                SpanId: "span-already-clustered",
                ClusterId: 1,
                ClusterLabel: "greetings",
                Distance: 0.1,
                ModelVersion: "cosine-v1",
                ComputedAt: new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc))],
            ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Should().BeEmpty(
            "the LEFT JOIN / IS NULL anti-join must hide spans that already have a cluster assignment");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_returns_spans_in_descending_start_time_order()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChatSpanAsync("span-oldest", startNs: 1_000, attributesJson: ChatAttributesFor("oldest"), ct);
        await SeedChatSpanAsync("span-middle", startNs: 2_000, attributesJson: ChatAttributesFor("middle"), ct);
        await SeedChatSpanAsync("span-newest", startNs: 3_000, attributesJson: ChatAttributesFor("newest"), ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 10, ct);

        rows.Select(static r => r.SpanId).Should().Equal(
            ["span-newest", "span-middle", "span-oldest"],
            "ORDER BY start_time_unix_nano DESC means the most recent chat span is first; the clustering worker drains backwards from now");
    }

    [Fact]
    public async Task GetUnclusteredChatSpans_respects_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 5; i++)
            await SeedChatSpanAsync($"span-{i}", startNs: 1_000 + i, attributesJson: ChatAttributesFor($"msg-{i}"), ct);

        var rows = await _store.GetUnclusteredChatSpansAsync(limit: 2, ct);

        rows.Should().HaveCount(2,
            "the LIMIT $1 parameter must bound the worker batch — otherwise an embedding burst could OOM");
    }

    [Fact]
    public async Task UpsertSpanClusters_no_op_for_empty_batch()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertSpanClustersAsync([], ct);

        var count = await CountSpanClustersAsync(ct);
        count.Should().Be(0,
            "the `rows.Count is 0` short-circuit must not write or even acquire the writer queue");
    }

    [Fact]
    public async Task UpsertSpanClusters_inserts_all_columns_verbatim_on_first_call()
    {
        var ct = TestContext.Current.CancellationToken;
        var computedAt = new DateTime(2026, 5, 19, 12, 34, 56, DateTimeKind.Utc);

        await _store.UpsertSpanClustersAsync(
            [new SpanClusterRow(
                SpanId: "span-fresh",
                ClusterId: 7,
                ClusterLabel: "billing-questions",
                Distance: 0.4242,
                ModelVersion: "cosine-v1",
                ComputedAt: computedAt)],
            ct);

        var row = await ReadSpanClusterAsync("span-fresh", ct);
        row.Should().NotBeNull("the row should be present after a first upsert");
        row.ClusterId.Should().Be(7);
        row.ClusterLabel.Should().Be("billing-questions");
        row.Distance.Should().Be(0.4242);
        row.ModelVersion.Should().Be("cosine-v1");
        row.ComputedAt.Should().Be(computedAt,
            "`computed_at` is parameter-bound, not defaulted to CURRENT_TIMESTAMP — the worker stamps it explicitly");
    }

    [Fact]
    public async Task UpsertSpanClusters_overwrites_every_non_pk_column_on_conflict()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertSpanClustersAsync(
            [new SpanClusterRow(
                SpanId: "span-conflict",
                ClusterId: 1,
                ClusterLabel: "first-label",
                Distance: 0.99,
                ModelVersion: "cosine-v0",
                ComputedAt: new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc))],
            ct);

        var secondComputedAt = new DateTime(2026, 5, 19, 11, 30, 0, DateTimeKind.Utc);
        await _store.UpsertSpanClustersAsync(
            [new SpanClusterRow(
                SpanId: "span-conflict",
                ClusterId: 9,
                ClusterLabel: "second-label",
                Distance: 0.01,
                ModelVersion: "cosine-v1",
                ComputedAt: secondComputedAt)],
            ct);

        var row = await ReadSpanClusterAsync("span-conflict", ct);
        row.Should().NotBeNull();
        row.ClusterId.Should().Be(9, "the EXCLUDED value must overwrite cluster_id");
        row.ClusterLabel.Should().Be("second-label", "cluster_label gets the EXCLUDED value");
        row.Distance.Should().Be(0.01, "distance is replaced wholesale, not kept");
        row.ModelVersion.Should().Be("cosine-v1", "re-clustering under a newer model bumps model_version");
        row.ComputedAt.Should().Be(secondComputedAt, "computed_at must move forward with each successful re-clustering pass");

        var totalRows = await CountSpanClustersAsync(ct);
        totalRows.Should().Be(1, "ON CONFLICT DO UPDATE keeps a single row per span_id — never duplicates");
    }

    [Fact]
    public async Task UpsertSpanClusters_persists_every_row_in_a_multi_row_batch()
    {
        var ct = TestContext.Current.CancellationToken;
        var computedAt = new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);

        await _store.UpsertSpanClustersAsync(
            [
                new SpanClusterRow("span-batch-a", ClusterId: 1, ClusterLabel: "alpha", Distance: 0.1, ModelVersion: "cosine-v1", ComputedAt: computedAt),
                new SpanClusterRow("span-batch-b", ClusterId: 2, ClusterLabel: "beta", Distance: 0.2, ModelVersion: "cosine-v1", ComputedAt: computedAt),
                new SpanClusterRow("span-batch-c", ClusterId: 3, ClusterLabel: "gamma", Distance: 0.3, ModelVersion: "cosine-v1", ComputedAt: computedAt)
            ],
            ct);

        var total = await CountSpanClustersAsync(ct);
        total.Should().Be(3,
            "the foreach inside `ExecuteWriteAsync` runs once per row; partial loss would silently swallow assignments");
    }

    private static string ChatAttributesFor(string label) =>
        $$"""{"gen_ai.operation.name":"chat","gen_ai.input.messages":"{{label}}"}""";

    private Task SeedChatSpanAsync(string spanId, long startNs, string? attributesJson, CancellationToken ct) =>
        _store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind,
                                   start_time_unix_nano, end_time_unix_nano, duration_ns,
                                   status_code, attributes_json)
                              VALUES ($1, 'trace-spanclusters', 'chat-completion', 'CLIENT',
                                      $2, $3, $4,
                                      'OK', $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNs });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNs + 1_000 });
            cmd.Parameters.Add(new DuckDBParameter { Value = 1_000L });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)attributesJson ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token);
        }, ct);

    private async Task<long> CountSpanClustersAsync(CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM span_clusters";
        return await cmd.ExecuteScalarAsync(ct) switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };
    }

    private async Task<SpanClusterReadback?> ReadSpanClusterAsync(string spanId, CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT cluster_id, cluster_label, distance, model_version, computed_at
                          FROM span_clusters WHERE span_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = spanId });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new SpanClusterReadback(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDouble(2),
            reader.GetString(3),
            DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc));
    }

    private sealed record SpanClusterReadback(
        int ClusterId,
        string ClusterLabel,
        double Distance,
        string ModelVersion,
        DateTime ComputedAt);
}
