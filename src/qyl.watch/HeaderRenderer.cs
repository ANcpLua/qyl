using Spectre.Console;

namespace qyl.watch;

/// <summary>
///     Renders the top header bar with live statistics.
///     Tracks rolling 10-second windows for throughput and latency.
/// </summary>
internal sealed class HeaderRenderer
{
    private readonly Lock _lock = new();
    private readonly List<(DateTimeOffset Time, double DurationMs, bool IsError)> _recentSpans = [];
    private readonly Dictionary<string, int> _services = new(StringComparer.OrdinalIgnoreCase);

    public void RecordSpan(SpanDto span)
    {
        lock (_lock)
        {
            var now = TimeProvider.System.GetUtcNow();
            _recentSpans.Add((now, span.DurationMs, span.IsError));

            if (span.ServiceName is { } svc)
            {
                if (_services.TryGetValue(svc, out var count))
                    _services[svc] = count + 1;
                else
                    _services[svc] = 1;
            }

            // Evict entries older than 10 seconds
            _recentSpans.RemoveAll(s => (now - s.Time).TotalSeconds > 10);
        }
    }

    public void Render()
    {
        lock (_lock)
        {
            var now = TimeProvider.System.GetUtcNow();
            _recentSpans.RemoveAll(s => (now - s.Time).TotalSeconds > 10);

            var total = _recentSpans.Count;
            var errors = 0;
            var durations = new List<double>(total);

            foreach (var (_, duration, isError) in _recentSpans)
            {
                durations.Add(duration);
                if (isError) errors++;
            }

            var rps = total > 0 ? total / 10.0 : 0;
            var errorRate = total > 0 ? errors * 100.0 / total : 0;

            double p95 = 0;
            if (durations.Count > 0)
            {
                durations.Sort();
                var idx = (int)Math.Ceiling(durations.Count * 0.95) - 1;
                p95 = durations[Math.Max(0, idx)];
            }

            var serviceText = _services.Count == 0
                ? "[grey]no services[/]"
                : string.Join(" ", _services.Select(kvp => $"[cyan]{Markup.Escape(kvp.Key)}[/]([grey]{kvp.Value}[/])"));

            var rpsColor = rps > 50 ? "green" : rps > 10 ? "yellow" : "grey";
            var errColor = errorRate > 5 ? "red" : errorRate > 0 ? "yellow" : "green";
            var p95Color = p95 > 500 ? "red" : p95 > 200 ? "yellow" : "green";

            AnsiConsole.MarkupLine(
                $"[bold]Services:[/] {serviceText} | " +
                $"[{rpsColor}]{rps:F1} req/s[/] | " +
                $"[{errColor}]{errorRate:F1}% errors[/] | " +
                $"[{p95Color}]p95 {p95:F0}ms[/]");
        }
    }

    public IReadOnlyList<string> GetServiceNames()
    {
        lock (_lock)
        {
            return [.. _services.Keys.Order()];
        }
    }
}
