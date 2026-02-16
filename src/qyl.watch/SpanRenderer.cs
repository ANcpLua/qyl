using Spectre.Console;

namespace qyl.watch;

/// <summary>
///     Renders individual spans to the terminal with color coding and tree indentation.
/// </summary>
internal static class SpanRenderer
{
    public static void Render(IReadOnlyList<SpanDto> spans, CliConfig config)
    {
        // Group by trace for tree rendering
        var byTrace = new Dictionary<string, List<SpanDto>>(StringComparer.Ordinal);
        foreach (var span in spans)
        {
            if (!Filters.ShouldDisplay(span, config))
                continue;

            var key = span.TraceId ?? "unknown";
            if (!byTrace.TryGetValue(key, out var list))
            {
                list = [];
                byTrace[key] = list;
            }

            list.Add(span);
        }

        foreach (var (_, traceSpans) in byTrace)
        {
            // Sort by start time
            traceSpans.Sort((a, b) => a.StartTimeUnixNano.CompareTo(b.StartTimeUnixNano));

            // Build parent lookup
            var childMap = new Dictionary<string, List<SpanDto>>(StringComparer.Ordinal);
            var roots = new List<SpanDto>();

            foreach (var s in traceSpans)
            {
                if (s.ParentSpanId is not { } parentId || !traceSpans.Exists(p => p.SpanId == parentId))
                {
                    roots.Add(s);
                }
                else
                {
                    if (!childMap.TryGetValue(parentId, out var children))
                    {
                        children = [];
                        childMap[parentId] = children;
                    }

                    children.Add(s);
                }
            }

            foreach (var root in roots)
            {
                RenderSpan(root, "", true, childMap);
            }
        }
    }

    private static void RenderSpan(
        SpanDto span,
        string prefix,
        bool isLast,
        IReadOnlyDictionary<string, List<SpanDto>> childMap)
    {
        var connector = prefix.Length == 0 ? "" : isLast ? " [grey]\\-[/] " : " [grey]|-[/] ";
        var line = FormatSpanLine(span);
        AnsiConsole.MarkupLine($"{prefix}{connector}{line}");

        if (span.SpanId is not null && childMap.TryGetValue(span.SpanId, out var children))
        {
            var childPrefix = prefix + (prefix.Length == 0 ? "" : isLast ? "   " : " [grey]|[/]  ");
            for (var i = 0; i < children.Count; i++)
            {
                RenderSpan(children[i], childPrefix, i == children.Count - 1, childMap);
            }
        }
    }

    private static string FormatSpanLine(SpanDto span)
    {
        var timestamp = FormatTimestamp(span.StartTimeUnixNano);
        var duration = FormatDuration(span.DurationMs);
        var durationColor = GetDurationColor(span);
        var statusColor = GetStatusColor(span);
        var service = span.ServiceName ?? "unknown";

        // GenAI span
        if (span.IsGenAi)
        {
            var model = span.GenAiResponseModel ?? span.GenAiRequestModel ?? "?";
            var tokens = "";
            if (span.GenAiInputTokens is not null || span.GenAiOutputTokens is not null)
            {
                var input = span.GenAiInputTokens?.ToString() ?? "?";
                var output = span.GenAiOutputTokens?.ToString() ?? "?";
                tokens = $" [grey]({input}/{output} tokens)[/]";
            }

            var cost = span.GenAiCostUsd is { } c ? $" [yellow]${c:F4}[/]" : "";
            var tool = span.GenAiToolName is { } t ? $" [blue]{Markup.Escape(t)}[/]" : "";

            return $"[grey]{timestamp}[/] [{statusColor}]{Markup.Escape(span.Name ?? "genai")}[/] " +
                   $"[magenta]{Markup.Escape(model)}[/]{tokens}{cost}{tool} " +
                   $"[{durationColor}]{duration}[/] [dim]{Markup.Escape(service)}[/]";
        }

        // Database span
        if (span.DbSystem is not null)
        {
            var op = span.DbOperation ?? "query";
            return $"[grey]{timestamp}[/] [{statusColor}]{Markup.Escape(span.DbSystem)}:{Markup.Escape(op)}[/] " +
                   $"[{durationColor}]{duration}[/] [dim]{Markup.Escape(service)}[/]";
        }

        // HTTP span
        if (span.HttpMethod is not null)
        {
            var method = span.HttpMethod;
            var path = span.HttpRoute ?? span.Name ?? "/";
            var statusCode = span.HttpStatusCode?.ToString() ?? "";
            var scColor = span.HttpStatusCode switch
            {
                >= 500 => "red",
                >= 400 => "yellow",
                _ => "green"
            };

            return $"[grey]{timestamp}[/] [{statusColor}]{Markup.Escape(method)}[/] " +
                   $"{Markup.Escape(path)} [{scColor}]{statusCode}[/] " +
                   $"[{durationColor}]{duration}[/] [dim]{Markup.Escape(service)}[/]";
        }

        // Generic span
        return $"[grey]{timestamp}[/] [{statusColor}]{Markup.Escape(span.Name ?? "?")}[/] " +
               $"[{durationColor}]{duration}[/] [dim]{Markup.Escape(service)}[/]";
    }

    private static string FormatTimestamp(ulong nanos)
    {
        var ticks = (long)(nanos / 100);
        var dto = new DateTimeOffset(ticks, TimeSpan.Zero);
        return dto.ToLocalTime().ToString("HH:mm:ss.fff");
    }

    private static string FormatDuration(double ms) => ms switch
    {
        >= 1000 => $"{ms / 1000:F2}s",
        >= 1 => $"{ms:F0}ms",
        _ => $"{ms:F2}ms"
    };

    private static string GetDurationColor(SpanDto span) => span.DurationMs switch
    {
        _ when span.IsError => "red",
        > 500 => "red",
        > 200 => "yellow",
        _ => "green"
    };

    private static string GetStatusColor(SpanDto span)
    {
        if (span.IsError) return "red";
        if (span.HttpStatusCode is >= 500) return "red";
        if (span.HttpStatusCode is >= 400) return "yellow";
        if (span.DurationMs > 200) return "yellow";
        return "green";
    }
}
