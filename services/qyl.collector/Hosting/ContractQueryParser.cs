using Microsoft.Extensions.Primitives;

namespace Qyl.Collector.Hosting;

internal readonly record struct ParsedSessionsParameters(
    bool? IsActive,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    int? Limit);

internal readonly record struct ParsedSessionStatsParameters(
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime);

internal readonly record struct ParsedTracesParameters(
    int? Limit,
    string? Cursor);

internal readonly record struct ParsedLogsParameters(
    int? SeverityMin,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    int? Limit);

internal readonly record struct ParsedMetricsParameters(
    string? NamePrefix,
    int? Limit);

internal readonly record struct ParsedMetricSeriesParameters(
    IReadOnlyList<MetricAttributeMatcher> Matchers,
    int? Limit);

internal readonly record struct ParsedMetricQueryParameters(
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    long? StepMs,
    MetricAggregation Aggregation,
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<MetricAttributeMatcher> Matchers,
    int? SeriesLimit);

internal static class ContractQueryParser
{
    // Metric query parameters carry their OpenAPI wire names verbatim: the contract is
    // what an agent or the dashboard codes against, and a name that only the collector
    // knows is a name no generated client can send.
    internal static IResult? ParseMetrics(HttpRequest request, out ParsedMetricsParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadString("name_prefix", out var namePrefix) is { } prefixError) return prefixError;
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;

        parsed = new ParsedMetricsParameters(namePrefix, limit);
        return null;
    }

    internal static IResult? ParseMetricSeries(HttpRequest request, out ParsedMetricSeriesParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (ReadMatchers(reader, out var matchers) is { } matcherError) return matcherError;
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;

        parsed = new ParsedMetricSeriesParameters(matchers, limit);
        return null;
    }

    internal static IResult? ParseMetricQuery(HttpRequest request, out ParsedMetricQueryParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadDateTime("start_time", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("end_time", out var endTime) is { } endError) return endError;
        if (reader.ReadInt64("step_ms", out var stepMs) is { } stepError) return stepError;
        if (reader.ReadStringList("group_by", out var groupBy) is { } groupError) return groupError;
        if (ReadMatchers(reader, out var matchers) is { } matcherError) return matcherError;
        if (reader.ReadInteger("series_limit", out var seriesLimit) is { } seriesError) return seriesError;

        var aggregation = MetricAggregation.Avg;
        if (reader.ReadString("aggregation", out var rawAggregation) is { } aggregationError)
            return aggregationError;
        if (rawAggregation is not null && !TryParseAggregation(rawAggregation, out aggregation))
        {
            return Invalid(
                "aggregation",
                "Value must be one of avg, min, max, sum, count, last, p50, p90, p95, p99.",
                "aggregation.invalid",
                rawAggregation);
        }

        parsed = new ParsedMetricQueryParameters(
            startTime,
            endTime,
            stepMs,
            aggregation,
            groupBy,
            matchers,
            seriesLimit);
        return null;
    }

    // The contract spells a matcher `key=value`, splitting on the first '=' so a value may
    // itself contain one. An entry with no '=' selects nothing knowable, so it is rejected
    // rather than silently ignored.
    private static IResult? ReadMatchers(QueryReader reader, out IReadOnlyList<MetricAttributeMatcher> matchers)
    {
        matchers = [];
        if (reader.ReadStringList("attr", out var exact) is { } exactError) return exactError;
        if (reader.ReadStringList("attr_prefix", out var prefixed) is { } prefixError) return prefixError;

        var parsed = new List<MetricAttributeMatcher>(exact.Count + prefixed.Count);
        foreach (var (name, values, isPrefix) in
                 new[] { ("attr", exact, false), ("attr_prefix", prefixed, true) })
        {
            foreach (var value in values)
            {
                var separator = value.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    return Invalid(
                        name,
                        "Value must be 'key=value' with a non-empty key.",
                        "matcher.invalid",
                        value);
                }

                parsed.Add(new MetricAttributeMatcher(
                    value[..separator],
                    value[(separator + 1)..],
                    isPrefix));
            }
        }

        matchers = parsed;
        return null;
    }

    private static bool TryParseAggregation(string raw, out MetricAggregation aggregation) =>
        Enum.TryParse(raw, ignoreCase: true, out aggregation) && Enum.IsDefined(aggregation);

    internal static IResult? ParseSessions(HttpRequest request, out ParsedSessionsParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadBoolean("isActive", out var isActive) is { } error) return error;
        if (reader.ReadDateTime("startTime", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("endTime", out var endTime) is { } endError) return endError;
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;

        parsed = new ParsedSessionsParameters(isActive, startTime, endTime, limit);
        return null;
    }

    internal static IResult? ParseSessionStats(HttpRequest request, out ParsedSessionStatsParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadDateTime("startTime", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("endTime", out var endTime) is { } endError) return endError;

        parsed = new ParsedSessionStatsParameters(startTime, endTime);
        return null;
    }

    internal static IResult? ParseTraces(HttpRequest request, out ParsedTracesParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;
        if (reader.ReadString("cursor", out var cursor) is { } cursorError) return cursorError;

        parsed = new ParsedTracesParameters(limit, cursor);
        return null;
    }

    internal static IResult? ParseLogs(HttpRequest request, out ParsedLogsParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadInteger("severityMin", out var severityMin) is { } severityError) return severityError;
        if (reader.ReadDateTime("startTime", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("endTime", out var endTime) is { } endError) return endError;
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;

        parsed = new ParsedLogsParameters(severityMin, startTime, endTime, limit);
        return null;
    }

    internal static IResult? ParseLogStream(HttpRequest request, out int? minSeverity) =>
        new QueryReader(request.Query).ReadInteger("minSeverity", out minSeverity);

    private readonly struct QueryReader(IQueryCollection query)
    {
        public IResult? ReadInteger(string name, out int? value)
        {
            value = null;
            if (!TryReadSingle(name, out var raw, out var rejectedValue)) return null;
            if (raw is not null && int.TryParse(
                    raw,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                value = parsed;
                return null;
            }

            return Invalid(
                name,
                "Value must be a single 32-bit integer.",
                "query.invalid_integer",
                rejectedValue);
        }

        public IResult? ReadInt64(string name, out long? value)
        {
            value = null;
            if (!TryReadSingle(name, out var raw, out var rejectedValue)) return null;
            if (raw is not null && long.TryParse(
                    raw,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                value = parsed;
                return null;
            }

            return Invalid(
                name,
                "Value must be a single 64-bit integer.",
                "query.invalid_integer",
                rejectedValue);
        }

        // Repeatable parameters are the one place multiple values are legal, so they bypass
        // TryReadSingle rather than being rejected by it.
        public IResult? ReadStringList(string name, out IReadOnlyList<string> values)
        {
            values = [];
            if (!query.TryGetValue(name, out var raw)) return null;

            var parsed = new List<string>(raw.Count);
            foreach (var value in raw)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return Invalid(
                        name,
                        "Each value must be a non-empty string.",
                        "query.invalid_string",
                        JoinValues(raw));
                }

                parsed.Add(value);
            }

            values = parsed;
            return null;
        }

        public IResult? ReadBoolean(string name, out bool? value)
        {
            value = null;
            if (!TryReadSingle(name, out var raw, out var rejectedValue)) return null;
            if (raw is not null && bool.TryParse(raw, out var parsed))
            {
                value = parsed;
                return null;
            }

            return Invalid(
                name,
                "Value must be a single boolean ('true' or 'false').",
                "query.invalid_boolean",
                rejectedValue);
        }

        public IResult? ReadString(string name, out string? value)
        {
            value = null;
            if (!TryReadSingle(name, out var raw, out var rejectedValue)) return null;
            if (raw is not null)
            {
                value = raw;
                return null;
            }

            return Invalid(
                name,
                "Value must be a single non-empty string.",
                "query.invalid_string",
                rejectedValue);
        }

        public IResult? ReadDateTime(string name, out DateTimeOffset? value)
        {
            value = null;
            if (!TryReadSingle(name, out var raw, out var rejectedValue)) return null;
            if (raw is not null && TryParseRfc3339(raw, out var parsed))
            {
                value = parsed;
                return null;
            }

            return Invalid(
                name,
                "Value must be a single RFC 3339 date-time with an explicit UTC offset.",
                "query.invalid_date_time",
                rejectedValue);
        }

        private bool TryReadSingle(string name, out string? value, out string? rejectedValue)
        {
            value = null;
            rejectedValue = null;
            if (!query.TryGetValue(name, out var values)) return false;

            rejectedValue = JoinValues(values);
            if (values.Count == 1 && !string.IsNullOrEmpty(values[0])) value = values[0];
            return true;
        }

        private static string JoinValues(StringValues values) =>
            string.Join(',', values.ToArray());
    }

    private static bool TryParseRfc3339(string value, out DateTimeOffset parsed)
    {
        parsed = default;
        if (value.Length < 20 || value.AsSpan().Trim().Length != value.Length ||
            value[4] != '-' || value[7] != '-' ||
            value[10] is not ('T' or 't') ||
            value[13] != ':' || value[16] != ':')
        {
            return false;
        }

        var hasUtcDesignator = value[^1] is 'Z' or 'z';
        var hasNumericOffset = value.Length >= 25 &&
                               value[^6] is '+' or '-' &&
                               value[^3] == ':' &&
                               char.IsAsciiDigit(value[^5]) &&
                               char.IsAsciiDigit(value[^4]) &&
                               char.IsAsciiDigit(value[^2]) &&
                               char.IsAsciiDigit(value[^1]);
        return (hasUtcDesignator || hasNumericOffset) &&
               DateTimeOffset.TryParse(
                   value,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out parsed) &&
               QylTimeConversions.TryToUnixNanoUnsigned(parsed.ToUniversalTime(), out _);
    }

    private static IResult Invalid(string field, string message, string code, string? rejectedValue) =>
        ContractErrorResults.Validation(field, message, code, rejectedValue);
}
