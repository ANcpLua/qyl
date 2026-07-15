using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.OTel.Profiles;
using Qyl.Api.Contracts.OTel.Traces;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;
using TraceContract = Qyl.Api.Contracts.OTel.Traces.Trace;

namespace Qyl.Collector.Mapping;

internal static class ContractJson
{
    public static IReadOnlyList<ContractAttribute>? ParseAttributes(string? json) =>
        TryParseAttributes(json, out var attributes) ? attributes : null;

    public static bool TryParseAttributes(string? json, out IReadOnlyList<ContractAttribute>? attributes)
    {
        attributes = null;
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return false;

            var parsed = new List<ContractAttribute>();
            foreach (var property in document.RootElement.EnumerateObject())
                parsed.Add(new ContractAttribute { Key = property.Name, Value = ReadValue(property.Value)! });

            attributes = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static object? ReadValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number when value.TryGetInt64(out var int64) => int64,
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => value.EnumerateArray().Select(ReadValue).ToArray(),
            JsonValueKind.Object => ReadObject(value),
            // Attribute.value is required even for OTLP empty AnyValue. A cloned JsonElement keeps
            // the explicit JSON null from being removed by the context's WhenWritingNull policy.
            JsonValueKind.Null => value.Clone(),
            _ => value.GetRawText()
        };

    private static Dictionary<string, object?> ReadObject(JsonElement value)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            result[property.Name] = ReadValue(property.Value);

        return result;
    }
}

internal static class ResourceMapping
{
    public static string ServiceNameOrUnknown(string? serviceName) =>
        string.IsNullOrWhiteSpace(serviceName) ? "unknown" : serviceName;
}

internal static class SpanMapper
{
    public static List<Span> ToContracts(IEnumerable<SpanStorageRow> records) =>
    [
        .. records.Select(static r => ToContractCore(
            r.TraceId, r.SpanId, r.ParentSpanId,
            r.Name, r.Kind, r.StatusCode,
            r.StartTimeUnixNano, r.EndTimeUnixNano,
            r.ServiceName,
            r.AttributesJson, r.ResourceJson, r.SchemaUrl,
            r.StatusMessage, r.EventsJson, r.LinksJson))
    ];

    public static TraceContract ToTrace(string traceId, IReadOnlyList<Span> spans)
    {
        var rootSpan = spans.FirstOrDefault(static s => s.ParentSpanId is null);
        var start = spans.Min(static s => s.StartTimeUnixNano);
        var end = spans.Max(static s => s.EndTimeUnixNano);
        var duration = end >= start ? end - start : 0UL;

        return new TraceContract
        {
            TraceId = traceId,
            Spans = spans,
            RootSpan = rootSpan,
            SpanCount = spans.Count,
            DurationNs = duration,
            StartTime = QylTimeConversions.NanosToDateTimeOffset(start),
            EndTime = QylTimeConversions.NanosToDateTimeOffset(end),
            Services = [.. spans.Select(static s => s.Resource.ServiceName).Distinct(StringComparer.Ordinal)],
            HasError = spans.Any(static s => s.Status.Code is SpanStatusCode.Error)
        };
    }

    private static SpanKind MapSpanKind(byte kind) =>
        kind switch
        {
            1 => SpanKind.Internal,
            2 => SpanKind.Server,
            3 => SpanKind.Client,
            4 => SpanKind.Producer,
            5 => SpanKind.Consumer,
            _ => SpanKind.Unspecified
        };

    private static SpanStatusCode MapStatus(byte statusCode) =>
        statusCode switch
        {
            1 => SpanStatusCode.Ok,
            2 => SpanStatusCode.Error,
            _ => SpanStatusCode.Unset
        };

    private static IReadOnlyList<SpanEvent> MapEvents(string? eventsJson)
    {
        var stored = SpanChildStorage.DeserializeEvents(eventsJson);
        if (stored is null || stored.Count is 0) return [];

        var result = new List<SpanEvent>(stored.Count);
        foreach (var e in stored)
        {
            result.Add(new SpanEvent
            {
                Name = e.Name,
                TimeUnixNano = e.TimeUnixNano,
                Attributes = ContractJson.ParseAttributes(e.AttributesJson)
            });
        }

        return result;
    }

    private static IReadOnlyList<SpanLink> MapLinks(string? linksJson)
    {
        var stored = SpanChildStorage.DeserializeLinks(linksJson);
        if (stored is null || stored.Count is 0) return [];

        var result = new List<SpanLink>(stored.Count);
        foreach (var l in stored)
        {
            result.Add(new SpanLink
            {
                TraceId = l.TraceId,
                SpanId = l.SpanId,
                Attributes = ContractJson.ParseAttributes(l.AttributesJson)
            });
        }

        return result;
    }

    private static Span ToContractCore(
        string traceId, string spanId, string? parentSpanId,
        string name, byte kind, byte statusCode,
        ulong startTimeUnixNano, ulong endTimeUnixNano,
        string? serviceName,
        string? attributesJson, string? resourceJson, string? schemaUrl,
        string? statusMessage, string? eventsJson, string? linksJson)
    {
        var attributes = ContractJson.ParseAttributes(attributesJson);
        var resourceAttributes = ContractJson.ParseAttributes(resourceJson);

        return new Span
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = name,
            Kind = MapSpanKind(kind),
            StartTimeUnixNano = startTimeUnixNano,
            EndTimeUnixNano = endTimeUnixNano,
            Attributes = attributes,
            Events = MapEvents(eventsJson),
            Links = MapLinks(linksJson),
            Status = new SpanStatus
            {
                Code = MapStatus(statusCode),
                Message = statusMessage
            },
            Resource = new Resource
            {
                ServiceName = ResourceMapping.ServiceNameOrUnknown(serviceName),
                Attributes = resourceAttributes
            },
            InstrumentationScope = schemaUrl is null
                ? null
                : new InstrumentationScope { ScopeName = schemaUrl }
        };
    }
}

internal static class LogMapper
{
    public static LogRecord ToContract(LogStorageRow record) =>
        new()
        {
            TimeUnixNano = record.TimeUnixNano,
            ObservedTimeUnixNano = record.ObservedTimeUnixNano ?? record.TimeUnixNano,
            SeverityNumber = MapSeverityNumber(record.SeverityNumber),
            SeverityText = MapSeverityText(record.SeverityText, record.SeverityNumber),
            Body = new LogBodyString { StringValue = record.Body ?? "" },
            Attributes = ContractJson.ParseAttributes(record.AttributesJson),
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            EventName = record.EventName,
            Resource = new Resource
            {
                ServiceName = ResourceMapping.ServiceNameOrUnknown(record.ServiceName),
                Attributes = ContractJson.ParseAttributes(record.ResourceJson)
            }
        };

    public static IReadOnlyList<LogRecord> ToContracts(IReadOnlyList<LogStorageRow> records) =>
        [.. records.Select(static record => ToContract(record))];

    private static SeverityNumber MapSeverityNumber(byte severityNumber) =>
        Enum.IsDefined(typeof(SeverityNumber), (int)severityNumber)
            ? (SeverityNumber)severityNumber
            : SeverityNumber.Unspecified;

    private static SeverityText MapSeverityText(string? severityText, byte severityNumber)
    {
        var normalized = severityText?.Trim();
        if (Enum.TryParse<SeverityText>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        return severityNumber switch
        {
            >= 21 => (SeverityText)5,
            >= 17 => (SeverityText)4,
            >= 13 => (SeverityText)3,
            >= 9 => (SeverityText)2,
            >= 5 => (SeverityText)1,
            _ => (SeverityText)0
        };
    }
}

internal static class MetricMapper
{
    public static MetricPoint ToContract(MetricStorageRow record) =>
        TryToContract(record, out var contract)
            ? contract
            : throw new InvalidDataException(
                $"Stored metric '{record.MetricId}' does not contain a complete MetricPoint projection.");

    public static bool TryToContract(MetricStorageRow record, out MetricPoint contract)
    {
        contract = null!;
        if (record.ContractProjectionVersion is not MetricStorageRow.CurrentContractProjectionVersion ||
            string.IsNullOrWhiteSpace(record.MetricName) ||
            record.TimeUnixNano is 0 ||
            record.StartTimeUnixNano is not { } startTimeUnixNano ||
            record.Flags is not { } storedFlags || storedFlags is < 0 or > uint.MaxValue ||
            record.ResourceDroppedAttributesCount is not { } resourceDroppedCount ||
            resourceDroppedCount is < 0 or > uint.MaxValue ||
            record.HasInstrumentationScope is not (0 or 1) ||
            record.ScopeDroppedAttributesCount is not { } scopeDroppedCount ||
            scopeDroppedCount is < 0 or > uint.MaxValue ||
            !ContractJson.TryParseAttributes(record.MetadataJson, out var metadata) ||
            !ContractJson.TryParseAttributes(record.AttributesJson, out var attributes) ||
            !ContractJson.TryParseAttributes(record.ResourceJson, out var resourceAttributes) ||
            !ContractJson.TryParseAttributes(record.ScopeAttributesJson, out var scopeAttributes) ||
            !TryMapExemplars(record.ExemplarsJson, out var exemplars))
        {
            return false;
        }

        var hasScope = record.HasInstrumentationScope is 1;
        if (!hasScope &&
            (record.ScopeName is not null || record.ScopeVersion is not null ||
             scopeAttributes is not null || scopeDroppedCount is not 0))
        {
            return false;
        }

        var resource = new Resource
        {
            ServiceName = ResourceMapping.ServiceNameOrUnknown(record.ServiceName),
            Attributes = resourceAttributes,
            DroppedAttributesCount = resourceDroppedCount
        };
        var scope = hasScope
            ? new InstrumentationScope
            {
                ScopeName = record.ScopeName ?? "",
                ScopeVersion = record.ScopeVersion,
                ScopeAttributes = scopeAttributes,
                DroppedAttributesCount = scopeDroppedCount
            }
            : null;
        var flags = (uint)storedFlags;
        var noRecordedValue = (flags & 1U) is not 0;
        if (noRecordedValue && exemplars is { Count: > 0 }) return false;

        switch (record.MetricType)
        {
            case MetricStorageTypes.Gauge:
                if (!TryMapNumberValue(
                        record.IntValue,
                        record.DoubleValue,
                        noRecordedValue,
                        out var gaugeValue)) return false;
                contract = new GaugeMetricPoint
                {
                    Name = record.MetricName,
                    Description = record.Description,
                    Unit = record.Unit,
                    Metadata = metadata,
                    Attributes = attributes,
                    StartTimeUnixNano = startTimeUnixNano,
                    TimeUnixNano = record.TimeUnixNano,
                    Flags = flags,
                    Resource = resource,
                    ResourceSchemaUrl = record.ResourceSchemaUrl,
                    InstrumentationScope = scope,
                    ScopeSchemaUrl = record.ScopeSchemaUrl,
                    Value = gaugeValue!,
                    Exemplars = exemplars
                };
                return true;

            case MetricStorageTypes.Sum:
                if (!TryMapNumberValue(
                        record.IntValue,
                        record.DoubleValue,
                        noRecordedValue,
                        out var sumValue) ||
                    !TryMapAggregationTemporality(record.AggregationTemporality, out var sumTemporality) ||
                    record.IsMonotonic is not (0 or 1))
                {
                    return false;
                }

                contract = new SumMetricPoint
                {
                    Name = record.MetricName,
                    Description = record.Description,
                    Unit = record.Unit,
                    Metadata = metadata,
                    Attributes = attributes,
                    StartTimeUnixNano = startTimeUnixNano,
                    TimeUnixNano = record.TimeUnixNano,
                    Flags = flags,
                    Resource = resource,
                    ResourceSchemaUrl = record.ResourceSchemaUrl,
                    InstrumentationScope = scope,
                    ScopeSchemaUrl = record.ScopeSchemaUrl,
                    Value = sumValue!,
                    AggregationTemporality = sumTemporality,
                    IsMonotonic = record.IsMonotonic is 1,
                    Exemplars = exemplars
                };
                return true;

            case MetricStorageTypes.Histogram:
                if (record.Count is not { } histogramCount ||
                    !TryMapAggregationTemporality(record.AggregationTemporality, out var histogramTemporality) ||
                    !TryReadHistogramBuckets(record.BucketsJson, histogramCount, out var histogramBuckets) ||
                    !IsValidCountAndSum(histogramCount, record.Sum) ||
                    !IsValidMinMax(record.Min, record.Max))
                {
                    return false;
                }

                contract = new HistogramMetricPoint
                {
                    Name = record.MetricName,
                    Description = record.Description,
                    Unit = record.Unit,
                    Metadata = metadata,
                    Attributes = attributes,
                    StartTimeUnixNano = startTimeUnixNano,
                    TimeUnixNano = record.TimeUnixNano,
                    Flags = flags,
                    Resource = resource,
                    ResourceSchemaUrl = record.ResourceSchemaUrl,
                    InstrumentationScope = scope,
                    ScopeSchemaUrl = record.ScopeSchemaUrl,
                    Count = histogramCount,
                    Sum = record.Sum,
                    BucketCounts = histogramBuckets.BucketCounts,
                    ExplicitBounds = histogramBuckets.ExplicitBounds,
                    Min = record.Min,
                    Max = record.Max,
                    AggregationTemporality = histogramTemporality,
                    Exemplars = exemplars
                };
                return true;

            case MetricStorageTypes.ExponentialHistogram:
                if (record.Count is not { } exponentialCount ||
                    record.ExponentialHistogramScale is not { } scale ||
                    record.ExponentialHistogramZeroCount is not { } zeroCount ||
                    record.ExponentialHistogramZeroThreshold is not { } zeroThreshold ||
                    !TryMapAggregationTemporality(record.AggregationTemporality, out var exponentialTemporality) ||
                    !TryReadExponentialHistogramBuckets(
                        record.ExponentialHistogramBucketsJson,
                        out var exponentialBuckets) ||
                    !double.IsFinite(zeroThreshold) || zeroThreshold < 0 ||
                    !TrySumExponentialBuckets(exponentialBuckets, zeroCount, out var distributedCount) ||
                    distributedCount != exponentialCount ||
                    !IsValidCountAndSum(exponentialCount, record.Sum) ||
                    !IsValidMinMax(record.Min, record.Max))
                {
                    return false;
                }

                contract = new ExponentialHistogramMetricPoint
                {
                    Name = record.MetricName,
                    Description = record.Description,
                    Unit = record.Unit,
                    Metadata = metadata,
                    Attributes = attributes,
                    StartTimeUnixNano = startTimeUnixNano,
                    TimeUnixNano = record.TimeUnixNano,
                    Flags = flags,
                    Resource = resource,
                    ResourceSchemaUrl = record.ResourceSchemaUrl,
                    InstrumentationScope = scope,
                    ScopeSchemaUrl = record.ScopeSchemaUrl,
                    Count = exponentialCount,
                    Sum = record.Sum,
                    Scale = scale,
                    ZeroCount = zeroCount,
                    ZeroThreshold = zeroThreshold,
                    Positive = new ExponentialHistogramBuckets
                    {
                        Offset = exponentialBuckets.PositiveOffset,
                        BucketCounts = exponentialBuckets.PositiveBucketCounts
                    },
                    Negative = new ExponentialHistogramBuckets
                    {
                        Offset = exponentialBuckets.NegativeOffset,
                        BucketCounts = exponentialBuckets.NegativeBucketCounts
                    },
                    Min = record.Min,
                    Max = record.Max,
                    AggregationTemporality = exponentialTemporality,
                    Exemplars = exemplars
                };
                return true;

            case MetricStorageTypes.Summary:
                if (record.Count is not { } summaryCount || record.Sum is not { } summarySum ||
                    !IsValidCountAndSum(summaryCount, summarySum) ||
                    !TryReadSummaryQuantiles(record.SummaryQuantilesJson, out var quantiles))
                {
                    return false;
                }

                contract = new SummaryMetricPoint
                {
                    Name = record.MetricName,
                    Description = record.Description,
                    Unit = record.Unit,
                    Metadata = metadata,
                    Attributes = attributes,
                    StartTimeUnixNano = startTimeUnixNano,
                    TimeUnixNano = record.TimeUnixNano,
                    Flags = flags,
                    Resource = resource,
                    ResourceSchemaUrl = record.ResourceSchemaUrl,
                    InstrumentationScope = scope,
                    ScopeSchemaUrl = record.ScopeSchemaUrl,
                    Count = summaryCount,
                    Sum = summarySum,
                    QuantileValues = quantiles
                };
                return true;

            default:
                return false;
        }
    }

    private static bool TryMapNumberValue(
        long? intValue,
        double? doubleValue,
        bool allowAbsent,
        out MetricNumberValue? value)
    {
        value = null;
        if (allowAbsent) return !intValue.HasValue && !doubleValue.HasValue;
        if (intValue.HasValue == doubleValue.HasValue) return false;

        value = intValue is { } integer
            ? new MetricIntegerValue { AsInt = integer }
            : new MetricDoubleValue { AsDouble = doubleValue!.Value };
        return true;
    }

    private static bool TryMapAggregationTemporality(
        byte? stored,
        out AggregationTemporality temporality)
    {
        temporality = stored switch
        {
            1 => AggregationTemporality.Delta,
            2 => AggregationTemporality.Cumulative,
            _ => default
        };
        return stored is 1 or 2;
    }

    private static bool TryMapExemplars(
        string? json,
        out IReadOnlyList<MetricExemplar>? exemplars)
    {
        exemplars = null;
        if (string.IsNullOrWhiteSpace(json)) return true;

        List<MetricExemplarJson>? stored;
        try
        {
            stored = JsonSerializer.Deserialize(json, StorageJsonSerializerContext.Default.MetricExemplarJsonList);
        }
        catch (JsonException)
        {
            return false;
        }

        if (stored is null) return false;
        var mapped = new List<MetricExemplar>(stored.Count);
        foreach (var exemplar in stored)
        {
            if (exemplar.TimeUnixNano is 0 ||
                !TryMapNumberValue(exemplar.IntValue, exemplar.DoubleValue, allowAbsent: false, out var value) ||
                !ContractJson.TryParseAttributes(exemplar.FilteredAttributesJson, out var filteredAttributes))
            {
                return false;
            }

            mapped.Add(new MetricExemplar
            {
                TimeUnixNano = exemplar.TimeUnixNano,
                Value = value!,
                SpanId = exemplar.SpanId,
                TraceId = exemplar.TraceId,
                FilteredAttributes = filteredAttributes
            });
        }

        exemplars = mapped;
        return true;
    }

    private static bool TryReadHistogramBuckets(
        string? json,
        ulong count,
        out MetricHistogramBucketsJson buckets)
    {
        buckets = null!;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            buckets = JsonSerializer.Deserialize(
                json,
                StorageJsonSerializerContext.Default.MetricHistogramBucketsJson)!;
        }
        catch (JsonException)
        {
            return false;
        }

        if (buckets is null ||
            !((buckets.BucketCounts.Length is 0 && buckets.ExplicitBounds.Length is 0) ||
              buckets.BucketCounts.Length == buckets.ExplicitBounds.Length + 1))
        {
            return false;
        }

        var previous = double.NegativeInfinity;
        foreach (var bound in buckets.ExplicitBounds)
        {
            if (!double.IsFinite(bound) || bound <= previous) return false;
            previous = bound;
        }

        return buckets.BucketCounts.Length is 0 ||
               TrySum(buckets.BucketCounts, out var bucketCount) && bucketCount == count;
    }

    private static bool TryReadExponentialHistogramBuckets(
        string? json,
        out MetricExponentialHistogramBucketsJson buckets)
    {
        buckets = null!;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            buckets = JsonSerializer.Deserialize(
                json,
                StorageJsonSerializerContext.Default.MetricExponentialHistogramBucketsJson)!;
            return buckets is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadSummaryQuantiles(
        string? json,
        out IReadOnlyList<SummaryQuantileValue> quantiles)
    {
        quantiles = [];
        if (string.IsNullOrWhiteSpace(json)) return false;

        List<MetricSummaryQuantileJson>? stored;
        try
        {
            stored = JsonSerializer.Deserialize(
                json,
                StorageJsonSerializerContext.Default.MetricSummaryQuantileJsonList);
        }
        catch (JsonException)
        {
            return false;
        }

        if (stored is null) return false;
        var previous = -1d;
        var mapped = new List<SummaryQuantileValue>(stored.Count);
        foreach (var value in stored)
        {
            if (!double.IsFinite(value.Quantile) || value.Quantile is < 0 or > 1 ||
                value.Quantile <= previous || double.IsNaN(value.Value) || value.Value < 0)
            {
                return false;
            }

            mapped.Add(new SummaryQuantileValue { Quantile = value.Quantile, Value = value.Value });
            previous = value.Quantile;
        }

        quantiles = mapped;
        return true;
    }

    private static bool IsValidCountAndSum(ulong count, double? sum) =>
        count is not 0 || sum is null or 0;

    private static bool IsValidMinMax(double? min, double? max) =>
        !(min.HasValue && double.IsNaN(min.Value)) &&
        !(max.HasValue && double.IsNaN(max.Value)) &&
        !(min.HasValue && max.HasValue && min.Value > max.Value);

    private static bool TrySumExponentialBuckets(
        MetricExponentialHistogramBucketsJson buckets,
        ulong zeroCount,
        out ulong count)
    {
        if (!TrySum(buckets.PositiveBucketCounts, out var positive) ||
            !TrySum(buckets.NegativeBucketCounts, out var negative))
        {
            count = 0;
            return false;
        }

        try
        {
            count = checked(zeroCount + positive + negative);
            return true;
        }
        catch (OverflowException)
        {
            count = 0;
            return false;
        }
    }

    private static bool TrySum(IEnumerable<ulong> values, out ulong sum)
    {
        sum = 0;
        try
        {
            foreach (var value in values)
                sum = checked(sum + value);
            return true;
        }
        catch (OverflowException)
        {
            sum = 0;
            return false;
        }
    }
}

internal static class ProfileMapper
{
    public static Profile ToContract(ProfileStorageRow record) =>
        new()
        {
            ProfileId = record.ProfileId,
            TimeUnixNano = record.TimeUnixNano,
            DurationNano = record.DurationNano,
            OriginalPayloadFormat = MapPayloadFormat(record.OriginalPayloadFormat),
            Attributes = ContractJson.ParseAttributes(record.AttributesJson),
            Resource = new Resource
            {
                ServiceName = ResourceMapping.ServiceNameOrUnknown(record.ServiceName),
                Attributes = ContractJson.ParseAttributes(record.ResourceJson)
            },
            InstrumentationScope = string.IsNullOrWhiteSpace(record.SchemaUrl)
                ? null
                : new InstrumentationScope { ScopeName = record.SchemaUrl }
        };

    public static Profile ToContract(ProfileDetail detail)
    {
        var stringTable = new List<string>();
        int? AddString(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var index = stringTable.IndexOf(value);
            if (index >= 0)
                return index;

            stringTable.Add(value);
            return stringTable.Count - 1;
        }

        ProfileValueType? AddValueType(string? type, string? unit)
        {
            var typeStrindex = AddString(type);
            var unitStrindex = AddString(unit);

            return typeStrindex is null && unitStrindex is null
                ? null
                : new ProfileValueType
                {
                    TypeStrindex = typeStrindex,
                    UnitStrindex = unitStrindex
                };
        }

        var linkIndexes = new Dictionary<int, int>();
        var links = new List<ProfileLink>();
        foreach (var row in detail.Samples)
        {
            if (row.LinkTraceId is null && row.LinkSpanId is null)
                continue;

            linkIndexes[row.Ordinal] = links.Count;
            links.Add(new ProfileLink { TraceId = row.LinkTraceId, SpanId = row.LinkSpanId });
        }

        var functions = detail.Functions.Select(row => new ProfileFunction
        {
            NameStrindex = AddString(row.Name),
            SystemNameStrindex = AddString(row.SystemName),
            FilenameStrindex = AddString(row.Filename),
            StartLine = row.StartLine
        }).ToArray();
        var locations = detail.Locations.Select(static row => new ProfileLocation
        {
            MappingIndex = row.MappingOrdinal,
            Address = row.Address,
            Lines = ParseProfileLines(row.LinesJson)
        }).ToArray();
        var mappings = detail.Mappings.Select(row => new ProfileMapping
        {
            MemoryStart = row.MemoryStart,
            MemoryLimit = row.MemoryLimit,
            FileOffset = row.FileOffset,
            FilenameStrindex = AddString(row.Filename)
        }).ToArray();
        var samples = detail.Samples.Select(row => new ProfileSample
        {
            StackIndex = row.StackOrdinal,
            Values = ParseLongList(row.ValuesJson),
            TimestampsUnixNano = ParseUlongList(row.TimestampsJson),
            LinkIndex = linkIndexes.TryGetValue(row.Ordinal, out var linkIndex) ? linkIndex : null
        }).ToArray();
        var stacks = detail.Stacks.Select(static row => new ProfileStack
        {
            LocationIndices = ParseIntList(row.LocationOrdinalsJson)
        }).ToArray();

        return new Profile
        {
            ProfileId = detail.Profile.ProfileId,
            SampleType = AddValueType(detail.Profile.SampleType, detail.Profile.SampleUnit),
            TimeUnixNano = detail.Profile.TimeUnixNano,
            DurationNano = detail.Profile.DurationNano,
            OriginalPayloadFormat = MapPayloadFormat(detail.Profile.OriginalPayloadFormat),
            Attributes = ContractJson.ParseAttributes(detail.Profile.AttributesJson),
            Resource = new Resource
            {
                ServiceName = string.IsNullOrWhiteSpace(detail.Profile.ServiceName) ? "unknown" : detail.Profile.ServiceName,
                Attributes = ContractJson.ParseAttributes(detail.Profile.ResourceJson)
            },
            InstrumentationScope = string.IsNullOrWhiteSpace(detail.Profile.SchemaUrl)
                ? null
                : new InstrumentationScope { ScopeName = detail.Profile.SchemaUrl },
            StringTable = stringTable,
            FunctionTable = functions,
            LocationTable = locations,
            MappingTable = mappings,
            Samples = samples,
            StackTable = stacks,
            LinkTable = links
        };
    }

    public static IReadOnlyList<Profile> ToContracts(IReadOnlyList<ProfileStorageRow> records) =>
        [.. records.Select(static record => ToContract(record))];

    private static OriginalPayloadFormat? MapPayloadFormat(string? format) =>
        Enum.TryParse<OriginalPayloadFormat>(format, ignoreCase: true, out var parsed) ? parsed : null;

    private static IReadOnlyList<ProfileLine>? ParseProfileLines(string? json) =>
        ParseJsonArray(json, static element => new ProfileLine
        {
            FunctionIndex = element.TryGetProperty("functionIndex", out var functionIndex) && functionIndex.TryGetInt32(out var fi)
                ? fi
                : element.TryGetProperty("functionOrdinal", out var functionOrdinal) && functionOrdinal.TryGetInt32(out var fo)
                    ? fo
                    : null,
            Line = element.TryGetProperty("line", out var line) && line.TryGetInt64(out var l) ? l : null,
            Column = element.TryGetProperty("column", out var column) && column.TryGetInt64(out var c) ? c : null
        });

    private static IReadOnlyList<int>? ParseIntList(string? json) =>
        ParseJsonArray(json, static element => element.TryGetInt32(out var value) ? value : 0);

    private static IReadOnlyList<long>? ParseLongList(string? json) =>
        ParseJsonArray(json, static element => element.TryGetInt64(out var value) ? value : 0L);

    private static IReadOnlyList<ulong>? ParseUlongList(string? json) =>
        ParseJsonArray(json, static element => element.TryGetUInt64(out var value) ? value : 0UL);

    private static IReadOnlyList<T>? ParseJsonArray<T>(string? json, Func<JsonElement, T> map)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Array)
                return null;

            return [.. document.RootElement.EnumerateArray().Select(map)];
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal static class SessionMapper
{
    public static SessionEntity ToContract(SessionQueryRow summary)
    {
        var startTime = AsUtcOffset(summary.StartTime);
        var lastActivity = AsUtcOffset(summary.LastActivity);
        var isActive = TimeProvider.System.GetUtcNow() - lastActivity < QylSessionActivity.ActiveWindow;

        return new SessionEntity
        {
            SessionId = summary.SessionId,
            StartTime = startTime,
            EndTime = isActive ? null : lastActivity,
            DurationMs = summary.DurationMs,
            SpanCount = ToInt32(summary.SpanCount),
            TraceCount = ToInt32(summary.TraceCount),
            ErrorCount = ToInt32(summary.ErrorCount),
            Services = [.. summary.Services],
            State = isActive ? SessionState.Active : SessionState.Ended,
            GenaiUsage = new SessionGenAiUsage
            {
                RequestCount = ToInt32(summary.GenAiRequestCount),
                TotalInputTokens = summary.InputTokens,
                TotalOutputTokens = summary.OutputTokens,
                ModelsUsed = [.. summary.Models],
                ProvidersUsed = [.. summary.Providers]
            }
        };
    }

    private static List<SessionEntity> ToContracts(IReadOnlyList<SessionQueryRow> summaries) =>
        [.. summaries.Select(ToContract)];

    public static CursorPageSessionEntity ToPage(
        IReadOnlyList<SessionQueryRow> summaries,
        bool hasMore,
        string? previousCursor = null,
        string? nextCursor = null)
    {
        return new CursorPageSessionEntity
        {
            Items = ToContracts(summaries),
            HasMore = hasMore,
            PrevCursor = previousCursor,
            NextCursor = nextCursor
        };
    }

    private static int ToInt32(long value) =>
        value switch
        {
            > int.MaxValue => int.MaxValue,
            _ => (int)value
        };

    private static DateTimeOffset AsUtcOffset(DateTime timestamp) =>
        new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
}
