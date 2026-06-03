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

    public static List<ProfileConversionResult> ToProfileStorageRows(ProfileIngestionBatch batch)
    {
        var rows = new List<ProfileConversionResult>(batch.Profiles.Count);

        foreach (var profile in batch.Profiles)
            rows.Add(ToProfileStorageRows(profile));

        return rows;
    }

    private static SpanStorageRow ToSpanStorageRow(SpanIngestionRecord span)
    {
        var durationNs = span.EndTimeUnixNano >= span.StartTimeUnixNano
            ? span.EndTimeUnixNano - span.StartTimeUnixNano
            : 0UL;
        var projection = AttributeKeySets.ExtractSpanStorageProjection(span.Attributes);

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
            GenAiRequestModel = projection.GenAiRequestModel,
            GenAiResponseModel = projection.GenAiResponseModel,
            GenAiInputTokens = projection.GenAiInputTokens,
            GenAiOutputTokens = projection.GenAiOutputTokens,
            GenAiTemperature = projection.GenAiTemperature,
            GenAiStopReason = projection.GenAiStopReason,
            GenAiToolName = projection.GenAiToolName,
            GenAiCostUsd = projection.GenAiCostUsd,
            AttributesJson = PersistedAttributePolicy.SerializeSpanAttributes(span.Attributes),
            ResourceJson = PersistedAttributePolicy.SerializeResourceAttributes(span.ResourceAttributes),
            SchemaUrl = span.SchemaUrl
        };
    }

    private static LogStorageRow ToLogStorageRow(LogIngestionRecord log)
    {
        var severityText = string.IsNullOrEmpty(log.SeverityText)
            ? SeverityNumberToText(log.SeverityNumber)
            : log.SeverityText;

        return new LogStorageRow
        {
            ProjectId = ProjectScope.Normalize(log.ProjectIdHint),
            LogId = Guid.NewGuid().ToString("N"),
            TraceId = log.TraceId,
            SpanId = log.SpanId,
            SessionId = log.Attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation),
            TimeUnixNano = log.TimeUnixNano,
            ObservedTimeUnixNano = log.ObservedTimeUnixNano,
            SeverityNumber = (byte)Math.Clamp(log.SeverityNumber, 0, 24),
            SeverityText = severityText,
            Body = ToSafeLogBody(log.BodyText),
            ServiceName = log.ServiceName,
            AttributesJson = PersistedAttributePolicy.SerializeLogAttributes(log.Attributes),
            ResourceJson = PersistedAttributePolicy.SerializeResourceAttributes(log.ResourceAttributes)
        };
    }

    private static ProfileConversionResult ToProfileStorageRows(ProfileIngestionRecord profile)
    {
        var projectId = ProjectScope.Normalize(profile.ProjectIdHint);
        var header = new ProfileStorageRow
        {
            ProjectId = projectId,
            ProfileId = profile.ProfileId,
            TraceId = profile.TraceId,
            SpanId = profile.SpanId,
            SessionId = profile.SessionId,
            TimeUnixNano = profile.TimeUnixNano,
            DurationNano = profile.DurationNano,
            SampleCount = profile.SampleCount,
            SampleType = profile.SampleType,
            SampleUnit = profile.SampleUnit,
            OriginalPayloadFormat = profile.OriginalPayloadFormat,
            ServiceName = profile.ServiceName,
            AttributesJson = PersistedAttributePolicy.SerializeProfileAttributes(profile.Attributes),
            ResourceJson = PersistedAttributePolicy.SerializeResourceAttributes(profile.ResourceAttributes),
            SchemaUrl = profile.SchemaUrl
        };

        return new ProfileConversionResult
        {
            Profile = header,
            Functions = profile.Functions.Select(function => new ProfileFunctionRow
            {
                ProjectId = projectId,
                ProfileId = profile.ProfileId,
                Ordinal = function.Ordinal,
                Name = function.Name,
                SystemName = function.SystemName,
                Filename = function.Filename,
                StartLine = function.StartLine
            }).ToList(),
            Locations = profile.Locations.Select(location => new ProfileLocationRow
            {
                ProjectId = projectId,
                ProfileId = profile.ProfileId,
                Ordinal = location.Ordinal,
                MappingOrdinal = location.MappingOrdinal,
                Address = location.Address,
                LinesJson = location.Lines is { Count: > 0 } lines
                    ? JsonSerializer.Serialize(lines, IngestionJsonSerializerContext.Default.ProfileLocationLineJsonList)
                    : null
            }).ToList(),
            Mappings = profile.Mappings.Select(mapping => new ProfileMappingRow
            {
                ProjectId = projectId,
                ProfileId = profile.ProfileId,
                Ordinal = mapping.Ordinal,
                Filename = mapping.Filename,
                MemoryStart = mapping.MemoryStart,
                MemoryLimit = mapping.MemoryLimit,
                FileOffset = mapping.FileOffset
            }).ToList(),
            Samples = profile.Samples.Select(sample => new ProfileSampleRow
            {
                ProjectId = projectId,
                ProfileId = profile.ProfileId,
                Ordinal = sample.Ordinal,
                StackOrdinal = sample.StackOrdinal,
                LinkTraceId = sample.LinkTraceId,
                LinkSpanId = sample.LinkSpanId,
                ValuesJson = sample.Values is { Length: > 0 } values ? JsonSerializer.Serialize(values) : null,
                TimestampsJson = sample.TimestampsUnixNano is { Length: > 0 } timestamps
                    ? JsonSerializer.Serialize(timestamps)
                    : null
            }).ToList(),
            Stacks = profile.Stacks.Select(stack => new ProfileStackRow
            {
                ProjectId = projectId,
                ProfileId = profile.ProfileId,
                Ordinal = stack.Ordinal,
                LocationOrdinalsJson = stack.LocationOrdinals is { Length: > 0 } ordinals
                    ? JsonSerializer.Serialize(ordinals)
                    : null
            }).ToList()
        };
    }

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
}
