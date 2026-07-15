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

internal readonly record struct ParsedGenAiEtlAuditParameters(
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    int? Limit);

internal readonly record struct ParsedGenAiEtlAuditPeriod(
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime);

internal static class ContractQueryParser
{
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

    internal static IResult? ParseGenAiEtlAudit(
        HttpRequest request,
        out ParsedGenAiEtlAuditParameters parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadDateTime("startTime", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("endTime", out var endTime) is { } endError) return endError;
        if (reader.ReadInteger("limit", out var limit) is { } limitError) return limitError;

        parsed = new ParsedGenAiEtlAuditParameters(startTime, endTime, limit);
        return null;
    }

    internal static IResult? ParseGenAiEtlAuditPeriod(
        HttpRequest request,
        out ParsedGenAiEtlAuditPeriod parsed)
    {
        parsed = default;
        var reader = new QueryReader(request.Query);
        if (reader.ReadDateTime("startTime", out var startTime) is { } startError) return startError;
        if (reader.ReadDateTime("endTime", out var endTime) is { } endError) return endError;

        parsed = new ParsedGenAiEtlAuditPeriod(startTime, endTime);
        return null;
    }

    internal static IResult? ParseLogStream(HttpRequest request, out int? minSeverity) =>
        new QueryReader(request.Query).ReadInteger("minSeverity", out minSeverity);

    internal static IResult? ParseProfiles(HttpRequest request, out int? limit) =>
        new QueryReader(request.Query).ReadInteger("limit", out limit);

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
                   out parsed);
    }

    private static IResult Invalid(string field, string message, string code, string? rejectedValue) =>
        ContractErrorResults.Validation(field, message, code, rejectedValue);
}
