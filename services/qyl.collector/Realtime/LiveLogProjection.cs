using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Realtime;

internal static class LiveLogProjection
{
    public static LiveLogDto ToDto(LogStorageRow log, int repeatCount = 1, bool isDuplicateSummary = false)
    {
        var timestamp = TimeConversions.UnixNanoToDateTime(log.TimeUnixNano).ToString("O");
        var observedTimestamp = log.ObservedTimeUnixNano.HasValue
            ? TimeConversions.UnixNanoToDateTime(log.ObservedTimeUnixNano.Value).ToString("O")
            : timestamp;

        var body = log.Body ?? string.Empty;
        if (isDuplicateSummary && repeatCount > 0)
            body = $"{body} [repeated {repeatCount}x]";

        return new LiveLogDto(
            timestamp,
            observedTimestamp,
            log.TraceId,
            log.SpanId,
            log.SeverityNumber,
            NormalizeSeverity(log.SeverityText, log.SeverityNumber),
            body,
            ParseAttributes(log.AttributesJson),
            log.ServiceName ?? "unknown"
        );
    }

    public static LiveLogDto ToDto(DeduplicatedLiveLog deduped) =>
        ToDto(deduped.Log, deduped.RepeatCount, deduped.IsDuplicateSummary);

    public static string NormalizeSeverity(string? severityText, byte severityNumber)
    {
        var normalized = severityText?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "trace" or "debug" or "info" or "warn" or "error" or "fatal" => normalized,
            "warning" => "warn",
            "log" => "info",
            _ => severityNumber switch
            {
                >= 21 => "fatal",
                >= 17 => "error",
                >= 13 => "warn",
                >= 9 => "info",
                >= 5 => "debug",
                _ => "trace"
            }
        };
    }

    private static Dictionary<string, object> ParseAttributes(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(attributesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return [];

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.TryGetInt64(out var i) ? i : property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
        catch
        {
            return [];
        }
    }
}

internal sealed record LiveLogDto(
    string Timestamp,
    string ObservedTimestamp,
    string? TraceId,
    string? SpanId,
    int SeverityNumber,
    string SeverityText,
    string Body,
    IReadOnlyDictionary<string, object> Attributes,
    string ServiceName
);
