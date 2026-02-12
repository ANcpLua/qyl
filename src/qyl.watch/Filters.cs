namespace qyl.watch;

/// <summary>
///     Applies runtime filters to incoming spans.
/// </summary>
internal static class Filters
{
    public static bool ShouldDisplay(SpanDto span, CliConfig config)
    {
        if (config.ErrorsOnly && !span.IsError && (span.HttpStatusCode is null || span.HttpStatusCode < 400))
            return false;

        if (config.SlowThresholdMs is { } threshold && span.DurationMs < threshold)
            return false;

        if (config.ServiceFilter is { } svc &&
            !string.Equals(span.ServiceName, svc, StringComparison.OrdinalIgnoreCase))
            return false;

        if (config.GenAiOnly && !span.IsGenAi)
            return false;

        return true;
    }
}
