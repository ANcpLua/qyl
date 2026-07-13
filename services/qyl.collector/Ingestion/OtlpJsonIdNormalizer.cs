using System.Text;

namespace Qyl.Collector.Ingestion;

/// <summary>
/// Rewrites the hex-encoded id fields of an OTLP/JSON payload to base64 so the protojson parser
/// yields the bytes the sender meant.
/// </summary>
/// <remarks>
/// The OTLP spec special-cases trace/span/profile ids in JSON to hex, while proto3's JSON mapping
/// (which <c>Google.Protobuf.JsonParser</c> implements) decodes every <c>bytes</c> field as base64.
/// Without this rewrite a spec-compliant hex id happens to also be valid base64 and silently
/// decodes to the wrong bytes — mangled, unjoinable ids. The contract is strict spec-hex: a
/// non-hex or wrong-length id throws <see cref="InvalidDataException"/>, which the ingest
/// endpoints surface as a 400, never a mangled row.
/// </remarks>
internal static class OtlpJsonIdNormalizer
{
    private const int TraceIdBytes = 16;
    private const int SpanIdBytes = 8;
    private const int ProfileIdBytes = 16;

    public static string NormalizeIdsToProtoJson(ReadOnlySpan<byte> utf8Json)
    {
        // The previous StreamReader-based path tolerated a UTF-8 BOM; keep that tolerance.
        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (utf8Json.StartsWith(utf8Bom))
            utf8Json = utf8Json[utf8Bom.Length..];

        if (utf8Json.IsEmpty)
            throw new InvalidDataException("OTLP/JSON payload is empty.");

        var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { MaxDepth = 128 });
        var buffer = new ArrayBufferWriter<byte>(utf8Json.Length);
        using var writer = new Utf8JsonWriter(buffer);

        // Protojson must ignore unknown fields, including future objects that happen to contain
        // properties named traceId/spanId/profileId. Track the containing OTLP message path so
        // only ids owned by known signal shapes are validated and rewritten.
        var path = new List<string>(8);
        var containerSegments = new Stack<bool>();
        string? pendingProperty = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    var name = reader.GetString()!;
                    writer.WritePropertyName(name);
                    pendingProperty = name;
                    continue;

                case JsonTokenType.String:
                    var expectedBytes = pendingProperty is { } property
                        ? ExpectedIdByteLength(path, property)
                        : 0;
                    if (expectedBytes > 0)
                        WriteIdAsBase64(writer, reader.GetString()!, expectedBytes, pendingProperty!);
                    else
                        writer.WriteStringValue(reader.GetString());
                    break;

                case JsonTokenType.Number:
                    writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
                    break;

                case JsonTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;

                case JsonTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;

                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;

                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    PushContainerPath(path, containerSegments, ref pendingProperty);
                    break;

                case JsonTokenType.EndObject:
                    writer.WriteEndObject();
                    PopContainerPath(path, containerSegments);
                    break;

                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    PushContainerPath(path, containerSegments, ref pendingProperty);
                    break;

                case JsonTokenType.EndArray:
                    writer.WriteEndArray();
                    PopContainerPath(path, containerSegments);
                    break;
            }

            pendingProperty = null;
        }

        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void PushContainerPath(
        List<string> path,
        Stack<bool> containerSegments,
        ref string? pendingProperty)
    {
        var addsSegment = pendingProperty is not null;
        if (addsSegment)
            path.Add(pendingProperty!);

        containerSegments.Push(addsSegment);
        pendingProperty = null;
    }

    private static void PopContainerPath(List<string> path, Stack<bool> containerSegments)
    {
        if (containerSegments.Pop())
            path.RemoveAt(path.Count - 1);
    }

    // protojson accepts both lowerCamelCase JSON names and original proto field names.
    private static int ExpectedIdByteLength(IReadOnlyList<string> path, string propertyName)
    {
        if (IsTraceSpanPath(path))
        {
            if (IsName(propertyName, "traceId", "trace_id")) return TraceIdBytes;
            if (IsName(propertyName, "spanId", "span_id") ||
                IsName(propertyName, "parentSpanId", "parent_span_id"))
                return SpanIdBytes;
        }

        if (IsTraceLinkPath(path) || IsLogRecordPath(path) || IsProfileLinkPath(path))
        {
            if (IsName(propertyName, "traceId", "trace_id")) return TraceIdBytes;
            if (IsName(propertyName, "spanId", "span_id")) return SpanIdBytes;
        }

        return IsProfilePath(path) && IsName(propertyName, "profileId", "profile_id")
            ? ProfileIdBytes
            : 0;
    }

    private static bool IsTraceSpanPath(IReadOnlyList<string> path) =>
        path.Count is 3 &&
        IsName(path[0], "resourceSpans", "resource_spans") &&
        IsName(path[1], "scopeSpans", "scope_spans") &&
        IsName(path[2], "spans", "spans");

    private static bool IsTraceLinkPath(IReadOnlyList<string> path) =>
        path.Count is 4 &&
        IsTraceSpanPathPrefix(path) &&
        IsName(path[3], "links", "links");

    private static bool IsTraceSpanPathPrefix(IReadOnlyList<string> path) =>
        IsName(path[0], "resourceSpans", "resource_spans") &&
        IsName(path[1], "scopeSpans", "scope_spans") &&
        IsName(path[2], "spans", "spans");

    private static bool IsLogRecordPath(IReadOnlyList<string> path) =>
        path.Count is 3 &&
        IsName(path[0], "resourceLogs", "resource_logs") &&
        IsName(path[1], "scopeLogs", "scope_logs") &&
        IsName(path[2], "logRecords", "log_records");

    private static bool IsProfilePath(IReadOnlyList<string> path) =>
        path.Count is 3 &&
        IsName(path[0], "resourceProfiles", "resource_profiles") &&
        IsName(path[1], "scopeProfiles", "scope_profiles") &&
        IsName(path[2], "profiles", "profiles");

    private static bool IsProfileLinkPath(IReadOnlyList<string> path) =>
        path.Count is 2 &&
        IsName(path[0], "dictionary", "dictionary") &&
        IsName(path[1], "linkTable", "link_table");

    private static bool IsName(string actual, string jsonName, string protoName) =>
        string.Equals(actual, jsonName, StringComparison.Ordinal) ||
        string.Equals(actual, protoName, StringComparison.Ordinal);

    private static void WriteIdAsBase64(Utf8JsonWriter writer, string hex, int expectedBytes, string field)
    {
        // Empty means absent (e.g. a root span's parentSpanId); protojson maps it to empty bytes.
        if (hex.Length is 0)
        {
            writer.WriteStringValue(hex);
            return;
        }

        if (hex.Length != expectedBytes * 2)
        {
            throw new InvalidDataException(
                $"OTLP/JSON field '{field}' must be {expectedBytes * 2} hex characters ({expectedBytes} bytes); got {hex.Length} characters.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            throw new InvalidDataException(
                $"OTLP/JSON field '{field}' must be hex-encoded per the OTLP spec; got a non-hex value.");
        }

        writer.WriteBase64StringValue(bytes);
    }
}
