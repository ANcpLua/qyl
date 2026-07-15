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

    public static List<MetricStorageRow> ToMetricStorageRows(MetricIngestionBatch batch)
    {
        var rows = new List<MetricStorageRow>(batch.Metrics.Count);

        foreach (var metric in batch.Metrics)
            rows.Add(ToMetricStorageRow(metric));

        return rows;
    }

    public static List<ProfileDetail> ToProfileStorageRows(ProfileIngestionBatch batch)
    {
        var rows = new List<ProfileDetail>(batch.Profiles.Count);

        foreach (var profile in batch.Profiles)
            rows.Add(ToProfileStorageRows(profile));

        return rows;
    }

    private static SpanStorageRow ToSpanStorageRow(SpanIngestionRecord span)
    {
        var durationNs = span.EndTimeUnixNano >= span.StartTimeUnixNano
            ? span.EndTimeUnixNano - span.StartTimeUnixNano
            : 0UL;
        var projection = StorageAttributeProjection.ExtractSpanHotAttributes(span.Attributes);

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
            ResourceJson = PersistedAttributePolicy.SerializeResourceAttributes(span.ResourceAttributes),
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
        var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(log.ResourceAttributes);

        return new LogStorageRow
        {
            ProjectId = projectId,
            LogId = StableStorageId("log", builder =>
            {
                AppendIdentityPart(builder, projectId);
                AppendIdentityPart(builder, log.TraceId);
                AppendIdentityPart(builder, log.SpanId);
                AppendIdentityPart(builder, sessionId);
                AppendIdentityPart(builder, log.TimeUnixNano);
                AppendIdentityPart(builder, log.ObservedTimeUnixNano);
                AppendIdentityPart(builder, severityNumber);
                AppendIdentityPart(builder, severityText);
                AppendIdentityPart(builder, body);
                AppendIdentityPart(builder, log.ServiceName);
                AppendIdentityPart(builder, attributesJson);
                AppendIdentityPart(builder, resourceJson);
            }),
            TraceId = log.TraceId,
            SpanId = log.SpanId,
            SessionId = sessionId,
            TimeUnixNano = log.TimeUnixNano,
            ObservedTimeUnixNano = log.ObservedTimeUnixNano,
            SeverityNumber = severityNumber,
            SeverityText = severityText,
            Body = body,
            ServiceName = log.ServiceName,
            AttributesJson = attributesJson,
            ResourceJson = resourceJson
        };
    }

    private static MetricStorageRow ToMetricStorageRow(MetricIngestionRecord metric)
    {
        var projectId = ProjectScope.Normalize(metric.ProjectIdHint);
        var attributesJson = PersistedAttributePolicy.SerializeMetricAttributes(metric.Attributes);
        var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(metric.ResourceAttributes);

        return new MetricStorageRow
        {
            ProjectId = projectId,
            MetricId = StableStorageId("metric", builder =>
            {
                AppendIdentityPart(builder, projectId);
                AppendIdentityPart(builder, metric.MetricName);
                AppendIdentityPart(builder, metric.MetricType);
                AppendIdentityPart(builder, metric.Unit);
                AppendIdentityPart(builder, metric.ScopeName);
                AppendIdentityPart(builder, metric.TimeUnixNano);
                AppendIdentityPart(builder, metric.StartTimeUnixNano);
                AppendIdentityPart(builder, metric.ServiceName);
                // Identity uses the raw dimensions, not the persisted allow-list projection, so
                // data points differing only in a non-persisted dimension stay distinct rows.
                AppendIdentityPart(builder, metric.Attributes.Count);
                foreach (var (key, value) in metric.Attributes.OrderBy(static item => item.Key, StringComparer.Ordinal))
                {
                    AppendIdentityPart(builder, key);
                    AppendIdentityPart(builder, value.ToStableString());
                }

                AppendIdentityPart(builder, resourceJson);
            }),
            MetricName = metric.MetricName,
            MetricType = (byte)Math.Clamp(metric.MetricType, 0, 5),
            Unit = metric.Unit,
            Description = metric.Description,
            ScopeName = metric.ScopeName,
            TimeUnixNano = metric.TimeUnixNano,
            StartTimeUnixNano = metric.StartTimeUnixNano,
            Value = metric.Value,
            Count = metric.Count,
            Sum = metric.Sum,
            Min = metric.Min,
            Max = metric.Max,
            BucketsJson = SerializeHistogramBuckets(metric.HistogramBounds, metric.HistogramCounts),
            IsMonotonic = metric.IsMonotonic is { } monotonic ? (byte)(monotonic ? 1 : 0) : null,
            AggregationTemporality = metric.AggregationTemporality is { } temporality
                ? (byte)Math.Clamp(temporality, 0, 2)
                : null,
            ServiceName = metric.ServiceName,
            AttributesJson = attributesJson,
            ResourceJson = resourceJson
        };
    }

    private static ProfileDetail ToProfileStorageRows(ProfileIngestionRecord profile)
    {
        var projectId = ProjectScope.Normalize(profile.ProjectIdHint);
        var attributesJson = PersistedAttributePolicy.SerializeProfileAttributes(profile.Attributes);
        var resourceJson = PersistedAttributePolicy.SerializeResourceAttributes(profile.ResourceAttributes);
        var profileId = ResolveProfileId(projectId, profile, attributesJson, resourceJson);
        var header = new ProfileStorageRow
        {
            ProjectId = projectId,
            ProfileId = profileId,
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
            AttributesJson = attributesJson,
            ResourceJson = resourceJson,
            SchemaUrl = profile.SchemaUrl
        };

        return new ProfileDetail
        {
            Profile = header,
            Functions = profile.Functions.Select(function => new ProfileFunctionRow
            {
                ProjectId = projectId,
                ProfileId = profileId,
                Ordinal = function.Ordinal,
                Name = function.Name,
                SystemName = function.SystemName,
                Filename = function.Filename,
                StartLine = function.StartLine
            }).ToList(),
            Locations = profile.Locations.Select(location => new ProfileLocationRow
            {
                ProjectId = projectId,
                ProfileId = profileId,
                Ordinal = location.Ordinal,
                MappingOrdinal = location.MappingOrdinal,
                Address = location.Address,
                LinesJson = location.Lines is { Count: > 0 } lines
                    ? JsonSerializer.Serialize(lines, StorageJsonSerializerContext.Default.ProfileLocationLineJsonList)
                    : null
            }).ToList(),
            Mappings = profile.Mappings.Select(mapping => new ProfileMappingRow
            {
                ProjectId = projectId,
                ProfileId = profileId,
                Ordinal = mapping.Ordinal,
                Filename = mapping.Filename,
                MemoryStart = mapping.MemoryStart,
                MemoryLimit = mapping.MemoryLimit,
                FileOffset = mapping.FileOffset
            }).ToList(),
            Samples = profile.Samples.Select(sample => new ProfileSampleRow
            {
                ProjectId = projectId,
                ProfileId = profileId,
                Ordinal = sample.Ordinal,
                StackOrdinal = sample.StackOrdinal,
                LinkTraceId = sample.LinkTraceId,
                LinkSpanId = sample.LinkSpanId,
                ValuesJson = sample.Values is { Length: > 0 } values
                    ? JsonSerializer.Serialize(values, StorageJsonSerializerContext.Default.Int64Array)
                    : null,
                TimestampsJson = sample.TimestampsUnixNano is { Length: > 0 } timestamps
                    ? JsonSerializer.Serialize(timestamps, StorageJsonSerializerContext.Default.UInt64Array)
                    : null
            }).ToList(),
            Stacks = profile.Stacks.Select(stack => new ProfileStackRow
            {
                ProjectId = projectId,
                ProfileId = profileId,
                Ordinal = stack.Ordinal,
                LocationOrdinalsJson = stack.LocationOrdinals is { Length: > 0 } ordinals
                    ? JsonSerializer.Serialize(ordinals, StorageJsonSerializerContext.Default.Int32Array)
                    : null
            }).ToList()
        };
    }

    private static string ResolveProfileId(
        string projectId,
        ProfileIngestionRecord profile,
        string? attributesJson,
        string? resourceJson)
    {
        if (!string.IsNullOrWhiteSpace(profile.ProfileId))
            return profile.ProfileId;

        return StableStorageId("profile", builder =>
        {
            AppendIdentityPart(builder, projectId);
            AppendIdentityPart(builder, profile.TraceId);
            AppendIdentityPart(builder, profile.SpanId);
            AppendIdentityPart(builder, profile.SessionId);
            AppendIdentityPart(builder, profile.TimeUnixNano);
            AppendIdentityPart(builder, profile.DurationNano);
            AppendIdentityPart(builder, profile.SampleCount);
            AppendIdentityPart(builder, profile.SampleType);
            AppendIdentityPart(builder, profile.SampleUnit);
            AppendIdentityPart(builder, profile.OriginalPayloadFormat);
            AppendIdentityPart(builder, profile.ServiceName);
            AppendIdentityPart(builder, attributesJson);
            AppendIdentityPart(builder, resourceJson);
            AppendIdentityPart(builder, profile.SchemaUrl);

            AppendIdentityPart(builder, profile.Functions.Count);
            foreach (var function in profile.Functions.OrderBy(static item => item.Ordinal))
            {
                AppendIdentityPart(builder, function.Ordinal);
                AppendIdentityPart(builder, function.Name);
                AppendIdentityPart(builder, function.SystemName);
                AppendIdentityPart(builder, function.Filename);
                AppendIdentityPart(builder, function.StartLine);
            }

            AppendIdentityPart(builder, profile.Locations.Count);
            foreach (var location in profile.Locations.OrderBy(static item => item.Ordinal))
            {
                AppendIdentityPart(builder, location.Ordinal);
                AppendIdentityPart(builder, location.MappingOrdinal);
                AppendIdentityPart(builder, location.Address);
                AppendIdentityPart(builder, location.Lines?.Count);
                if (location.Lines is not null)
                {
                    foreach (var line in location.Lines)
                    {
                        AppendIdentityPart(builder, line.FunctionOrdinal);
                        AppendIdentityPart(builder, line.Line);
                        AppendIdentityPart(builder, line.Column);
                    }
                }
            }

            AppendIdentityPart(builder, profile.Mappings.Count);
            foreach (var mapping in profile.Mappings.OrderBy(static item => item.Ordinal))
            {
                AppendIdentityPart(builder, mapping.Ordinal);
                AppendIdentityPart(builder, mapping.Filename);
                AppendIdentityPart(builder, mapping.MemoryStart);
                AppendIdentityPart(builder, mapping.MemoryLimit);
                AppendIdentityPart(builder, mapping.FileOffset);
            }

            AppendIdentityPart(builder, profile.Samples.Count);
            foreach (var sample in profile.Samples.OrderBy(static item => item.Ordinal))
            {
                AppendIdentityPart(builder, sample.Ordinal);
                AppendIdentityPart(builder, sample.StackOrdinal);
                AppendIdentityPart(builder, sample.LinkTraceId);
                AppendIdentityPart(builder, sample.LinkSpanId);
                AppendIdentityParts(builder, sample.Values);
                AppendIdentityParts(builder, sample.TimestampsUnixNano);
            }

            AppendIdentityPart(builder, profile.Stacks.Count);
            foreach (var stack in profile.Stacks.OrderBy(static item => item.Ordinal))
            {
                AppendIdentityPart(builder, stack.Ordinal);
                AppendIdentityParts(builder, stack.LocationOrdinals);
            }
        });
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

    private static void AppendIdentityPart(StringBuilder builder, int? value) =>
        AppendIdentityPart(builder, value?.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, long? value) =>
        AppendIdentityPart(builder, value?.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, ulong value) =>
        AppendIdentityPart(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityPart(StringBuilder builder, ulong? value) =>
        AppendIdentityPart(builder, value?.ToString(CultureInfo.InvariantCulture));

    private static void AppendIdentityParts(StringBuilder builder, IReadOnlyList<int>? values)
    {
        AppendIdentityPart(builder, values?.Count);
        if (values is null)
            return;

        foreach (var value in values)
            AppendIdentityPart(builder, value);
    }

    private static void AppendIdentityParts(StringBuilder builder, IReadOnlyList<long>? values)
    {
        AppendIdentityPart(builder, values?.Count);
        if (values is null)
            return;

        foreach (var value in values)
            AppendIdentityPart(builder, value);
    }

    private static void AppendIdentityParts(StringBuilder builder, IReadOnlyList<ulong>? values)
    {
        AppendIdentityPart(builder, values?.Count);
        if (values is null)
            return;

        foreach (var value in values)
            AppendIdentityPart(builder, value);
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

    private static string? SerializeHistogramBuckets(
        IReadOnlyList<double>? explicitBounds,
        IReadOnlyList<ulong>? bucketCounts)
    {
        if (bucketCounts is not { Count: > 0 })
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("bounds");
            foreach (var bound in explicitBounds ?? [])
                writer.WriteNumberValue(bound);
            writer.WriteEndArray();
            writer.WriteStartArray("counts");
            foreach (var count in bucketCounts)
                writer.WriteNumberValue(count);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
