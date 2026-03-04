using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using qyl.collector.Realtime;

namespace qyl.collector.Logs;

internal sealed class LogSummaryService(DuckDbStore store, TimeProvider timeProvider)
{
    private const int MaxRows = 10_000;
    private const int BatchSize = 500;
    private static readonly string[] SSeverityOrder = ["trace", "debug", "info", "warn", "error", "fatal"];

    private static readonly FrozenDictionary<string, TimeSpan> SWindowDurations =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
        {
            ["30s"] = TimeSpan.FromSeconds(30),
            ["1m"] = TimeSpan.FromMinutes(1),
            ["5m"] = TimeSpan.FromMinutes(5),
            ["15m"] = TimeSpan.FromMinutes(15),
            ["1h"] = TimeSpan.FromHours(1)
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SSuccessKeywords = new(
        @"\b(succeeded|successfully|success|resolved|connected|refreshed|recovered|completed)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Order matters: UUID before generic hex.
    private static readonly (Regex Pattern, string Replacement)[] SPatternReplacements =
    [
        (new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled), "<UUID>"),
        (new Regex(@"0x[0-9a-fA-F]+", RegexOptions.Compiled), "<HEX>"),
        (new Regex(@"\b\d{1,3}(?:\.\d{1,3}){3}(?::\d+)?\b", RegexOptions.Compiled), "<IP>"),
        (new Regex(@"(?<![a-zA-Z])\d+(?:\.\d+)?(?![a-zA-Z])", RegexOptions.Compiled), "<N>")
    ];

    private static readonly Regex SCollapsedNumbers = new(
        @"(<N>[,.\s]*)+",
        RegexOptions.Compiled);

    public static bool IsValidWindow(string window) => SWindowDurations.ContainsKey(window);

    public async Task<LogSummaryResponse> BuildSummaryAsync(
        string window,
        string? serviceName,
        string? sinceCursor,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var nowNano = TimeConversions.ToUnixNanoUnsigned(now);

        ulong after;
        if (!string.IsNullOrWhiteSpace(sinceCursor))
        {
            if (!LogCursor.TryDecode(sinceCursor, out after))
                throw new ArgumentException("Invalid cursor format.", nameof(sinceCursor));
        }
        else
        {
            var duration = SWindowDurations.GetValueOrDefault(window, TimeSpan.FromMinutes(5));
            after = TimeConversions.ToUnixNanoUnsigned(now - duration);
        }

        var logs = await FetchLogsAsync(after, null, serviceName, minSeverity, search, ct).ConfigureAwait(false);
        logs.Sort(static (a, b) => a.TimeUnixNano.CompareTo(b.TimeUnixNano));

        var totalCount = logs.Count;
        var errorCount = logs.Count(static l => l.SeverityNumber >= 17);
        var warningCount = logs.Count(static l => l.SeverityNumber is >= 13 and < 17);

        var (topIssues, resolvedPatterns) = BuildTopIssues(logs);
        var summary = BuildProse(window, serviceName, totalCount, errorCount, warningCount, topIssues, resolvedPatterns);
        var cursor = LogCursor.Encode(logs.Count > 0 ? logs[^1].TimeUnixNano : nowNano);

        return new LogSummaryResponse(
            window,
            now.UtcDateTime,
            cursor,
            summary,
            errorCount,
            warningCount,
            totalCount,
            topIssues);
    }

    public async Task<IReadOnlyList<LogPatternResponse>> BuildPatternsAsync(
        string window,
        string? serviceName,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int minCount,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var (after, before) = ResolveTimeRange(window, startTime, endTime);
        var effectiveMinSeverity = minSeverity ?? 17;
        var logs = await FetchLogsAsync(after, before, serviceName, effectiveMinSeverity, search, ct).ConfigureAwait(false);
        logs.Sort(static (a, b) => a.TimeUnixNano.CompareTo(b.TimeUnixNano));

        var groups = new Dictionary<string, PatternAccumulator>(StringComparer.Ordinal);
        foreach (var log in logs)
        {
            var service = log.ServiceName ?? "unknown";
            var body = log.Body ?? string.Empty;
            var template = ExtractPattern(body);
            var key = $"{service}\u001f{template}";
            var timestamp = TimeConversions.UnixNanoToDateTime(log.TimeUnixNano);
            var severity = LiveLogProjection.NormalizeSeverity(log.SeverityText, log.SeverityNumber);

            if (!groups.TryGetValue(key, out var accumulator))
            {
                accumulator = new PatternAccumulator(template, body, service, timestamp);
                groups[key] = accumulator;
            }

            accumulator.AddOccurrence(timestamp, severity);
        }

        return groups
            .Where(x => x.Value.Count >= minCount)
            .Select(static x => x.Value.ToResponse(x.Key))
            .OrderByDescending(static x => x.Count)
            .ThenByDescending(static x => x.LastSeen)
            .Take(50)
            .ToArray();
    }

    public async Task<LogStatsResponse> BuildStatsAsync(
        string window,
        string? serviceName,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var (after, before) = ResolveTimeRange(window, startTime, endTime);
        var logs = await FetchLogsAsync(after, before, serviceName, minSeverity, search, ct).ConfigureAwait(false);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trace"] = 0,
            ["debug"] = 0,
            ["info"] = 0,
            ["warn"] = 0,
            ["error"] = 0,
            ["fatal"] = 0
        };

        foreach (var log in logs)
        {
            var severity = LiveLogProjection.NormalizeSeverity(log.SeverityText, log.SeverityNumber);
            if (counts.TryGetValue(severity, out var current))
                counts[severity] = current + 1;
            else
                counts[severity] = 1;
        }

        DateTime? oldestTimestamp = null;
        DateTime? newestTimestamp = null;
        if (logs.Count > 0)
        {
            oldestTimestamp = TimeConversions.UnixNanoToDateTime(logs.Min(static l => l.TimeUnixNano));
            newestTimestamp = TimeConversions.UnixNanoToDateTime(logs.Max(static l => l.TimeUnixNano));
        }

        var bySeverity = SSeverityOrder
            .Select(x => new LogStatsSeverityCount(x, counts.GetValueOrDefault(x, 0)))
            .ToArray();

        var effectiveWindow = startTime.HasValue || endTime.HasValue ? "custom" : window;
        return new LogStatsResponse(
            effectiveWindow,
            now.UtcDateTime,
            logs.Count,
            oldestTimestamp,
            newestTimestamp,
            bySeverity);
    }

    public async Task<LogWaitResponse> WaitForLogAsync(LogWaitRequest request, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 300));
        var poll = TimeSpan.FromMilliseconds(Math.Clamp(request.PollIntervalMs, 100, 5000));

        var started = timeProvider.GetUtcNow();
        var deadline = started + timeout;
        var after = TimeConversions.ToUnixNanoUnsigned(started);
        var polls = 0;

        while (timeProvider.GetUtcNow() < deadline && !ct.IsCancellationRequested)
        {
            polls++;
            var rows = await store.GetLogsAsync(
                traceId: request.TraceId,
                severityText: request.SeverityText,
                minSeverity: request.MinSeverity,
                search: request.Search,
                after: after,
                serviceName: request.ServiceName,
                limit: BatchSize,
                ct: ct).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                var ordered = rows.OrderBy(static l => l.TimeUnixNano).ToArray();
                after = ordered[^1].TimeUnixNano;
                var first = ordered[0];
                var waitedMs = (long)(timeProvider.GetUtcNow() - started).TotalMilliseconds;

                return new LogWaitResponse(
                    true,
                    LiveLogProjection.ToDto(first),
                    waitedMs,
                    polls,
                    "Matching log found.");
            }

            await Task.Delay(poll, ct).ConfigureAwait(false);
        }

        return new LogWaitResponse(
            false,
            null,
            (long)(timeProvider.GetUtcNow() - started).TotalMilliseconds,
            polls,
            "Timeout waiting for matching log.");
    }

    private async Task<List<LogStorageRow>> FetchLogsAsync(
        ulong after,
        ulong? before,
        string? serviceName,
        int? minSeverity,
        string? search,
        CancellationToken ct)
    {
        var all = new List<LogStorageRow>(Math.Min(MaxRows, 2048));
        var upperBound = before;

        while (all.Count < MaxRows)
        {
            var batch = await store.GetLogsAsync(
                minSeverity: minSeverity,
                search: search,
                after: after,
                before: upperBound,
                serviceName: serviceName,
                limit: Math.Min(BatchSize, MaxRows - all.Count),
                ct: ct).ConfigureAwait(false);

            if (batch.Count is 0)
                break;

            all.AddRange(batch);
            var minSeen = batch.Min(static l => l.TimeUnixNano);
            if (minSeen <= after)
                break;

            upperBound = minSeen - 1;
            if (upperBound <= after)
                break;

            if (batch.Count < BatchSize)
                break;
        }

        return all;
    }

    private (ulong After, ulong? Before) ResolveTimeRange(
        string window,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        if (startTime.HasValue)
        {
            return (
                TimeConversions.ToUnixNanoUnsigned(startTime.Value),
                endTime.HasValue ? TimeConversions.ToUnixNanoUnsigned(endTime.Value) : null);
        }

        var duration = SWindowDurations.GetValueOrDefault(window, TimeSpan.FromMinutes(5));
        var now = timeProvider.GetUtcNow();
        return (
            TimeConversions.ToUnixNanoUnsigned(now - duration),
            endTime.HasValue ? TimeConversions.ToUnixNanoUnsigned(endTime.Value) : null);
    }

    private static (IReadOnlyList<LogSummaryIssue> Issues, FrozenSet<string> ResolvedPatterns)
        BuildTopIssues(IEnumerable<LogStorageRow> logs)
    {
        var groups = new Dictionary<string, IssueAccumulator>(StringComparer.Ordinal);
        var activeByService = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var resolvedPatterns = new HashSet<string>(StringComparer.Ordinal);

        foreach (var log in logs)
        {
            var service = log.ServiceName ?? "unknown";
            var message = log.Body ?? string.Empty;

            if (log.SeverityNumber >= 17)
            {
                var pattern = ExtractPattern(message);
                var key = $"{service}\u001f{pattern}";
                var timestamp = TimeConversions.UnixNanoToDateTime(log.TimeUnixNano);

                if (!groups.TryGetValue(key, out var acc))
                {
                    groups[key] = new IssueAccumulator(pattern, 1, timestamp, timestamp);
                }
                else
                {
                    groups[key] = acc with
                    {
                        Count = acc.Count + 1,
                        LastSeen = timestamp
                    };
                }

                if (!activeByService.TryGetValue(service, out var active))
                {
                    active = new HashSet<string>(StringComparer.Ordinal);
                    activeByService[service] = active;
                }

                active.Add(pattern);
                continue;
            }

            if (!SSuccessKeywords.IsMatch(message))
                continue;

            if (activeByService.TryGetValue(service, out var servicePatterns))
            {
                foreach (var pattern in servicePatterns)
                    resolvedPatterns.Add(pattern);
                servicePatterns.Clear();
            }
        }

        var issues = groups.Values
            .OrderByDescending(static x => x.Count)
            .Take(8)
            .Select(x => new LogSummaryIssue(
                x.Pattern,
                x.Count,
                x.FirstSeen,
                x.LastSeen,
                resolvedPatterns.Contains(x.Pattern)))
            .ToArray();

        return (issues, resolvedPatterns.ToFrozenSet(StringComparer.Ordinal));
    }

    private static string BuildProse(
        string window,
        string? serviceName,
        int totalCount,
        int errorCount,
        int warningCount,
        IReadOnlyList<LogSummaryIssue> topIssues,
        FrozenSet<string> resolvedPatterns)
    {
        if (totalCount is 0)
            return serviceName is null
                ? $"No log entries in the last {window}."
                : $"{serviceName} had no log entries in the last {window}.";

        var subject = serviceName ?? "The system";
        var parts = new List<string>
        {
            $"In the last {window}, {subject} logged {totalCount} entries."
        };

        if (errorCount > 0)
        {
            var unresolved = topIssues.Where(static i => !i.Resolved).Take(3)
                .Select(static i => $"{i.Pattern} ({i.Count}x)").ToArray();
            var resolved = topIssues.Where(static i => i.Resolved).Take(3)
                .Select(static i => i.Pattern).ToArray();

            if (unresolved.Length > 0)
                parts.Add($"{errorCount} error(s): {string.Join(", ", unresolved)}.");
            else
                parts.Add($"{errorCount} error(s) occurred.");

            if (resolved.Length > 0)
                parts.Add($"{resolved.Length} issue type(s) appear resolved: {string.Join(", ", resolved)}.");
        }
        else
        {
            parts.Add("No errors detected.");
        }

        if (warningCount > 0)
            parts.Add($"{warningCount} warning(s) detected.");

        if (resolvedPatterns.Count > 0 && errorCount is 0)
            parts.Add("Recent success signals indicate recovery from previous failures.");

        return string.Join(" ", parts);
    }

    private static string ExtractPattern(string message)
    {
        var result = message;
        foreach (var (pattern, replacement) in SPatternReplacements)
            result = pattern.Replace(result, replacement);

        result = SCollapsedNumbers.Replace(result, "<N> ");
        return result.Trim();
    }

    private sealed record IssueAccumulator(string Pattern, int Count, DateTime FirstSeen, DateTime LastSeen);

    private sealed class PatternAccumulator(
        string template,
        string sample,
        string serviceName,
        DateTime firstSeen)
    {
        public string Template { get; } = template;
        public string Sample { get; } = sample;
        public string ServiceName { get; } = serviceName;
        public int Count { get; set; }
        public DateTime FirstSeen { get; } = firstSeen;
        public DateTime LastSeen { get; set; } = firstSeen;
        public Dictionary<string, int> SeverityCounts { get; } = new(StringComparer.Ordinal);
        public List<DateTime> ObservedAt { get; } = [];

        public void AddOccurrence(DateTime observedAt, string severity)
        {
            Count++;
            LastSeen = observedAt;
            ObservedAt.Add(observedAt);
            if (SeverityCounts.TryGetValue(severity, out var existing))
                SeverityCounts[severity] = existing + 1;
            else
                SeverityCounts[severity] = 1;
        }

        public LogPatternResponse ToResponse(string key)
        {
            var severityDistribution = SeverityCounts
                .OrderByDescending(static x => x.Value)
                .Select(static x => new LogPatternSeverityCount(x.Key, x.Value))
                .ToArray();

            return new LogPatternResponse(
                CreatePatternId(key),
                Template,
                Sample,
                Count,
                FirstSeen,
                LastSeen,
                ServiceName,
                ComputeTrend(ObservedAt),
                severityDistribution);
        }

        private static string ComputeTrend(List<DateTime> observedAt)
        {
            if (observedAt.Count <= 1)
                return "new";

            var first = observedAt[0];
            var last = observedAt[^1];
            if ((last - first) <= TimeSpan.FromMinutes(1) && observedAt.Count >= 5)
                return "spike";

            var midpoint = first + TimeSpan.FromTicks((last - first).Ticks / 2);
            var firstHalf = observedAt.Count(x => x <= midpoint);
            var secondHalf = observedAt.Count - firstHalf;

            if (firstHalf is 0 && secondHalf > 0)
                return "increasing";
            if (secondHalf >= firstHalf * 2)
                return "increasing";
            if (secondHalf * 2 <= firstHalf)
                return "decreasing";
            return "stable";
        }

        private static string CreatePatternId(string key)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexStringLower(hash[..8]);
        }
    }
}

internal sealed record LogSummaryResponse(
    string Window,
    DateTime GeneratedAt,
    string Cursor,
    string Summary,
    int ErrorCount,
    int WarningCount,
    int TotalCount,
    IReadOnlyList<LogSummaryIssue> TopIssues
);

internal sealed record LogSummaryIssue(
    string Pattern,
    int Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    bool Resolved
);

internal sealed record LogPatternResponse(
    string PatternId,
    string Template,
    string Sample,
    int Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    string ServiceName,
    string Trend,
    IReadOnlyList<LogPatternSeverityCount> SeverityDistribution
);

internal sealed record LogPatternSeverityCount(
    string Severity,
    int Count
);

internal sealed record LogStatsResponse(
    string Window,
    DateTime GeneratedAt,
    int TotalCount,
    DateTime? OldestTimestamp,
    DateTime? NewestTimestamp,
    IReadOnlyList<LogStatsSeverityCount> BySeverity
);

internal sealed record LogStatsSeverityCount(
    string Severity,
    int Count
);

internal sealed record LogWaitRequest(
    string? ServiceName = null,
    string? TraceId = null,
    string? SeverityText = null,
    int? MinSeverity = null,
    string? Search = null,
    int TimeoutSeconds = 30,
    int PollIntervalMs = 1000
);

internal sealed record LogWaitResponse(
    bool Matched,
    LiveLogDto? Log,
    long WaitedMs,
    int PollCount,
    string Message
);
