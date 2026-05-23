using ANcpLua.Roslyn.Utilities.Time;
using Qyl.Collector.Metrics;

namespace Qyl.Collector.Analytics;

[QylService(QylLifetime.Singleton)]
public sealed partial class AnomalyService(DuckDbStore store, ILogger<AnomalyService> logger)
{
    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        string metric,
        int hours = 24,
        double sensitivity = 2.0,
        string? service = null,
        CancellationToken ct = default)
    {
        var metricSelection = ValidateAndGetMetric(metric);
        var cutoffNano = ComputeCutoffNano(hours);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var metricFilter = BuildMetricPredicate(metricSelection.Predicate);
        var serviceFilter = service is not null ? "AND service_name = $3" : "";

        cmd.CommandText = "WITH hourly AS ("
                          + " SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,"
                          + " " + metricSelection.Expression + " AS metric_value"
                          + " FROM spans WHERE start_time_unix_nano >= $1 " + metricFilter + " " + serviceFilter
                          + " GROUP BY bucket"
                          + "), stats AS (SELECT AVG(metric_value) AS mean, STDDEV(metric_value) AS stddev FROM hourly)"
                          + " SELECT h.bucket, h.metric_value,"
                          + " (h.metric_value - s.mean) / NULLIF(s.stddev, 0) AS z_score, s.mean, s.stddev"
                          + " FROM hourly h, stats s"
                          + " WHERE ABS((h.metric_value - s.mean) / NULLIF(s.stddev, 0)) > $2"
                          + " ORDER BY h.bucket DESC";

        cmd.Parameters.Add(new DuckDBParameter { Value = cutoffNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = sensitivity });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        List<AnomalyPoint> anomalies = [];
        double mean = 0;
        double stddev = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var bucket = reader.GetDateTime(0);
            var value = reader.GetDouble(1);
            var zScore = reader.Col(2).AsDouble ?? 0;
            mean = reader.Col(3).AsDouble ?? 0;
            stddev = reader.Col(4).AsDouble ?? 0;

            var direction = zScore > 0 ? "spike" : "drop";
            anomalies.Add(new AnomalyPoint(bucket, value, zScore, direction));
        }

        LogAnomalyDetection(metric, hours, anomalies.Count);

        return new AnomalyDetectionResult(metric, hours, sensitivity, mean, stddev, anomalies);
    }


    public async Task<BaselineResult> GetBaselineAsync(
        string metric,
        int hours = 24,
        string? service = null,
        CancellationToken ct = default)
    {
        var metricSelection = ValidateAndGetMetric(metric);
        var cutoffNano = ComputeCutoffNano(hours);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var metricFilter = BuildMetricPredicate(metricSelection.Predicate);
        var serviceFilter = service is not null ? "AND service_name = $2" : "";

        cmd.CommandText = "WITH hourly AS ("
                          + " SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,"
                          + " " + metricSelection.Expression + " AS metric_value"
                          + " FROM spans WHERE start_time_unix_nano >= $1 " + metricFilter + " " + serviceFilter
                          + " GROUP BY bucket)"
                          + " SELECT AVG(metric_value) AS mean, STDDEV(metric_value) AS stddev,"
                          + " PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY metric_value) AS p50,"
                          + " PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY metric_value) AS p95,"
                          + " PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY metric_value) AS p99,"
                          + " COUNT(*) AS sample_count FROM hourly";

        cmd.Parameters.Add(new DuckDBParameter { Value = cutoffNano });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new BaselineResult(metric, hours, 0, 0, 0, 0, 0, 0);
        }

        var mean = reader.Col(0).AsDouble ?? 0;
        var stddev = reader.Col(1).AsDouble ?? 0;
        var p50 = reader.Col(2).AsDouble ?? 0;
        var p95 = reader.Col(3).AsDouble ?? 0;
        var p99 = reader.Col(4).AsDouble ?? 0;
        var sampleCount = reader.GetInt64(5);

        LogBaselineComputed(metric, hours, sampleCount);

        return new BaselineResult(metric, hours, mean, stddev, p50, p95, p99, sampleCount);
    }


    public async Task<PeriodComparisonResult> ComparePeriodAsync(
        string metric,
        DateTime period1Start,
        DateTime period1End,
        DateTime period2Start,
        DateTime period2End,
        string? service = null,
        CancellationToken ct = default)
    {
        var metricSelection = ValidateAndGetMetric(metric);

        var period1 = await GetPeriodBaselineAsync(
            metric, metricSelection, period1Start, period1End, service, ct).ConfigureAwait(false);
        var period2 = await GetPeriodBaselineAsync(
            metric, metricSelection, period2Start, period2End, service, ct).ConfigureAwait(false);

        var meanDelta = period2.Mean - period1.Mean;
        var meanDeltaPercent = period1.Mean is not 0
            ? meanDelta / period1.Mean * 100.0
            : 0;

        LogPeriodComparison(metric, meanDelta, meanDeltaPercent);

        return new PeriodComparisonResult(metric, period1, period2, meanDelta, meanDeltaPercent);
    }


    private async Task<BaselineResult> GetPeriodBaselineAsync(
        string metric,
        AnomalyMetricSelection metricSelection,
        DateTime periodStart,
        DateTime periodEnd,
        string? service,
        CancellationToken ct)
    {
        var startNano = DateTimeToUnixNano(periodStart);
        var endNano = DateTimeToUnixNano(periodEnd);
        var hours = (int)(periodEnd - periodStart).TotalHours;

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var metricFilter = BuildMetricPredicate(metricSelection.Predicate);
        var serviceFilter = service is not null ? "AND service_name = $3" : "";

        cmd.CommandText = "WITH hourly AS ("
                          + " SELECT time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,"
                          + " " + metricSelection.Expression + " AS metric_value"
                          + " FROM spans WHERE start_time_unix_nano >= $1 AND start_time_unix_nano <= $2 " +
                          metricFilter + " " +
                          serviceFilter
                          + " GROUP BY bucket)"
                          + " SELECT AVG(metric_value) AS mean, STDDEV(metric_value) AS stddev,"
                          + " PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY metric_value) AS p50,"
                          + " PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY metric_value) AS p95,"
                          + " PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY metric_value) AS p99,"
                          + " COUNT(*) AS sample_count FROM hourly";

        cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
        if (service is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = service });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new BaselineResult(metric, hours, 0, 0, 0, 0, 0, 0);
        }

        var mean = reader.Col(0).AsDouble ?? 0;
        var stddev = reader.Col(1).AsDouble ?? 0;
        var p50 = reader.Col(2).AsDouble ?? 0;
        var p95 = reader.Col(3).AsDouble ?? 0;
        var p99 = reader.Col(4).AsDouble ?? 0;
        var sampleCount = reader.GetInt64(5);

        return new BaselineResult(metric, hours, mean, stddev, p50, p95, p99, sampleCount);
    }

    private static AnomalyMetricSelection ValidateAndGetMetric(string metric)
    {
        if (!DerivedMetricCatalog.TryGetAnomalyMetric(metric, out var selection))
        {
            throw new ArgumentException(
                $"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", DerivedMetricCatalog.GetAnomalyMetricNames().Order(StringComparer.Ordinal))}",
                nameof(metric));
        }

        return selection;
    }

    private static string BuildMetricPredicate(string? predicate) =>
        predicate is null ? string.Empty : $"AND ({predicate})";

    private static long ComputeCutoffNano(int hours) =>
        TimeConversions.ToUnixNano(
            TimeProvider.System.GetUtcNow().AddHours(-hours));

    private static long DateTimeToUnixNano(DateTime dt) =>
        TimeConversions.ToUnixNano(new DateTimeOffset(dt, TimeSpan.Zero));


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
