namespace qyl.collector.Analytics;

/// <summary>
///     Z-score anomaly detection service operating against the <c>spans</c> table
///     via DuckDB time-bucketed aggregations. Supports error rate, latency percentiles,
///     request count, token usage, and cost metrics.
/// </summary>
public sealed partial class AnomalyService(DuckDbStore store, ILogger<AnomalyService> logger)
{
    // ==========================================================================
    // Whitelisted Metrics
    // ==========================================================================

    private static readonly FrozenDictionary<string, string> MetricExpressions =
        new Dictionary<string, string>
        {
            ["error_rate"] =
                "CAST(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS DOUBLE) / NULLIF(COUNT(*), 0)",
            ["latency_p50"] = "PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY duration_ns)",
            ["latency_p95"] = "PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns)",
            ["latency_p99"] = "PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns)",
            ["request_count"] = "COUNT(*)",
            ["token_usage"] =
                "SUM(CAST(attributes_json->>'gen_ai.usage.total_tokens' AS BIGINT)) FILTER (WHERE attributes_json->>'gen_ai.usage.total_tokens' IS NOT NULL)",
            ["cost"] =
                "SUM(CAST(attributes_json->>'gen_ai.response.cost' AS DOUBLE)) FILTER (WHERE attributes_json->>'gen_ai.response.cost' IS NOT NULL)"
        }.ToFrozenDictionary();

    // ==========================================================================
    // Anomaly Detection
    // ==========================================================================

    /// <summary>
    ///     Detects Z-score anomalies in hourly-bucketed metric values over the
    ///     specified lookback window. Points exceeding the sensitivity threshold
    ///     are returned as anomalies.
    /// </summary>
    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        string metric,
        int hours = 24,
        double sensitivity = 2.0,
        string? service = null,
        CancellationToken ct = default)
    {
        string metricExpr = ValidateAndGetExpression(metric);
        long cutoffNano = ComputeCutoffNano(hours);

        await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using DuckDBCommand cmd = lease.Connection.CreateCommand();

        string serviceFilter = service is not null ? "AND service_name = $3" : "";

        cmd.CommandText = $"""
                           WITH hourly AS (
                               SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                                      {metricExpr} AS metric_value
                               FROM spans
                               WHERE start_time_unix_nano >= $1
                               {serviceFilter}
                               GROUP BY bucket
                           ),
                           stats AS (
                               SELECT AVG(metric_value) AS mean, STDDEV(metric_value) AS stddev
                               FROM hourly
                           )
                           SELECT h.bucket, h.metric_value,
                                  (h.metric_value - s.mean) / NULLIF(s.stddev, 0) AS z_score,
                                  s.mean, s.stddev
                           FROM hourly h, stats s
                           WHERE ABS((h.metric_value - s.mean) / NULLIF(s.stddev, 0)) > $2
                           ORDER BY h.bucket DESC
                           """;

        cmd.Parameters.Add(new DuckDBParameter { Value = cutoffNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = sensitivity });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        List<AnomalyPoint> anomalies = [];
        double mean = 0;
        double stddev = 0;

        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            DateTime bucket = reader.GetDateTime(0);
            double value = reader.GetDouble(1);
            double zScore = reader.Col(2).AsDouble ?? 0;
            mean = reader.Col(3).AsDouble ?? 0;
            stddev = reader.Col(4).AsDouble ?? 0;

            string direction = zScore > 0 ? "spike" : "drop";
            anomalies.Add(new AnomalyPoint(bucket, value, zScore, direction));
        }

        LogAnomalyDetection(metric, hours, anomalies.Count);

        return new AnomalyDetectionResult(metric, hours, sensitivity, mean, stddev, anomalies);
    }

    // ==========================================================================
    // Baseline Statistics
    // ==========================================================================

    /// <summary>
    ///     Computes baseline statistics (mean, stddev, percentiles) for a metric
    ///     over hourly buckets within the specified lookback window.
    /// </summary>
    public async Task<BaselineResult> GetBaselineAsync(
        string metric,
        int hours = 24,
        string? service = null,
        CancellationToken ct = default)
    {
        string metricExpr = ValidateAndGetExpression(metric);
        long cutoffNano = ComputeCutoffNano(hours);

        await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using DuckDBCommand cmd = lease.Connection.CreateCommand();

        string serviceFilter = service is not null ? "AND service_name = $2" : "";

        cmd.CommandText = $"""
                           WITH hourly AS (
                               SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                                      {metricExpr} AS metric_value
                               FROM spans
                               WHERE start_time_unix_nano >= $1
                               {serviceFilter}
                               GROUP BY bucket
                           )
                           SELECT AVG(metric_value) AS mean,
                                  STDDEV(metric_value) AS stddev,
                                  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY metric_value) AS p50,
                                  PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY metric_value) AS p95,
                                  PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY metric_value) AS p99,
                                  COUNT(*) AS sample_count
                           FROM hourly
                           """;

        cmd.Parameters.Add(new DuckDBParameter { Value = cutoffNano });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new BaselineResult(metric, hours, 0, 0, 0, 0, 0, 0);
        }

        double mean = reader.Col(0).AsDouble ?? 0;
        double stddev = reader.Col(1).AsDouble ?? 0;
        double p50 = reader.Col(2).AsDouble ?? 0;
        double p95 = reader.Col(3).AsDouble ?? 0;
        double p99 = reader.Col(4).AsDouble ?? 0;
        long sampleCount = reader.GetInt64(5);

        LogBaselineComputed(metric, hours, sampleCount);

        return new BaselineResult(metric, hours, mean, stddev, p50, p95, p99, sampleCount);
    }

    // ==========================================================================
    // Period Comparison
    // ==========================================================================

    /// <summary>
    ///     Compares baseline statistics between two time periods, computing
    ///     the mean delta and percentage change.
    /// </summary>
    public async Task<PeriodComparisonResult> ComparePeriodAsync(
        string metric,
        DateTime period1Start,
        DateTime period1End,
        DateTime period2Start,
        DateTime period2End,
        string? service = null,
        CancellationToken ct = default)
    {
        string metricExpr = ValidateAndGetExpression(metric);

        BaselineResult period1 = await GetPeriodBaselineAsync(
            metric, metricExpr, period1Start, period1End, service, ct).ConfigureAwait(false);
        BaselineResult period2 = await GetPeriodBaselineAsync(
            metric, metricExpr, period2Start, period2End, service, ct).ConfigureAwait(false);

        double meanDelta = period2.Mean - period1.Mean;
        double meanDeltaPercent = period1.Mean != 0
            ? (meanDelta / period1.Mean) * 100.0
            : 0;

        LogPeriodComparison(metric, meanDelta, meanDeltaPercent);

        return new PeriodComparisonResult(metric, period1, period2, meanDelta, meanDeltaPercent);
    }

    // ==========================================================================
    // Private Helpers
    // ==========================================================================

    private async Task<BaselineResult> GetPeriodBaselineAsync(
        string metric,
        string metricExpr,
        DateTime periodStart,
        DateTime periodEnd,
        string? service,
        CancellationToken ct)
    {
        long startNano = DateTimeToUnixNano(periodStart);
        long endNano = DateTimeToUnixNano(periodEnd);
        int hours = (int)(periodEnd - periodStart).TotalHours;

        await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using DuckDBCommand cmd = lease.Connection.CreateCommand();

        string serviceFilter = service is not null ? "AND service_name = $3" : "";

        cmd.CommandText = $"""
                           WITH hourly AS (
                               SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                                      {metricExpr} AS metric_value
                               FROM spans
                               WHERE start_time_unix_nano >= $1 AND start_time_unix_nano <= $2
                               {serviceFilter}
                               GROUP BY bucket
                           )
                           SELECT AVG(metric_value) AS mean,
                                  STDDEV(metric_value) AS stddev,
                                  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY metric_value) AS p50,
                                  PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY metric_value) AS p95,
                                  PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY metric_value) AS p99,
                                  COUNT(*) AS sample_count
                           FROM hourly
                           """;

        cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new BaselineResult(metric, hours, 0, 0, 0, 0, 0, 0);
        }

        double mean = reader.Col(0).AsDouble ?? 0;
        double stddev = reader.Col(1).AsDouble ?? 0;
        double p50 = reader.Col(2).AsDouble ?? 0;
        double p95 = reader.Col(3).AsDouble ?? 0;
        double p99 = reader.Col(4).AsDouble ?? 0;
        long sampleCount = reader.GetInt64(5);

        return new BaselineResult(metric, hours, mean, stddev, p50, p95, p99, sampleCount);
    }

    private static string ValidateAndGetExpression(string metric)
    {
        if (!MetricExpressions.TryGetValue(metric, out string? expr))
        {
            throw new ArgumentException(
                $"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", MetricExpressions.Keys)}",
                nameof(metric));
        }

        return expr;
    }

    private static long ComputeCutoffNano(int hours)
    {
        DateTimeOffset cutoff = TimeProvider.System.GetUtcNow().AddHours(-hours);
        return cutoff.ToUnixTimeMilliseconds() * 1_000_000;
    }

    private static long DateTimeToUnixNano(DateTime dt)
    {
        DateTimeOffset dto = new(dt, TimeSpan.Zero);
        return dto.ToUnixTimeMilliseconds() * 1_000_000;
    }

    // ==========================================================================
    // Structured Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Anomaly detection for {Metric} over {Hours}h found {Count} anomalies")]
    private partial void LogAnomalyDetection(string metric, int hours, int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Baseline computed for {Metric} over {Hours}h with {SampleCount} samples")]
    private partial void LogBaselineComputed(string metric, int hours, long sampleCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Period comparison for {Metric}: delta={MeanDelta:F4}, percent={MeanDeltaPercent:F2}%")]
    private partial void LogPeriodComparison(string metric, double meanDelta, double meanDeltaPercent);
}

// =============================================================================
// Response Types
// =============================================================================

public sealed record AnomalyDetectionResult(
    string Metric,
    int Hours,
    double Sensitivity,
    double Mean,
    double StdDev,
    IReadOnlyList<AnomalyPoint> Anomalies);

public sealed record AnomalyPoint(
    DateTime Bucket,
    double Value,
    double ZScore,
    string Direction);

public sealed record BaselineResult(
    string Metric,
    int Hours,
    double Mean,
    double StdDev,
    double P50,
    double P95,
    double P99,
    long SampleCount);

public sealed record PeriodComparisonResult(
    string Metric,
    BaselineResult Period1,
    BaselineResult Period2,
    double MeanDelta,
    double MeanDeltaPercent);
