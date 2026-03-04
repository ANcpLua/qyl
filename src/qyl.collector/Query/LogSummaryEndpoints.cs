namespace qyl.collector.Logs;

internal static class LogSummaryEndpoints
{
    public static WebApplication MapLogSummaryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/logs")
            .WithTags("Logs");

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetLogSummary")
            .WithSummary("Get an LLM-optimized summary of recent log activity.");

        group.MapGet("/patterns", GetPatternsAsync)
            .WithName("GetLogPatterns")
            .WithSummary("Get grouped log message patterns.");

        group.MapPost("/wait", WaitForLogAsync)
            .WithName("WaitForLog")
            .WithSummary("Wait until a matching log entry appears (future logs only).");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        LogSummaryService service,
        string? window,
        string? serviceName,
        string? sinceCursor,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var selectedWindow = string.IsNullOrWhiteSpace(window) ? "5m" : window;
        if (!LogSummaryService.IsValidWindow(selectedWindow))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["window"] = ["Window must be one of: 30s, 1m, 5m, 15m, 1h."]
            });
        }

        try
        {
            var summary = await service.BuildSummaryAsync(
                selectedWindow,
                serviceName,
                sinceCursor,
                minSeverity,
                search,
                ct).ConfigureAwait(false);

            return Results.Ok(summary);
        }
        catch (ArgumentException ex) when (ex.ParamName is "sinceCursor")
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sinceCursor"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> GetPatternsAsync(
        LogSummaryService service,
        string? window,
        string? serviceName,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? minCount,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var selectedWindow = string.IsNullOrWhiteSpace(window) ? "5m" : window;
        if (startTime is null && !LogSummaryService.IsValidWindow(selectedWindow))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["window"] = ["Window must be one of: 30s, 1m, 5m, 15m, 1h."]
            });
        }

        var selectedMinCount = minCount ?? 2;
        if (selectedMinCount < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["minCount"] = ["Minimum count must be greater than or equal to 1."]
            });
        }

        if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["endTime"] = ["endTime must be greater than startTime."]
            });
        }

        var patterns = await service.BuildPatternsAsync(
            selectedWindow,
            serviceName,
            startTime,
            endTime,
            selectedMinCount,
            minSeverity,
            search,
            ct).ConfigureAwait(false);

        return Results.Ok(patterns);
    }

    private static async Task<IResult> WaitForLogAsync(
        LogSummaryService service,
        LogWaitRequest request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.SeverityText))
        {
            var normalized = request.SeverityText.Trim().ToLowerInvariant();
            if (normalized is not ("trace" or "debug" or "info" or "warn" or "error" or "fatal" or "warning"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["severityText"] = ["Severity must be one of: trace, debug, info, warn, warning, error, fatal."]
                });
            }

            request = request with
            {
                SeverityText = normalized is "warning" ? "warn" : normalized
            };
        }

        var response = await service.WaitForLogAsync(request, ct).ConfigureAwait(false);
        return Results.Ok(response);
    }
}
