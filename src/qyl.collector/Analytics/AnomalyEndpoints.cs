using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Analytics;

/// <summary>
///     Minimal API endpoints for Z-score anomaly detection, baseline statistics,
///     and period comparison. Routes: <c>/api/v1/analytics/anomaly/*</c>
/// </summary>
internal static class AnomalyEndpoints
{
    public static WebApplication MapAnomalyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/analytics/anomaly")
            .WithTags("Anomaly Detection");

        group.MapGet("/anomalies", DetectAnomaliesAsync)
            .WithName("DetectAnomalies")
            .WithSummary("Detect Z-score anomalies in a metric over a time window");

        group.MapGet("/baseline", GetBaselineAsync)
            .WithName("GetBaseline")
            .WithSummary("Get baseline statistics for a metric");

        group.MapGet("/compare", ComparePeriodsAsync)
            .WithName("ComparePeriods")
            .WithSummary("Compare metric baselines between two time periods");

        return app;
    }

    private static async Task<IResult> DetectAnomaliesAsync(
        [FromServices] AnomalyService service,
        string metric,
        int? hours,
        double? sensitivity,
        string? serviceName,
        CancellationToken ct)
    {
        try
        {
            AnomalyDetectionResult result = await service.DetectAnomaliesAsync(
                metric,
                hours ?? 24,
                sensitivity ?? 2.0,
                serviceName,
                ct).ConfigureAwait(false);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["metric"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> GetBaselineAsync(
        [FromServices] AnomalyService service,
        string metric,
        int? hours,
        string? serviceName,
        CancellationToken ct)
    {
        try
        {
            BaselineResult result = await service.GetBaselineAsync(
                metric,
                hours ?? 24,
                serviceName,
                ct).ConfigureAwait(false);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["metric"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> ComparePeriodsAsync(
        [FromServices] AnomalyService service,
        string metric,
        DateTime period1Start,
        DateTime period1End,
        DateTime period2Start,
        DateTime period2End,
        string? serviceName,
        CancellationToken ct)
    {
        try
        {
            PeriodComparisonResult result = await service.ComparePeriodAsync(
                metric,
                period1Start,
                period1End,
                period2Start,
                period2End,
                serviceName,
                ct).ConfigureAwait(false);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["metric"] = [ex.Message]
            });
        }
    }
}
