using System.Text;

namespace Qyl.Collector.Storage;

internal static class IngestionStorageMapper
{
    public static List<SpanStorageRow> ToSpanStorageRows(TraceIngestionBatch batch)
    {
        var rows = new List<SpanStorageRow>(batch.Spans.Count);

        foreach (var span in batch.Spans)
            rows.Add(ToSpanStorageRow(span));

        return rows;
    }

    public static List<LogStorageRow> ToLogStorageRows(LogIngestionBatch batch)
    {
        var rows = new List<LogStorageRow>(batch.Logs.Count);

        foreach (var log in batch.Logs)
            rows.Add(ToLogStorageRow(log));

        return rows;
    }

    private static SpanStorageRow ToSpanStorageRow(SpanIngestionRecord span)
    {
        var durationNs = span.EndTimeUnixNano >= span.StartTimeUnixNano
            ? span.EndTimeUnixNano - span.StartTimeUnixNano
            : 0UL;
        var projection = StorageAttributeProjection.ExtractSpanHotAttributes(span.Attributes);
        var resourceEntityRefsJson = SerializeResourceEntityRefs(span.ResourceEntityRefs);

        return new SpanStorageRow
        {
            ProjectId = ProjectScope.Normalize(span.ProjectIdHint),
            SpanId = span.SpanId,
            TraceId = span.TraceId,
            ParentSpanId = string.IsNullOrEmpty(span.ParentSpanId) ? null : span.ParentSpanId,
            SessionId = projection.SessionId,
            Name = span.Name,
            Kind = ConvertSpanKindToByte(span.Kind),
            StartTimeUnixNano = span.StartTimeUnixNano,
            EndTimeUnixNano = span.EndTimeUnixNano,
            DurationNs = durationNs,
            StatusCode = ConvertStatusCodeToByte(span.StatusCode),
            ServiceName = span.ServiceName,
            GenAiProviderName = projection.GenAiProviderName,
            GenAiOperationName = projection.GenAiOperationName,
            GenAiOutputType = projection.GenAiOutputType,
            GenAiRequestModel = projection.GenAiRequestModel,
            GenAiResponseModel = projection.GenAiResponseModel,
            GenAiInputTokens = projection.GenAiInputTokens,
            GenAiOutputTokens = projection.GenAiOutputTokens,
            GenAiCacheReadInputTokens = projection.GenAiCacheReadInputTokens,
            GenAiCacheCreationInputTokens = projection.GenAiCacheCreationInputTokens,
            GenAiReasoningTokens = projection.GenAiReasoningTokens,
            AttributesJson = PersistedAttributePolicy.SerializeSpanAttributes(span.Attributes, projection),
            ResourceJson = PersistedAttributePolicy.SerializeResourceAttributes(
                span.ResourceAttributes,
                span.ResourceEntityRefs),
            ResourceEntityRefsJson = resourceEntityRefsJson,
            SchemaUrl = span.SchemaUrl,
            StatusMessage = span.StatusMessage,
            EventsJson = SpanChildStorage.SerializeEvents(span.Events),
            LinksJson = SpanChildStorage.SerializeLinks(span.Links)
        };
    }

    private static LogStorageRow ToLogStorageRow(LogIngestionRecord log)
    {
        var projectId = ProjectScope.Normalize(log.ProjectIdHint);
        var severityText = string.IsNullOrEmpty(log.SeverityText)
            ? SeverityNumberToText(log.SeverityNumber)
            : log.SeverityText;
        var severityNumber = (byte)Math.Clamp(log.SeverityNumber, 0, 24);
        var sessionId = log.Attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation);
        var body = ToSafeLogBody(log.BodyText);
        var attributesJson = PersistedAttributePolicy.SerializeLogAttributes(log.Attributes);
        var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(
            log.ResourceAttributes,
            log.ResourceEntityRefs);
        var resourceEntityRefsJson = SerializeResourceEntityRefs(log.ResourceEntityRefs);
        var resourceEntityRefsIdentity = GetResourceEntityRefsIdentity(
            log.ResourceEntityRefs,
            log.ResourceAttributes);

        return new LogStorageRow
        {
            ProjectId = projectId,
            LogId = StableStorageId("log", builder =>
            {
                AppendIdentityPart(builder, projectId);
                AppendIdentityPart(builder, log.TraceId);
                AppendIdentityPart(builder, log.SpanId);
                AppendIdentityPart(builder, log.EventName);
                AppendIdentityPart(builder, sessionId);
                AppendIdentityPart(builder, log.TimeUnixNano);
                AppendIdentityPart(builder, log.ObservedTimeUnixNano);
                AppendIdentityPart(builder, severityNumber);
                AppendIdentityPart(builder, severityText);
                AppendIdentityPart(builder, body);
                AppendIdentityPart(builder, log.ServiceName);
                AppendIdentityPart(builder, attributesJson);
                AppendIdentityPart(builder, resourceJson);
                if (!string.IsNullOrEmpty(resourceEntityRefsIdentity))
                    AppendIdentityPart(builder, resourceEntityRefsIdentity);
            }),
            TraceId = log.TraceId,
            SpanId = log.SpanId,
            EventName = log.EventName,
            SessionId = sessionId,
            TimeUnixNano = log.TimeUnixNano,
            ObservedTimeUnixNano = log.ObservedTimeUnixNano,
            SeverityNumber = severityNumber,
            SeverityText = severityText,
            Body = body,
            ServiceName = log.ServiceName,
            AttributesJson = attributesJson,
            ResourceJson = resourceJson,
            ResourceEntityRefsJson = resourceEntityRefsJson
        };
    }

    private static string StableStorageId(string prefix, Action<StringBuilder> appendParts)
    {
        var builder = new StringBuilder(512);
        builder.Append(prefix).Append('\n');
        appendParts(builder);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return prefix + "_" + Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private static void AppendIdentityPart(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:").Append('\n');
            return;
        }

        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static void AppendIdentityPart(StringBuilder builder, byte value) =>
        AppendIdentityPart(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, int value) =>
        AppendIdentityPart(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, ulong value) =>
        AppendIdentityPart(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, ulong? value) =>
        AppendIdentityPart(builder, value?.ToString(CultureInfo.InvariantCulture));

    private static string? ToSafeLogBody(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        var fingerprint = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return $"sha256:{fingerprint};chars:{raw.Length};bytes:{bytes.Length}";
    }

    private static byte ConvertSpanKindToByte(int? kind) => kind switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        4 => 4,
        5 => 5,
        _ => 0
    };

    private static byte ConvertStatusCodeToByte(int? code) => code switch
    {
        1 => 1,
        2 => 2,
        _ => 0
    };

    private static string SeverityNumberToText(int severityNumber) => severityNumber switch
    {
        >= 1 and <= 4 => "TRACE",
        >= 5 and <= 8 => "DEBUG",
        >= 9 and <= 12 => "INFO",
        >= 13 and <= 16 => "WARN",
        >= 17 and <= 20 => "ERROR",
        >= 21 => "FATAL",
        _ => "UNSPECIFIED"
    };

    private static string? SerializeResourceEntityRefs(
        IReadOnlyList<ResourceEntityRefIngestionRecord> entityRefs) =>
        entityRefs.Count is 0
            ? null
            : JsonSerializer.Serialize(
                entityRefs as List<ResourceEntityRefIngestionRecord> ?? [.. entityRefs],
                StorageJsonSerializerContext.Default.ResourceEntityRefIngestionRecordList);

    private static string? GetResourceEntityRefsIdentity(
        IReadOnlyList<ResourceEntityRefIngestionRecord> entityRefs,
        IReadOnlyDictionary<string, OtlpAttributeValue> resourceAttributes)
    {
        if (entityRefs.Count is 0) return null;

        var identities = new List<string>(entityRefs.Count);
        foreach (var entityRef in entityRefs)
        {
            var identity = new StringBuilder();
            AppendIdentityPart(identity, entityRef.Type);
            var idKeys = entityRef.IdKeys.Order(StringComparer.Ordinal).ToArray();
            AppendIdentityPart(identity, idKeys.Length);
            foreach (var key in idKeys)
            {
                if (!resourceAttributes.TryGetValue(key, out var value))
                {
                    throw new InvalidDataException(
                        $"Resource entity id_key '{key}' does not reference a persisted resource attribute.");
                }

                AppendIdentityPart(identity, key);
                AppendIdentityPart(identity, value.ToIdentityString());
            }
            identities.Add(identity.ToString());
        }

        identities.Sort(StringComparer.Ordinal);
        var result = new StringBuilder();
        AppendIdentityPart(result, identities.Count);
        foreach (var identity in identities)
            AppendIdentityPart(result, identity);
        return result.ToString();
    }

}
