
using Qyl.Contracts.Primitives;

namespace Qyl.Collector.AgentRuns;

public sealed class AgentInsightsService(DuckDbStore store)
{
    private static readonly HashSet<string> s_validBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        "1 minute",
        "5 minutes",
        "15 minutes",
        "30 minutes",
        "1 hour",
        "4 hours",
        "12 hours",
        "1 day",
        "1 week",
        "1 month"
    };

    private static string AutoBucket(long fromUnixMs, long toUnixMs)
    {
        var rangeMs = toUnixMs - fromUnixMs;
        return rangeMs switch
        {
            < 2 * 3600_000L => "5 minutes",
            < 24 * 3600_000L => "1 hour",
            < 7 * 86400_000L => "4 hours",
            < 30 * 86400_000L => "1 day",
            _ => "1 week"
        };
    }

    private static string BucketInterval(string? bucket, long fromUnixMs, long toUnixMs) =>
        bucket is null or "auto" || !s_validBuckets.Contains(bucket)
            ? AutoBucket(fromUnixMs, toUnixMs)
            : bucket;

    private static decimal MsToNano(long ms) => (decimal)ms * 1_000_000;


    public async Task<TrafficResult> GetTrafficAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                          + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                          + " COUNT(*) AS runs,"
                                                          + " SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1 ELSE 0 END) AS errors,"
                                                          + " ROUND(SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1.0 ELSE 0 END) / COUNT(*) * 100, 2) AS error_rate"
                                                          + " FROM spans"
                                                          + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                          + " AND (gen_ai_request_model IS NOT NULL OR gen_ai_tool_name IS NOT NULL)"
                                                          + " GROUP BY bucket ORDER BY bucket";

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var buckets = new List<TrafficBucket>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            buckets.Add(new TrafficBucket
            {
                Time = reader.GetDateTime(0).ToString("o"),
                Runs = reader.Col(1).GetInt64(0),
                Errors = reader.Col(2).GetInt64(0),
                ErrorRate = reader.Col(3).GetDouble(0)
            });
        }

        return new TrafficResult { Buckets = buckets };
    }


    public async Task<DurationResult> GetDurationAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                          + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                          + " ROUND(AVG(duration_ns / 1000000.0), 2) AS avg_ms,"
                                                          + " ROUND(quantile_cont(duration_ns / 1000000.0, 0.95), 2) AS p95_ms"
                                                          + " FROM spans"
                                                          + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                          + " AND (gen_ai_request_model IS NOT NULL OR gen_ai_tool_name IS NOT NULL)"
                                                          + " GROUP BY bucket ORDER BY bucket";

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var buckets = new List<DurationBucket>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            buckets.Add(new DurationBucket
            {
                Time = reader.GetDateTime(0).ToString("o"),
                AvgMs = reader.Col(1).GetDouble(0),
                P95Ms = reader.Col(2).GetDouble(0)
            });
        }

        return new DurationResult { Buckets = buckets };
    }


    public async Task<IssuesResult> GetIssuesAsync(
        long fromMs, long toMs, int limit = 10, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT
                              COALESCE(status_message, name) AS error,
                              COUNT(*) AS count,
                              array_agg(DISTINCT trace_id ORDER BY trace_id)[:5] AS sample_traces
                          FROM spans
                          WHERE start_time_unix_nano >= $1
                            AND start_time_unix_nano < $2
                            AND TRY_CAST(status_code AS INTEGER) = 2
                            AND (gen_ai_request_model IS NOT NULL OR gen_ai_tool_name IS NOT NULL)
                          GROUP BY error
                          ORDER BY count DESC
                          LIMIT $3
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var issues = new List<IssueItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            issues.Add(new IssueItem
            {
                Error = reader.Col(0).AsString ?? "Unknown error",
                Count = reader.Col(1).GetInt64(0),
                SampleTraceIds = ReadStringArray(reader, 2)
            });
        }

        return new IssuesResult { Issues = issues };
    }


    public async Task<ModelTimeseriesResult> GetLlmCallsAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                          + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                          + " COALESCE(gen_ai_request_model, 'unknown') AS model,"
                                                          + " COUNT(*) AS count FROM spans"
                                                          + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                          + " AND gen_ai_request_model IS NOT NULL"
                                                          + " GROUP BY bucket, model ORDER BY bucket, count DESC";

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        return await ReadModelTimeseries(cmd, ct).ConfigureAwait(false);
    }


    public async Task<ModelTimeseriesResult> GetTokensAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                          + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                          + " COALESCE(gen_ai_request_model, 'unknown') AS model,"
                                                          + " COALESCE(SUM(gen_ai_input_tokens + gen_ai_output_tokens), 0) AS count"
                                                          + " FROM spans"
                                                          + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                          + " AND gen_ai_request_model IS NOT NULL"
                                                          + " GROUP BY bucket, model ORDER BY bucket, count DESC";

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        return await ReadModelTimeseries(cmd, ct).ConfigureAwait(false);
    }


    public async Task<ToolTimeseriesResult> GetToolCallsTimeseriesAsync(
        long fromMs, long toMs, string? bucket = null, int topN = 8, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        await using var topCmd = lease.Connection.CreateCommand();
        topCmd.CommandText = """
                             SELECT gen_ai_tool_name, COUNT(*) AS cnt
                             FROM spans
                             WHERE start_time_unix_nano >= $1
                               AND start_time_unix_nano < $2
                               AND gen_ai_tool_name IS NOT NULL
                             GROUP BY gen_ai_tool_name
                             ORDER BY cnt DESC
                             LIMIT $3
                             """;
        topCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        topCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });
        topCmd.Parameters.Add(new DuckDBParameter { Value = topN });

        var topTools = new List<string>();
        var totals = new Dictionary<string, long>();
        await using (var tr = await topCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await tr.ReadAsync(ct).ConfigureAwait(false))
            {
                var name = tr.Col(0).AsString ?? "unknown";
                topTools.Add(name);
                totals[name] = tr.Col(1).GetInt64(0);
            }
        }

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                          + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                          + " COALESCE(gen_ai_tool_name, 'unknown') AS tool,"
                                                          + " COUNT(*) AS count FROM spans"
                                                          + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                          + " AND gen_ai_tool_name IS NOT NULL"
                                                          + " GROUP BY bucket, tool ORDER BY bucket, count DESC";

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var topSet = new HashSet<string>(topTools);
        var bucketMap = new SortedDictionary<string, Dictionary<string, long>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var time = reader.GetDateTime(0).ToString("o");
            var tool = reader.Col(1).AsString ?? "unknown";
            var count = reader.Col(2).GetInt64(0);

            var key = topSet.Contains(tool) ? tool : "Other";
            if (!bucketMap.TryGetValue(time, out var entry))
            {
                entry = [];
                bucketMap[time] = entry;
            }

            entry[key] = entry.GetValueOrDefault(key) + count;
        }

        var buckets = bucketMap
            .Select(kv => new ToolTimeBucket { Time = kv.Key, Tools = new Dictionary<string, long>(kv.Value) })
            .ToList();

        var legend = topTools
            .Select(t => new LegendItem { Name = t, Total = totals.GetValueOrDefault(t) })
            .ToList();

        return new ToolTimeseriesResult { Buckets = buckets, Legend = legend };
    }


    public async Task<TraceListResult> GetAgentTracesAsync(
        long fromMs, long toMs, int limit = 50, int offset = 0,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          WITH agent_traces AS (
                              SELECT
                                  trace_id,
                                  MIN(start_time_unix_nano) AS start_nano,
                                  MAX(end_time_unix_nano) AS end_nano,
                                  SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1 ELSE 0 END) AS errors,
                                  SUM(CASE WHEN gen_ai_request_model IS NOT NULL THEN 1 ELSE 0 END) AS llm_calls,
                                  SUM(CASE WHEN gen_ai_tool_name IS NOT NULL THEN 1 ELSE 0 END) AS tool_calls,
                                  COALESCE(SUM(gen_ai_input_tokens + gen_ai_output_tokens), 0) AS total_tokens,
                                  COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost,
                                  MAX(CASE WHEN parent_span_id IS NULL OR parent_span_id = '' THEN name END) AS root_name
                              FROM spans
                              WHERE start_time_unix_nano >= $1
                                AND start_time_unix_nano < $2
                                AND (gen_ai_request_model IS NOT NULL OR gen_ai_tool_name IS NOT NULL)
                              GROUP BY trace_id
                          )
                          SELECT
                              t.trace_id,
                              t.start_nano,
                              t.end_nano,
                              t.errors,
                              t.llm_calls,
                              t.tool_calls,
                              t.total_tokens,
                              t.total_cost,
                              t.root_name,
                              a.agent_name
                          FROM agent_traces t
                          LEFT JOIN agent_runs a ON t.trace_id = a.trace_id
                          ORDER BY t.start_nano DESC
                          LIMIT $3 OFFSET $4
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        cmd.Parameters.Add(new DuckDBParameter { Value = offset });

        var items = new List<AgentTraceRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startNano = reader.Col(1).GetUInt64(0);
            var endNano = reader.Col(2).GetUInt64(0);

            items.Add(new AgentTraceRow
            {
                TraceId = reader.GetString(0),
                Timestamp = TimeConversions.UnixNanoToDateTime(startNano).ToString("o"),
                RootDurationMs = TimeConversions.NanosToMs(endNano - startNano),
                Errors = reader.Col(3).GetInt64(0),
                LlmCalls = reader.Col(4).GetInt64(0),
                ToolCalls = reader.Col(5).GetInt64(0),
                TotalTokens = reader.Col(6).GetInt64(0),
                TotalCost = reader.Col(7).GetDouble(0),
                RootName = reader.Col(8).AsString,
                AgentName = reader.Col(9).AsString
            });
        }

        await using var countCmd = lease.Connection.CreateCommand();
        countCmd.CommandText = """
                               SELECT COUNT(DISTINCT trace_id)
                               FROM spans
                               WHERE start_time_unix_nano >= $1
                                 AND start_time_unix_nano < $2
                                 AND (gen_ai_request_model IS NOT NULL OR gen_ai_tool_name IS NOT NULL)
                               """;
        countCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        countCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });
        var total = (long)(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);

        return new TraceListResult { Items = items, Total = total };
    }


    public async Task<ModelsResult> GetModelsAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(gen_ai_request_model, 'unknown') AS model,
                              COUNT(*) AS calls,
                              COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens,
                              COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens,
                              COALESCE(SUM(gen_ai_cost_usd), 0) AS cost,
                              ROUND(AVG(duration_ns / 1000000.0), 2) AS avg_ms,
                              ROUND(SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1.0 ELSE 0 END) / COUNT(*) * 100, 2) AS error_rate
                          FROM spans
                          WHERE start_time_unix_nano >= $1
                            AND start_time_unix_nano < $2
                            AND gen_ai_request_model IS NOT NULL
                          GROUP BY model
                          ORDER BY calls DESC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var models = new List<ModelRow>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                models.Add(new ModelRow
                {
                    Name = reader.Col(0).AsString ?? "unknown",
                    Calls = reader.Col(1).GetInt64(0),
                    InputTokens = reader.Col(2).GetInt64(0),
                    OutputTokens = reader.Col(3).GetInt64(0),
                    Cost = reader.Col(4).GetDouble(0),
                    AvgDurationMs = reader.Col(5).GetDouble(0),
                    ErrorRate = reader.Col(6).GetDouble(0)
                });
            }
        }

        await using var tsCmd = lease.Connection.CreateCommand();
        tsCmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                            + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                            + " COALESCE(gen_ai_request_model, 'unknown') AS model,"
                                                            + " COUNT(*) AS count FROM spans"
                                                            + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                            + " AND gen_ai_request_model IS NOT NULL"
                                                            + " GROUP BY bucket, model ORDER BY bucket";
        tsCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        tsCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var timeseries = await ReadModelTimeseries(tsCmd, ct).ConfigureAwait(false);

        return new ModelsResult { Models = models, Timeseries = timeseries.Buckets, Legend = timeseries.Legend };
    }


    public async Task<ToolsResult> GetToolsAsync(
        long fromMs, long toMs, string? bucket = null, CancellationToken ct = default)
    {
        var interval = BucketInterval(bucket, fromMs, toMs);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COALESCE(gen_ai_tool_name, 'unknown') AS tool,
                              COUNT(*) AS calls,
                              ROUND(AVG(duration_ns / 1000000.0), 2) AS avg_ms,
                              ROUND(SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1.0 ELSE 0 END) / COUNT(*) * 100, 2) AS error_rate
                          FROM spans
                          WHERE start_time_unix_nano >= $1
                            AND start_time_unix_nano < $2
                            AND gen_ai_tool_name IS NOT NULL
                          GROUP BY tool
                          ORDER BY calls DESC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        cmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var tools = new List<ToolRow>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                tools.Add(new ToolRow
                {
                    Name = reader.Col(0).AsString ?? "unknown",
                    Calls = reader.Col(1).GetInt64(0),
                    AvgDurationMs = reader.Col(2).GetDouble(0),
                    ErrorRate = reader.Col(3).GetDouble(0)
                });
            }
        }

        await using var tsCmd = lease.Connection.CreateCommand();
        tsCmd.CommandText = "SELECT time_bucket(INTERVAL '" + interval
                                                            + "', make_timestamp(CAST(start_time_unix_nano / 1000 AS BIGINT))) AS bucket,"
                                                            + " COALESCE(gen_ai_tool_name, 'unknown') AS tool,"
                                                            + " COUNT(*) AS count FROM spans"
                                                            + " WHERE start_time_unix_nano >= $1 AND start_time_unix_nano < $2"
                                                            + " AND gen_ai_tool_name IS NOT NULL"
                                                            + " GROUP BY bucket, tool ORDER BY bucket, count DESC";
        tsCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(fromMs) });
        tsCmd.Parameters.Add(new DuckDBParameter { Value = MsToNano(toMs) });

        var topToolNames = tools.Take(8).Select(t => t.Name).ToHashSet();
        var bucketMap = new SortedDictionary<string, Dictionary<string, long>>();
        var totals = new Dictionary<string, long>();

        await using (var reader = await tsCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var time = reader.GetDateTime(0).ToString("o");
                var tool = reader.Col(1).AsString ?? "unknown";
                var count = reader.Col(2).GetInt64(0);

                var key = topToolNames.Contains(tool) ? tool : "Other";
                if (!bucketMap.TryGetValue(time, out var entry))
                {
                    entry = [];
                    bucketMap[time] = entry;
                }

                entry[key] = entry.GetValueOrDefault(key) + count;
                totals[key] = totals.GetValueOrDefault(key) + count;
            }
        }

        var tsBuckets = bucketMap
            .Select(kv => new ToolTimeBucket { Time = kv.Key, Tools = kv.Value })
            .ToList();

        var legend = tools.Take(8)
            .Select(t => new LegendItem { Name = t.Name, Total = t.Calls })
            .ToList();

        return new ToolsResult { Tools = tools, Timeseries = tsBuckets, Legend = legend };
    }


    public async Task<IReadOnlyList<TraceSpan>> GetTraceSpansAsync(
        string traceId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT
                              span_id, parent_span_id, name,
                              start_time_unix_nano, end_time_unix_nano, duration_ns,
                              status_code, status_message,
                              gen_ai_provider_name, gen_ai_request_model,
                              gen_ai_input_tokens, gen_ai_output_tokens,
                              gen_ai_tool_name, gen_ai_cost_usd,
                              gen_ai_stop_reason, attributes_json
                          FROM spans
                          WHERE trace_id = $1
                          ORDER BY start_time_unix_nano ASC
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

        var spans = new List<TraceSpan>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var startNano = reader.Col(3).GetUInt64(0);
            spans.Add(new TraceSpan
            {
                SpanId = reader.GetString(0),
                ParentSpanId = reader.Col(1).AsString,
                Name = reader.GetString(2),
                Timestamp = TimeConversions.UnixNanoToDateTime(startNano).ToString("o"),
                DurationMs = TimeConversions.NanosToMs(reader.Col(5).GetUInt64(0)),
                StatusCode = (int)reader.Col(6).GetDouble(0),
                StatusMessage = reader.Col(7).AsString,
                Provider = reader.Col(8).AsString,
                Model = reader.Col(9).AsString,
                InputTokens = reader.Col(10).AsInt64 ?? 0,
                OutputTokens = reader.Col(11).AsInt64 ?? 0,
                ToolName = reader.Col(12).AsString,
                Cost = reader.Col(13).AsDouble ?? 0,
                StopReason = reader.Col(14).AsString,
                AttributesJson = reader.Col(15).AsString
            });
        }

        return spans;
    }


    private static async Task<ModelTimeseriesResult> ReadModelTimeseries(
        DuckDBCommand cmd, CancellationToken ct)
    {
        var bucketMap = new SortedDictionary<string, Dictionary<string, long>>();
        var totals = new Dictionary<string, long>();

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var time = reader.GetDateTime(0).ToString("o");
            var model = reader.Col(1).AsString ?? "unknown";
            var count = reader.Col(2).GetInt64(0);

            if (!bucketMap.TryGetValue(time, out var entry))
            {
                entry = [];
                bucketMap[time] = entry;
            }

            entry[model] = entry.GetValueOrDefault(model) + count;
            totals[model] = totals.GetValueOrDefault(model) + count;
        }

        var buckets = bucketMap
            .Select(static kv => new ModelTimeBucket { Time = kv.Key, Models = kv.Value })
            .ToList();

        var legend = totals
            .OrderByDescending(static kv => kv.Value)
            .Select(static kv => new LegendItem { Name = kv.Key, Total = kv.Value })
            .ToList();

        return new ModelTimeseriesResult { Buckets = buckets, Legend = legend };
    }

    private static List<string> ReadStringArray(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var value = reader.GetValue(ordinal);
        switch (value)
        {
            case IReadOnlyList<string> list:
                return [.. list];
            case Array arr:
            {
                var result = new List<string>(arr.Length);
                result.AddRange(from object? item in arr select item?.ToString() ?? "");
                return result;
            }
            default:
                return [];
        }
    }
}


public sealed record TrafficResult
{
    public IReadOnlyList<TrafficBucket> Buckets { get; init; } = [];
}

public sealed record TrafficBucket
{
    public required string Time { get; init; }
    public long Runs { get; init; }
    public long Errors { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record DurationResult
{
    public IReadOnlyList<DurationBucket> Buckets { get; init; } = [];
}

public sealed record DurationBucket
{
    public required string Time { get; init; }
    public double AvgMs { get; init; }
    public double P95Ms { get; init; }
}

public sealed record IssuesResult
{
    public IReadOnlyList<IssueItem> Issues { get; init; } = [];
}

public sealed record IssueItem
{
    public required string Error { get; init; }
    public long Count { get; init; }
    public IReadOnlyList<string> SampleTraceIds { get; init; } = [];
}

public sealed record ModelTimeseriesResult
{
    public IReadOnlyList<ModelTimeBucket> Buckets { get; init; } = [];
    public IReadOnlyList<LegendItem> Legend { get; init; } = [];
}

public sealed record ModelTimeBucket
{
    public required string Time { get; init; }
    public Dictionary<string, long> Models { get; init; } = [];
}

public sealed record ToolTimeseriesResult
{
    public IReadOnlyList<ToolTimeBucket> Buckets { get; init; } = [];
    public IReadOnlyList<LegendItem> Legend { get; init; } = [];
}

public sealed record ToolTimeBucket
{
    public required string Time { get; init; }
    public Dictionary<string, long> Tools { get; init; } = [];
}

public sealed record LegendItem
{
    public required string Name { get; init; }
    public long Total { get; init; }
}

public sealed record TraceListResult
{
    public IReadOnlyList<AgentTraceRow> Items { get; init; } = [];
    public long Total { get; init; }
}

public sealed record AgentTraceRow
{
    public required string TraceId { get; init; }
    public required string Timestamp { get; init; }
    public double RootDurationMs { get; init; }
    public long Errors { get; init; }
    public long LlmCalls { get; init; }
    public long ToolCalls { get; init; }
    public long TotalTokens { get; init; }
    public double TotalCost { get; init; }
    public string? RootName { get; init; }
    public string? AgentName { get; init; }
}

public sealed record ModelsResult
{
    public IReadOnlyList<ModelRow> Models { get; init; } = [];
    public IReadOnlyList<ModelTimeBucket> Timeseries { get; init; } = [];
    public IReadOnlyList<LegendItem> Legend { get; init; } = [];
}

public sealed record ModelRow
{
    public required string Name { get; init; }
    public long Calls { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public double Cost { get; init; }
    public double AvgDurationMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record ToolsResult
{
    public IReadOnlyList<ToolRow> Tools { get; init; } = [];
    public IReadOnlyList<ToolTimeBucket> Timeseries { get; init; } = [];
    public IReadOnlyList<LegendItem> Legend { get; init; } = [];
}

public sealed record ToolRow
{
    public required string Name { get; init; }
    public long Calls { get; init; }
    public double AvgDurationMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record TraceSpan
{
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required string Timestamp { get; init; }
    public double DurationMs { get; init; }
    public int StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public string? ToolName { get; init; }
    public double Cost { get; init; }
    public string? StopReason { get; init; }
    public string? AttributesJson { get; init; }
}
