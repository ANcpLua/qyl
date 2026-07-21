using Qyl.Api.Contracts.Common;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Traces;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;
using Resource = Qyl.Api.Contracts.OTel.Resource.Resource;
using TraceContract = Qyl.Api.Contracts.OTel.Traces.Trace;

namespace Qyl.Collector.Mapping;

internal static class ContractJson
{
    public static IReadOnlyList<ContractAttribute>? ParseAttributes(string? json)
    {
        if (TryParseAttributes(json, out var attributes)) return attributes;
        throw new InvalidDataException("Stored attributes do not match the generated AttributeValue contract.");
    }

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
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!keys.Add(property.Name) || !TryReadValue(property.Value, out var value)) return false;
                parsed.Add(new ContractAttribute { Key = property.Name, Value = value });
            }

            attributes = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadValue(JsonElement value, out object? result)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                result = value.GetString() ?? "";
                return true;
            case JsonValueKind.True:
                result = true;
                return true;
            case JsonValueKind.False:
                result = false;
                return true;
            case JsonValueKind.Null:
                // Attribute.Value is required even for an empty OTLP AnyValue. Keeping an explicit
                // null JsonElement prevents the context-wide WhenWritingNull policy from omitting it.
                result = value.Clone();
                return true;
            case JsonValueKind.Array:
            {
                var items = new object?[value.GetArrayLength()];
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (!TryReadValue(item, out items[index]))
                    {
                        result = null;
                        return false;
                    }

                    index++;
                }

                result = items;
                return true;
            }
            case JsonValueKind.Object:
                return TryReadTaggedValue(value, out result);
            default:
                // Untagged JSON numbers are ambiguous: an OTLP int64 and a finite double can have
                // the same JSON token. The generated contract therefore requires tagged wrappers.
                result = null;
                return false;
        }
    }

    private static bool TryReadTaggedValue(JsonElement value, out object? result)
    {
        result = null;
        if (!value.TryGetProperty("type", out var typeProperty) ||
            typeProperty.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        switch (typeProperty.GetString())
        {
            case "bytes":
                if (!HasExactProperties(value, "type", "base64") ||
                    !value.TryGetProperty("base64", out var base64Property) ||
                    base64Property.ValueKind is not JsonValueKind.String)
                {
                    return false;
                }

                try
                {
                    result = new AttributeBytesValue
                    {
                        Type = "bytes",
                        Base64 = Convert.FromBase64String(base64Property.GetString() ?? "")
                    };
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }

            case "int":
                if (!HasExactProperties(value, "type", "value") ||
                    !value.TryGetProperty("value", out var integerProperty) ||
                    integerProperty.ValueKind is not JsonValueKind.String ||
                    !long.TryParse(
                        integerProperty.GetString(),
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out var integer))
                {
                    return false;
                }

                result = new AttributeIntValue { Type = "int", Value = integer };
                return true;

            case "double":
                if (!HasExactProperties(value, "type", "value") ||
                    !value.TryGetProperty("value", out var doubleProperty) ||
                    !TryReadDouble(doubleProperty, out var number))
                {
                    return false;
                }

                result = new AttributeDoubleValue { Type = "double", Value = number };
                return true;

            case "kvlist":
                if (!HasExactProperties(value, "type", "values") ||
                    !value.TryGetProperty("values", out var valuesProperty) ||
                    valuesProperty.ValueKind is not JsonValueKind.Object ||
                    !TryReadValueDictionary(valuesProperty, out var values))
                {
                    return false;
                }

                result = new AttributeKeyValueListValue { Type = "kvlist", Values = values };
                return true;

            default:
                return false;
        }
    }

    private static bool TryReadValueDictionary(
        JsonElement value,
        out IReadOnlyDictionary<string, object?> values)
    {
        var parsed = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!parsed.TryAdd(property.Name, null) || !TryReadValue(property.Value, out var item))
            {
                values = null!;
                return false;
            }

            parsed[property.Name] = item;
        }

        values = parsed;
        return true;
    }

    private static bool TryReadDouble(JsonElement value, out double number)
    {
        if (value.ValueKind is JsonValueKind.Number && value.TryGetDouble(out number)) return true;
        if (value.ValueKind is JsonValueKind.String)
        {
            number = value.GetString() switch
            {
                "NaN" => double.NaN,
                "Infinity" => double.PositiveInfinity,
                "-Infinity" => double.NegativeInfinity,
                _ => 0
            };
            return value.GetString() is "NaN" or "Infinity" or "-Infinity";
        }

        number = 0;
        return false;
    }

    private static bool HasExactProperties(JsonElement value, params string[] expected)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name)) return false;
        }

        return names.SetEquals(expected);
    }
}

internal static class ResourceMapping
{
    public static string ServiceNameOrUnknown(string? serviceName) =>
        string.IsNullOrWhiteSpace(serviceName) ? "unknown" : serviceName;

    public static IReadOnlyList<EntityRef>? ParseEntityRefs(
        string? json,
        IReadOnlyList<ContractAttribute>? resourceAttributes)
    {
        if (TryParseEntityRefs(json, resourceAttributes, out var entityRefs)) return entityRefs;
        throw new InvalidDataException("Stored Resource entity references are invalid.");
    }

    public static IReadOnlyList<EntityRef>? ParseEntityRefs(string? json, string? resourceJson) =>
        ParseEntityRefs(json, ContractJson.ParseAttributes(resourceJson));

    public static bool TryParseEntityRefs(
        string? json,
        IReadOnlyList<ContractAttribute>? resourceAttributes,
        out IReadOnlyList<EntityRef>? entityRefs)
    {
        entityRefs = null;
        if (string.IsNullOrWhiteSpace(json)) return true;

        List<ResourceEntityRefIngestionRecord>? stored;
        try
        {
            stored = JsonSerializer.Deserialize(
                json,
                StorageJsonSerializerContext.Default.ResourceEntityRefIngestionRecordList);
        }
        catch (JsonException)
        {
            return false;
        }

        if (stored is null) return false;
        var resourceKeys = resourceAttributes?.Select(static attribute => attribute.Key)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        if (resourceAttributes is not null && resourceKeys.Count != resourceAttributes.Count) return false;

        var mapped = new List<EntityRef>(stored.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        string? previousIdentity = null;
        foreach (var entityRef in stored)
        {
            if (entityRef is null ||
                entityRef.SchemaUrl is "" ||
                string.IsNullOrWhiteSpace(entityRef.Type) ||
                entityRef.IdKeys is not { Count: > 0 } idKeys ||
                entityRef.DescriptionKeys is not { } descriptionKeys ||
                !IsCanonicalKeyList(idKeys) ||
                !IsCanonicalKeyList(descriptionKeys) ||
                idKeys.Any(key => !resourceKeys.Contains(key)) ||
                descriptionKeys.Any(key => !resourceKeys.Contains(key)))
            {
                return false;
            }

            var identity = GetIdentity(entityRef.Type, idKeys);
            if (!identities.Add(identity) ||
                previousIdentity is not null && StringComparer.Ordinal.Compare(previousIdentity, identity) >= 0)
            {
                return false;
            }

            previousIdentity = identity;
            mapped.Add(new EntityRef
            {
                SchemaUrl = entityRef.SchemaUrl,
                Type = entityRef.Type,
                IdKeys = idKeys,
                DescriptionKeys = descriptionKeys.Count is 0 ? null : descriptionKeys
            });
        }

        entityRefs = mapped.Count is 0 ? null : mapped;
        return true;
    }

    private static bool IsCanonicalKeyList(IReadOnlyList<string> keys)
    {
        string? previous = null;
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                previous is not null && StringComparer.Ordinal.Compare(previous, key) >= 0)
            {
                return false;
            }

            previous = key;
        }

        return true;
    }

    private static string GetIdentity(string type, IReadOnlyList<string> idKeys)
    {
        var builder = new StringBuilder();
        AppendSegment(builder, type);
        builder.Append(idKeys.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var key in idKeys)
            AppendSegment(builder, key);
        return builder.ToString();
    }

    private static void AppendSegment(StringBuilder builder, string value) =>
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
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
            r.AttributesJson, r.ResourceJson, r.ResourceEntityRefsJson, r.SchemaUrl,
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
        string? attributesJson, string? resourceJson, string? resourceEntityRefsJson, string? schemaUrl,
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
                Attributes = resourceAttributes,
                EntityRefs = ResourceMapping.ParseEntityRefs(resourceEntityRefsJson, resourceAttributes)
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
                Attributes = ContractJson.ParseAttributes(record.ResourceJson),
                EntityRefs = ResourceMapping.ParseEntityRefs(record.ResourceEntityRefsJson, record.ResourceJson)
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
