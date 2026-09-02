using DuckDB.NET.Data;
using System.Text;

namespace Qyl.Collector.Storage;

/// <summary>
/// A metric range query in the Grafana/Prometheus shape: a metric, label matchers, a time
/// range, and a step. The collector answers it server-side and returns buckets, never raw
/// points — the primary consumer is an agent over MCP, and a compact bucketed answer is both
/// the cheaper payload and the one it can reason about without a second round trip.
/// </summary>
internal sealed record MetricRangeQuery
{
    public required string ProjectId { get; init; }
    public required string MetricName { get; init; }
    public required ulong StartUnixNano { get; init; }
    public required ulong EndUnixNano { get; init; }

    /// <summary>Bucket width in nanoseconds. One bucket covering the range collapses the query to a single value.</summary>
    public required ulong StepUnixNano { get; init; }

    public MetricAggregation Aggregation { get; init; } = MetricAggregation.Avg;

    /// <summary>Attribute keys to split the result by. Empty collapses every matching series into one stream.</summary>
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    public IReadOnlyList<MetricAttributeMatcher> Matchers { get; init; } = [];

    /// <summary>Maximum result streams. Grouping on a high-cardinality attribute is truncated, not refused.</summary>
    public int SeriesLimit { get; init; } = 50;
}

internal sealed partial class DuckDbStore
{
    private const int MaxMetricSeriesPerBatch = 120;

    private const int MaxMetricPointsPerBatch = 150;

    /// <summary>
    /// Percentiles fold bucket vectors that SQL cannot sum element-wise, so their rows are
    /// read out and folded here. The cap keeps that read bounded; every other aggregation is
    /// computed entirely in DuckDB and is unaffected.
    /// </summary>
    private const int MaxHistogramRowsPerQuery = 20_000;

    public async Task InsertMetricsAsync(
        IReadOnlyList<MetricSeriesRow> series,
        IReadOnlyList<MetricPointRow> points,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (points.Count is 0)
            return;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            // Series first: a point is only reachable through its series row, so writing the
            // point first would expose an unjoinable sample to a concurrent reader.
            await InsertRowsBatchedAsync(con, tx, series, MetricSeriesRow.AddParameters,
                MetricSeriesRow.BuildMultiRowInsertSql, MaxMetricSeriesPerBatch, token);
            await InsertRowsBatchedAsync(con, tx, points, MetricPointRow.AddParameters,
                MetricPointRow.BuildMultiRowInsertSql, MaxMetricPointsPerBatch, token);
            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<MetricCatalogEntry>> ListMetricsAsync(
        string projectId,
        string? namePrefix = null,
        int limit = 200,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<MetricCatalogEntry>>(con =>
        {
            using var cmd = con.CreateCommand();
            var filter = string.IsNullOrEmpty(namePrefix)
                ? ""
                : " AND starts_with(metric_name, $3)";
            // Composed, not interpolated, into CommandText: every fragment spliced here is a
            // constant chosen by this method, and every caller value is a bound parameter.
            // An aggregate projection, not a row read: the catalog collapses every series
            // under a name into one entry, so there is no generated column list to reuse
            // and the reader ordinals below are this statement's own contract.
            var sql = """
                      SELECT count(*),
                             metric_name,
                             max(metric_kind),
                             max(temporality),
                             max(is_monotonic),
                             max(unit),
                             max(description),
                             max(last_seen_unix_nano)
                      FROM metric_series
                      WHERE project_id = $1
                      """
                      + filter
                      + " GROUP BY metric_name ORDER BY metric_name LIMIT $2";
            cmd.CommandText = sql;
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            if (filter.Length > 0)
                cmd.Parameters.Add(new DuckDBParameter { Value = namePrefix });

            var entries = new List<MetricCatalogEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(new MetricCatalogEntry(
                    DuckDbValueReader.ReadString(reader, 1, ""),
                    DuckDbValueReader.ReadByte(reader, 2, 0),
                    DuckDbValueReader.ReadByte(reader, 3, 0),
                    DuckDbValueReader.ReadByte(reader, 4, 0),
                    DuckDbValueReader.ReadString(reader, 5),
                    DuckDbValueReader.ReadString(reader, 6),
                    DuckDbValueReader.ReadInt64(reader, 0, 0),
                    DuckDbValueReader.ReadUInt64(reader, 7, 0)));
            }

            return entries;
        }, ct);
    }

    public Task<IReadOnlyList<MetricSeriesRow>> ListMetricSeriesAsync(
        string projectId,
        string metricName,
        IReadOnlyList<MetricAttributeMatcher> matchers,
        int limit = 200,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<MetricSeriesRow>>(con =>
        {
            var qb = new QueryBuilder();
            qb.Add("project_id = $N", projectId);
            qb.Add("metric_name = $N", metricName);
            AppendMatchers(ref qb, matchers);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + MetricSeriesRow.SelectColumnList
                              + " FROM metric_series " + qb.WhereClause
                              + " ORDER BY last_seen_unix_nano DESC, series_id LIMIT " + qb.NextParam;
            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            var rows = new List<MetricSeriesRow>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                rows.Add(MetricSeriesRow.MapFromReader(reader));

            return rows;
        }, ct);
    }

    public Task<IReadOnlyList<MetricQuerySeries>> QueryMetricAsync(
        MetricRangeQuery request,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (request.StepUnixNano is 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Metric query step must be positive.");
        if (request.EndUnixNano < request.StartUnixNano)
            throw new ArgumentOutOfRangeException(nameof(request), "Metric query end must not precede its start.");

        return ExecuteReadAsync<IReadOnlyList<MetricQuerySeries>>(
            con => IsPercentile(request.Aggregation)
                ? QueryHistogramPercentile(con, request)
                : QueryAggregate(con, request),
            ct);
    }

    private static IReadOnlyList<MetricQuerySeries> QueryAggregate(
        DuckDBConnection con,
        MetricRangeQuery request)
    {
        var kind = ResolveMetricKind(con, request);
        var qb = new QueryBuilder();
        qb.Add("s.project_id = $N", request.ProjectId);
        qb.Add("s.metric_name = $N", request.MetricName);
        AppendMatchers(ref qb, request.Matchers, "s.");
        qb.Add("p.time_unix_nano >= $N", (decimal)request.StartUnixNano);
        qb.Add("p.time_unix_nano <= $N", (decimal)request.EndUnixNano);

        var groupExpression = BuildGroupExpression(request.GroupBy);
        var sample = BuildSampleExpression(kind, request.Aggregation);
        var start = request.StartUnixNano.ToString(CultureInfo.InvariantCulture);
        var step = request.StepUnixNano.ToString(CultureInfo.InvariantCulture);

        using var cmd = con.CreateCommand();
        // Composed, not interpolated, into CommandText: the spliced fragments are the
        // aggregate and grouping expressions this method derives, plus the range and step
        // rendered from unsigned integers. Every caller-supplied value is a bound parameter.
        var sql = "SELECT " + groupExpression + " AS group_key, "
                  + BuildBucketExpression(start, step) + " AS bucket_start, "
                  + sample + " AS sample, count(*) AS point_count "
                  + "FROM metric_points AS p JOIN metric_series AS s "
                  + "ON s.project_id = p.project_id AND s.series_id = p.series_id "
                  + qb.WhereClause
                  + " GROUP BY group_key, bucket_start ORDER BY group_key, bucket_start";
        cmd.CommandText = sql;
        qb.ApplyTo(cmd);

        var streams = new Dictionary<string, List<MetricAggregatePoint>>(StringComparer.Ordinal);
        var order = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var groupKey = DuckDbValueReader.ReadString(reader, 0) ?? "";
                if (!streams.TryGetValue(groupKey, out var bucket))
                {
                    if (order.Count >= request.SeriesLimit)
                        continue;
                    bucket = [];
                    streams[groupKey] = bucket;
                    order.Add(groupKey);
                }

                bucket.Add(new MetricAggregatePoint(
                    DuckDbValueReader.ReadUInt64(reader, 1, 0),
                    DuckDbValueReader.ReadDouble(reader, 2),
                    DuckDbValueReader.ReadUInt64(reader, 3, 0)));
            }
        }

        return [.. order.Select(key => new MetricQuerySeries(key, ToGroupAttributesJson(request.GroupBy, key), streams[key]))];
    }

    /// <summary>
    /// Percentiles need bucket vectors summed element-wise across the points falling in one
    /// step, which SQL has no aggregate for. The rows are read out (bounded) and folded here.
    /// </summary>
    private static IReadOnlyList<MetricQuerySeries> QueryHistogramPercentile(
        DuckDBConnection con,
        MetricRangeQuery request)
    {
        var qb = new QueryBuilder();
        qb.Add("s.project_id = $N", request.ProjectId);
        qb.Add("s.metric_name = $N", request.MetricName);
        AppendMatchers(ref qb, request.Matchers, "s.");
        qb.Add("p.time_unix_nano >= $N", (decimal)request.StartUnixNano);
        qb.Add("p.time_unix_nano <= $N", (decimal)request.EndUnixNano);

        var groupExpression = BuildGroupExpression(request.GroupBy);
        var start = request.StartUnixNano.ToString(CultureInfo.InvariantCulture);
        var step = request.StepUnixNano.ToString(CultureInfo.InvariantCulture);

        using var cmd = con.CreateCommand();
        var sql = "SELECT " + groupExpression + " AS group_key, "
                  + BuildBucketExpression(start, step) + " AS bucket_start, "
                  + "p.bucket_bounds_json, p.bucket_counts_json "
                  + "FROM metric_points AS p JOIN metric_series AS s "
                  + "ON s.project_id = p.project_id AND s.series_id = p.series_id "
                  + qb.WhereClause
                  + " AND p.bucket_counts_json IS NOT NULL "
                  + "ORDER BY group_key, bucket_start LIMIT "
                  + MaxHistogramRowsPerQuery.ToString(CultureInfo.InvariantCulture);
        cmd.CommandText = sql;
        qb.ApplyTo(cmd);

        var folded = new Dictionary<string, Dictionary<ulong, HistogramFold>>(StringComparer.Ordinal);
        var order = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var groupKey = DuckDbValueReader.ReadString(reader, 0) ?? "";
                if (!folded.TryGetValue(groupKey, out var buckets))
                {
                    if (order.Count >= request.SeriesLimit)
                        continue;
                    buckets = [];
                    folded[groupKey] = buckets;
                    order.Add(groupKey);
                }

                var bucketStart = DuckDbValueReader.ReadUInt64(reader, 1, 0);
                var bounds = ParseDoubleArray(DuckDbValueReader.ReadString(reader, 2));
                var counts = ParseUInt64Array(DuckDbValueReader.ReadString(reader, 3));
                if (counts is null)
                    continue;

                if (!buckets.TryGetValue(bucketStart, out var fold))
                    buckets[bucketStart] = fold = new HistogramFold();
                fold.Add(bounds, counts);
            }
        }

        var quantile = QuantileOf(request.Aggregation);
        return
        [
            .. order.Select(key => new MetricQuerySeries(
                key,
                ToGroupAttributesJson(request.GroupBy, key),
                [
                    .. folded[key]
                        .OrderBy(static item => item.Key)
                        .Select(item => new MetricAggregatePoint(
                            item.Key,
                            item.Value.Quantile(quantile),
                            item.Value.TotalCount))
                ]))
        ];
    }

    /// <summary>
    /// Sums bucket vectors that share explicit bounds. Points whose bounds disagree (an SDK
    /// reconfigured mid-window) are kept apart and the widest-count layout wins, because
    /// merging incompatible layouts would silently invent a distribution.
    /// </summary>
    private sealed class HistogramFold
    {
        private double[]? _bounds;
        private ulong[]? _counts;

        public ulong TotalCount { get; private set; }

        public void Add(double[]? bounds, ulong[] counts)
        {
            TotalCount += counts.Aggregate(0UL, static (sum, value) => sum + value);

            if (_counts is null)
            {
                _bounds = bounds;
                _counts = [.. counts];
                return;
            }

            if (_counts.Length != counts.Length || !BoundsMatch(_bounds, bounds))
            {
                if (counts.Length > _counts.Length)
                {
                    _bounds = bounds;
                    _counts = [.. counts];
                }

                return;
            }

            for (var i = 0; i < counts.Length; i++)
                _counts[i] += counts[i];
        }

        public double? Quantile(double quantile)
        {
            if (_counts is not { Length: > 0 } counts || _bounds is not { Length: > 0 } bounds)
                return null;

            var total = counts.Aggregate(0UL, static (sum, value) => sum + value);
            if (total is 0)
                return null;

            var target = quantile * total;
            var cumulative = 0UL;
            for (var i = 0; i < counts.Length; i++)
            {
                var next = cumulative + counts[i];
                if (next < target || counts[i] is 0)
                {
                    cumulative = next;
                    continue;
                }

                // The first bucket is (-infinity, bounds[0]] and the last (bounds[^1], +infinity):
                // neither has two finite edges, so report the finite one rather than extrapolate.
                if (i is 0)
                    return bounds[0];
                if (i >= bounds.Length)
                    return bounds[^1];

                var lower = bounds[i - 1];
                var upper = bounds[i];
                var withinBucket = (target - cumulative) / counts[i];
                return lower + (upper - lower) * withinBucket;
            }

            return bounds[^1];
        }

        private static bool BoundsMatch(double[]? left, double[]? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            return left.AsSpan().SequenceEqual(right);
        }
    }

    private static byte ResolveMetricKind(DuckDBConnection con, MetricRangeQuery request)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
                          SELECT max(metric_kind) FROM metric_series
                          WHERE project_id = $1 AND metric_name = $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = request.ProjectId });
        cmd.Parameters.Add(new DuckDBParameter { Value = request.MetricName });
        var value = cmd.ExecuteScalar();
        return value is null or DBNull
            ? (byte)MetricKind.Gauge
            : Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The column an aggregate reads depends on the metric's shape: a histogram has no scalar
    /// value, so its average is the recorded sum over the recorded count, not an average of
    /// per-point averages.
    /// </summary>
    private static string BuildSampleExpression(byte metricKind, MetricAggregation aggregation)
    {
        var isHistogram = metricKind is (byte)MetricKind.Histogram or (byte)MetricKind.ExponentialHistogram;
        if (!isHistogram)
        {
            return aggregation switch
            {
                MetricAggregation.Avg => "avg(p.value)",
                MetricAggregation.Min => "min(p.value)",
                MetricAggregation.Max => "max(p.value)",
                MetricAggregation.Sum => "sum(p.value)",
                MetricAggregation.Count => "CAST(count(p.value) AS DOUBLE)",
                MetricAggregation.Last => "arg_max(p.value, p.time_unix_nano)",
                _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unsupported metric aggregation.")
            };
        }

        return aggregation switch
        {
            MetricAggregation.Avg =>
                "CASE WHEN sum(p.histogram_count) > 0 " +
                "THEN sum(p.histogram_sum) / sum(p.histogram_count) ELSE NULL END",
            MetricAggregation.Min => "min(p.histogram_min)",
            MetricAggregation.Max => "max(p.histogram_max)",
            MetricAggregation.Sum => "sum(p.histogram_sum)",
            MetricAggregation.Count => "CAST(sum(p.histogram_count) AS DOUBLE)",
            MetricAggregation.Last => "arg_max(p.histogram_sum, p.time_unix_nano)",
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unsupported metric aggregation.")
        };
    }

    /// <summary>
    /// Grouping keys are read out of the persisted attribute JSON. With no keys every matching
    /// series collapses into one stream, which is what an agent asking "what is X doing" wants.
    /// </summary>
    /// <summary>
    /// Floors a point's timestamp onto the step grid anchored at the range start. HUGEINT
    /// keeps the subtraction signed so a point exactly at the start does not wrap.
    /// </summary>
    private static string BuildBucketExpression(string start, string step) =>
        "(((CAST(p.time_unix_nano AS HUGEINT) - " + start + ") // " + step + ") * " + step +
        " + " + start + ")";

    private static string BuildGroupExpression(IReadOnlyList<string> groupBy)
    {
        if (groupBy.Count is 0)
            return "''";

        var parts = groupBy.Select(static key =>
            $"coalesce(json_extract_string(s.attributes_json, '{JsonPathLiteral(key)}'), '')");
        // chr(31) is the unit separator: DuckDB does not decode escapes inside single quotes,
        // so the separator has to be built as a function call rather than written as "\x1f".
        return string.Join(" || chr(31) || ", parts);
    }

    /// <summary>ASCII unit separator; the SQL side builds it with chr(31).</summary>
    private const char GroupKeySeparator = '\u001f';

    private static string? ToGroupAttributesJson(IReadOnlyList<string> groupBy, string groupKey)
    {
        if (groupBy.Count is 0)
            return null;

        var values = groupKey.Split(GroupKeySeparator);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            for (var i = 0; i < groupBy.Count; i++)
                writer.WriteString(groupBy[i], i < values.Length ? values[i] : "");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void AppendMatchers(
        ref QueryBuilder qb,
        IReadOnlyList<MetricAttributeMatcher> matchers,
        string alias = "")
    {
        foreach (var matcher in matchers)
        {
            var extract = $"json_extract_string({alias}attributes_json, '{JsonPathLiteral(matcher.Key)}')";
            if (matcher.IsPrefix)
                qb.Add($"starts_with({extract}, $N)", matcher.Value);
            else
                qb.Add($"{extract} = $N", matcher.Value);
        }
    }

    // Attribute keys are dotted, so they must be quoted inside the JSON path or DuckDB reads
    // them as nested members. Keys come from callers, never from stored data, but the escape
    // keeps the path a literal regardless.
    private static string JsonPathLiteral(string key) =>
        "$.\"" + key.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal) + "\"";

    private static bool IsPercentile(MetricAggregation aggregation) =>
        aggregation is MetricAggregation.P50 or MetricAggregation.P90
            or MetricAggregation.P95 or MetricAggregation.P99;

    private static double QuantileOf(MetricAggregation aggregation) => aggregation switch
    {
        MetricAggregation.P50 => 0.50,
        MetricAggregation.P90 => 0.90,
        MetricAggregation.P95 => 0.95,
        MetricAggregation.P99 => 0.99,
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Not a percentile aggregation.")
    };

    private static double[]? ParseDoubleArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        using var document = JsonDocument.Parse(json);
        var values = new double[document.RootElement.GetArrayLength()];
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
            values[index++] = element.GetDouble();
        return values;
    }

    private static ulong[]? ParseUInt64Array(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        using var document = JsonDocument.Parse(json);
        var values = new ulong[document.RootElement.GetArrayLength()];
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
            values[index++] = element.GetUInt64();
        return values;
    }

    public Task<MetricRetentionResult> DeleteExpiredMetricsBatchAsync(
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");

        return ExecuteMaintenanceWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            var deletedPoints = 0;
            await using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                                      DELETE FROM metric_points
                                      WHERE (project_id, series_id, time_unix_nano) IN (
                                          SELECT project_id, series_id, time_unix_nano
                                          FROM metric_points
                                          WHERE time_unix_nano < $1
                                          ORDER BY time_unix_nano, project_id, series_id
                                          LIMIT $2
                                      )
                                      RETURNING series_id
                                      """;
                command.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffUnixNano });
                command.Parameters.Add(new DuckDBParameter { Value = batchSize });
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    deletedPoints++;
            }

            // A series with no points left is unreachable: nothing can query it and it would
            // otherwise keep advertising a metric name that has no data behind it.
            var deletedSeries = 0;
            await using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                                      DELETE FROM metric_series
                                      WHERE NOT EXISTS (
                                          SELECT 1 FROM metric_points AS p
                                          WHERE p.project_id = metric_series.project_id
                                            AND p.series_id = metric_series.series_id
                                      )
                                      RETURNING series_id
                                      """;
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    deletedSeries++;
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new MetricRetentionResult(deletedPoints, deletedSeries);
        }, ct);
    }
}
