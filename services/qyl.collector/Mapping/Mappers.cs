using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Otel;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Profiles;
using Qyl.Api.Contracts.OTel.Traces;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;
using TraceContract = Qyl.Api.Contracts.OTel.Traces.Trace;

namespace Qyl.Collector.Mapping;

internal static class ContractJson
{
    public static IReadOnlyList<ContractAttribute>? ParseAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return null;

            var attributes = new List<ContractAttribute>();
            foreach (var property in document.RootElement.EnumerateObject())
                attributes.Add(new ContractAttribute { Key = property.Name, Value = ReadValue(property.Value) ?? "" });

            return attributes;
        }
        catch (JsonException)
        {
            return null;
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
            JsonValueKind.Null => null,
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

internal static class SpanMapper
{
    public static Span ToContract(SpanStorageRow record, string serviceName, string? serviceVersion = null) =>
        ToContractCore(
            record.TraceId, record.SpanId, record.ParentSpanId, record.SessionId,
            record.Name, record.Kind, record.StatusCode,
            record.StartTimeUnixNano, record.EndTimeUnixNano, record.DurationNs,
            serviceName, serviceVersion,
            record.AttributesJson, record.ResourceJson, record.SchemaUrl);

    public static List<Span> ToContracts(
        IEnumerable<SpanStorageRow> records,
        Func<SpanStorageRow, (string ServiceName, string? ServiceVersion)> serviceResolver) =>
    [
        .. records.Select(r =>
        {
            var (serviceName, serviceVersion) = serviceResolver(r);
            return ToContract(r, serviceName, serviceVersion);
        })
    ];

    public static TraceContract ToTrace(string traceId, IReadOnlyList<Span> spans)
    {
        var rootSpan = spans.FirstOrDefault(static s => s.ParentSpanId is null);
        var start = spans.Count is 0 ? 0UL : spans.Min(static s => s.StartTimeUnixNano);
        var end = spans.Count is 0 ? 0UL : spans.Max(static s => s.EndTimeUnixNano);
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

    private static Span ToContractCore(
        string traceId, string spanId, string? parentSpanId, string? sessionId,
        string name, byte kind, byte statusCode,
        ulong startTimeUnixNano, ulong endTimeUnixNano, ulong durationNs,
        string serviceName, string? serviceVersion,
        string? attributesJson, string? resourceJson, string? schemaUrl)
    {
        _ = sessionId;
        _ = durationNs;
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
            Events = [],
            Links = [],
            Status = new SpanStatus
            {
                Code = MapStatus(statusCode)
            },
            Resource = new Resource
            {
                ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "unknown" : serviceName,
                ServiceVersion = serviceVersion,
                Attributes = resourceAttributes
            },
            InstrumentationScope = schemaUrl is null
                ? null
                : new InstrumentationScope { ScopeName = schemaUrl, ScopeVersion = serviceVersion }
        };
    }
}

internal static class LogMapper
{
    public static LogRecord ToContract(LogStorageRow record, string? bodyOverride = null) =>
        new()
        {
            TimeUnixNano = record.TimeUnixNano,
            ObservedTimeUnixNano = record.ObservedTimeUnixNano ?? record.TimeUnixNano,
            SeverityNumber = MapSeverityNumber(record.SeverityNumber),
            SeverityText = MapSeverityText(record.SeverityText, record.SeverityNumber),
            Body = bodyOverride ?? record.Body ?? "",
            Attributes = ContractJson.ParseAttributes(record.AttributesJson),
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            Resource = new Resource
            {
                ServiceName = string.IsNullOrWhiteSpace(record.ServiceName) ? "unknown" : record.ServiceName,
                Attributes = ContractJson.ParseAttributes(record.ResourceJson)
            }
        };

    public static IReadOnlyList<LogRecord> ToContracts(IEnumerable<LogStorageRow> records) =>
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
                ServiceName = string.IsNullOrWhiteSpace(record.ServiceName) ? "unknown" : record.ServiceName,
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
        var mappings = detail.Mappings.Select(static row => new ProfileMapping
        {
            MemoryStart = row.MemoryStart,
            MemoryLimit = row.MemoryLimit,
            FileOffset = row.FileOffset
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

    public static IReadOnlyList<Profile> ToContracts(IEnumerable<ProfileStorageRow> records) =>
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

internal static class AttributeParsing
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? ParseNullableLong(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.TryParseInt64();
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
                ProvidersUsed = [.. summary.Providers],
                EstimatedCostUsd = summary.TotalCostUsd
            }
        };
    }

    private static List<SessionEntity> ToContracts(IEnumerable<SessionQueryRow> summaries) =>
        [.. summaries.Select(ToContract)];

    public static CursorPageSessionEntity ToPage(
        IEnumerable<SessionQueryRow> summaries,
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
            < int.MinValue => int.MinValue,
            _ => (int)value
        };

    private static DateTimeOffset AsUtcOffset(DateTime timestamp) =>
        new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
}
