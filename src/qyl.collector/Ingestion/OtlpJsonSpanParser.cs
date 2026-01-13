// =============================================================================
// qyl OTLP Ingestion - Zero-Allocation JSON Parser
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using System.Buffers.Text;

namespace qyl.collector.Ingestion;

/// <summary>
///     High-performance OTLP/JSON span parser. Zero allocation on hot path.
///     Designed for streaming ingestion via System.Threading.Channels.
/// </summary>
public ref struct OtlpJsonSpanParser
{
    private Utf8JsonReader _reader;

    // String interning for common attribute values
    private static readonly FrozenDictionary<ulong, string> InternedStrings = CreateInternedStrings();

    private static FrozenDictionary<ulong, string> CreateInternedStrings()
    {
        var dict = new Dictionary<ulong, string>();
        // Intern common provider names
        foreach (var p in (ReadOnlySpan<string>)["openai", "anthropic", "gcp.gemini", "aws.bedrock", "azure.openai"])
            dict[ComputeHash(p)] = p;

        // Intern common operation names
        foreach (var o in (ReadOnlySpan<string>)["chat", "text_completion", "embeddings", "invoke_agent"])
            dict[ComputeHash(o)] = o;

        // Intern common model prefixes
        foreach (var m in (ReadOnlySpan<string>)
                 ["gpt-4", "gpt-4o", "gpt-3.5-turbo", "claude-3", "claude-sonnet", "gemini-pro"])
            dict[ComputeHash(m)] = m;

        return dict.ToFrozenDictionary();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeHash(ReadOnlySpan<byte> utf8) => XxHash3.HashToUInt64(utf8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeHash(string s) => XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(s));

    public OtlpJsonSpanParser(ReadOnlySpan<byte> json) =>
        _reader = new Utf8JsonReader(json,
            new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

    public OtlpJsonSpanParser(ReadOnlySequence<byte> json) =>
        _reader = new Utf8JsonReader(json,
            new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

    /// <summary>
    ///     Parse OTLP ExportTraceServiceRequest JSON into spans.
    ///     Returns all parsed spans as a list.
    /// </summary>
    public List<ParsedSpan> ParseExportRequest()
    {
        List<ParsedSpan> results = [];

        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return results;

        while (_reader.Read())
        {
            if (_reader.TokenType == JsonTokenType.EndObject) break;

            if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                if (_reader.ValueTextEquals("resourceSpans"u8))
                    ParseResourceSpans(results);
                else
                    _reader.Skip();
            }
        }

        return results;
    }

    private void ParseResourceSpans(List<ParsedSpan> results)
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
        {
            if (_reader.TokenType == JsonTokenType.StartObject)
                ParseResourceSpan(results);
        }
    }

    private void ParseResourceSpan(List<ParsedSpan> results)
    {
        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                if (_reader.ValueTextEquals("resource"u8))
                    _ = ParseResourceServiceName(); // Parse but don't use - service name extracted at resource level
                else if (_reader.ValueTextEquals("scopeSpans"u8))
                    ParseScopeSpans(results);
                else
                    _reader.Skip();
            }
        }
    }

    private string? ParseResourceServiceName()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return null;

        string? serviceName = null;
        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName &&
                _reader.ValueTextEquals("attributes"u8))
            {
                serviceName = ExtractServiceNameFromAttributes();
                // Continue to consume the rest of the resource object
            }
            else if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                _reader.Skip();
            }
        }

        return serviceName;
    }

    private string? ExtractServiceNameFromAttributes()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return null;

        string? serviceName = null;
        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
        {
            if (_reader.TokenType == JsonTokenType.StartObject)
            {
                string? key = null;
                string? value = null;

                while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
                {
                    if (_reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (_reader.ValueTextEquals("key"u8))
                        {
                            _reader.Read();
                            if (_reader.ValueTextEquals("service.name"u8)) key = "service.name";
                        }
                        else if (_reader.ValueTextEquals("value"u8) && key == "service.name")
                            value = ParseAnyValue();
                        else
                            _reader.Skip();
                    }
                }

                if (key == "service.name" && value is not null)
                    serviceName = value;
                // Continue to consume remaining attributes
            }
        }

        return serviceName;
    }

    private void ParseScopeSpans(List<ParsedSpan> results)
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
        {
            if (_reader.TokenType == JsonTokenType.StartObject)
                ParseScopeSpan(results);
        }
    }

    private void ParseScopeSpan(List<ParsedSpan> results)
    {
        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                if (_reader.ValueTextEquals("spans"u8))
                    ParseSpanArray(results);
                else
                    _reader.Skip();
            }
        }
    }

    private void ParseSpanArray(List<ParsedSpan> results)
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
        {
            if (_reader.TokenType == JsonTokenType.StartObject)
            {
                var span = ParseSingleSpan();
                if (span is not null) results.Add(span);
            }
        }
    }

    private ParsedSpan? ParseSingleSpan()
    {
        var span = new ParsedSpan();

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            var propName = _reader.ValueSpan;

            if (propName.SequenceEqual("traceId"u8))
            {
                _reader.Read();
                if (_reader.TokenType == JsonTokenType.String)
                {
                    var valueSpan = _reader.ValueSpan;
                    if (TraceId.TryParse(valueSpan, null, out var traceId)) span.TraceId = traceId;
                }
            }
            else if (propName.SequenceEqual("spanId"u8))
            {
                _reader.Read();
                if (_reader.TokenType == JsonTokenType.String)
                {
                    var valueSpan = _reader.ValueSpan;
                    if (SpanId.TryParse(valueSpan, null, out var spanId)) span.SpanId = spanId;
                }
            }
            else if (propName.SequenceEqual("parentSpanId"u8))
            {
                _reader.Read();
                if (_reader.TokenType == JsonTokenType.String)
                {
                    var valueSpan = _reader.ValueSpan;
                    if (SpanId.TryParse(valueSpan, null, out var parentId)) span.ParentSpanId = parentId;
                }
            }
            else if (propName.SequenceEqual("name"u8))
            {
                _reader.Read();
                span.Name = GetInternedString(_reader.ValueSpan);
            }
            else if (propName.SequenceEqual("kind"u8))
            {
                _reader.Read();
                span.Kind = _reader.TokenType == JsonTokenType.Number
                    ? (SpanKind)_reader.GetInt32()
                    : SpanKind.Unspecified;
            }
            else if (propName.SequenceEqual("startTimeUnixNano"u8))
            {
                _reader.Read();
                span.StartTime = ParseUnixNano();
            }
            else if (propName.SequenceEqual("endTimeUnixNano"u8))
            {
                _reader.Read();
                span.EndTime = ParseUnixNano();
            }
            else if (propName.SequenceEqual("status"u8))
                ParseStatus(span);
            else if (propName.SequenceEqual("attributes"u8))
                ParseSpanAttributes(span);
            else
                _reader.Skip();
        }

        return span.TraceId.IsEmpty ? null : span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UnixNano ParseUnixNano()
    {
        // OTel spec: timestamps are fixed64 (unsigned 64-bit)
        if (_reader.TokenType == JsonTokenType.Number)
        {
            // Try ulong first (correct per OTel spec), fall back to long for compatibility
            if (_reader.TryGetUInt64(out var ulongValue))
                return new UnixNano(ulongValue);
            // Fallback: some producers may serialize as signed
            if (_reader.TryGetInt64(out var longValue) && longValue >= 0)
                return new UnixNano((ulong)longValue);
            return UnixNano.Empty;
        }

        if (_reader.TokenType == JsonTokenType.String)
        {
            // OTLP JSON encodes large numbers as strings
            var span = _reader.ValueSpan;
            // Try unsigned first (correct per OTel spec)
            if (Utf8Parser.TryParse(span, out ulong ulongValue, out _))
                return new UnixNano(ulongValue);
        }

        return UnixNano.Empty;
    }

    private void ParseStatus(ParsedSpan span)
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                if (_reader.ValueTextEquals("code"u8))
                {
                    _reader.Read();
                    span.Status = _reader.TokenType == JsonTokenType.Number
                        ? (StatusCode)_reader.GetInt32()
                        : StatusCode.Unset;
                }
                else if (_reader.ValueTextEquals("message"u8))
                {
                    _reader.Read();
                    span.StatusMessage = _reader.GetString();
                }
                else
                    _reader.Skip();
            }
        }
    }

    private void ParseSpanAttributes(ParsedSpan span)
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
        {
            if (_reader.TokenType == JsonTokenType.StartObject)
                ParseSingleAttribute(span);
        }
    }

    private void ParseSingleAttribute(ParsedSpan span)
    {
        ReadOnlySpan<byte> keySpan = default;
        string? key = null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            if (_reader.ValueTextEquals("key"u8))
            {
                _reader.Read();
                keySpan = _reader.HasValueSequence
                    ? _reader.ValueSequence.ToArray()
                    : _reader.ValueSpan;
                key = null;
            }
            else if (_reader.ValueTextEquals("value"u8))
            {
                if (keySpan.IsEmpty)
                {
                    _reader.Skip();
                    continue;
                }

                if (OtlpGenAiAttributes.IsGenAiAttribute(keySpan))
                    ParseGenAiAttributeValue(span, keySpan);
                // Legacy agents.* prefix - normalize to gen_ai.agent.*/gen_ai.tool.*
                else if (keySpan.StartsWith("agents."u8))
                    ParseLegacyAgentsAttributeValue(span, keySpan);
                else if (keySpan.SequenceEqual("session.id"u8))
                {
                    var value = ParseAnyValue();
                    if (value is string s && Guid.TryParse(s, out var guid))
                        span.SessionId = new SessionId(guid);
                }
                else
                {
                    key ??= Encoding.UTF8.GetString(keySpan);
                    var value = ParseAnyValueAsObject();
                    span.Attributes ??= [];
                    span.Attributes.Add(new KeyValuePair<string, object?>(key, value));
                }
            }
            else
                _reader.Skip();
        }
    }

    private void ParseGenAiAttributeValue(ParsedSpan span, ReadOnlySpan<byte> keySpan)
    {
        if (keySpan.SequenceEqual(OtlpGenAiAttributes.ProviderName) ||
            keySpan.SequenceEqual(OtlpGenAiAttributes.DeprecatedSystem))
            span.ProviderName = GetInternedString(ParseAnyValueSpan());
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.RequestModel))
            span.RequestModel = GetInternedString(ParseAnyValueSpan());
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.ResponseModel))
            span.ResponseModel = GetInternedString(ParseAnyValueSpan());
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.OperationName))
            span.OperationName = GetInternedString(ParseAnyValueSpan());
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.InputTokens) ||
                 keySpan.SequenceEqual(OtlpGenAiAttributes.DeprecatedPromptTokens))
            span.InputTokens = ParseAnyValueAsLong();
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.OutputTokens) ||
                 keySpan.SequenceEqual(OtlpGenAiAttributes.DeprecatedCompletionTokens))
            span.OutputTokens = ParseAnyValueAsLong();
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.RequestTemperature))
            span.Temperature = ParseAnyValueAsDouble();
        else if (keySpan.SequenceEqual(OtlpGenAiAttributes.RequestMaxTokens))
            span.MaxTokens = ParseAnyValueAsLong();
        else
            _reader.Skip();
    }

    /// <summary>
    ///     Parses legacy agents.* attributes and normalizes them to gen_ai.agent.* / gen_ai.tool.*.
    /// </summary>
    private void ParseLegacyAgentsAttributeValue(ParsedSpan span, ReadOnlySpan<byte> keySpan)
    {
        var key = Encoding.UTF8.GetString(keySpan);

        // Normalize legacy agents.* to gen_ai.* (see SchemaNormalizer.DeprecatedMappings)
        var normalizedKey = SchemaNormalizer.Normalize(key);

        var value = ParseAnyValueAsObject();
        span.Attributes ??= [];
        span.Attributes.Add(new KeyValuePair<string, object?>(normalizedKey, value));
    }

    private ReadOnlySpan<byte> ParseAnyValueSpan()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return default;

        ReadOnlySpan<byte> result = default;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            if (_reader.ValueTextEquals("stringValue"u8))
            {
                _reader.Read();
                result = _reader.HasValueSequence
                    ? _reader.ValueSequence.ToArray()
                    : _reader.ValueSpan;
            }
            else
                _reader.Skip();
        }

        return result;
    }

    private string? ParseAnyValue()
    {
        var span = ParseAnyValueSpan();
        return span.IsEmpty ? null : Encoding.UTF8.GetString(span);
    }

    private long ParseAnyValueAsLong()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return 0;

        long result = 0;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            if (_reader.ValueTextEquals("intValue"u8))
            {
                _reader.Read();
                result = _reader.TokenType switch
                {
                    JsonTokenType.Number => _reader.GetInt64(),
                    JsonTokenType.String when Utf8Parser.TryParse(_reader.ValueSpan, out long v, out _) => v,
                    _ => result
                };
            }
            else if (_reader.ValueTextEquals("stringValue"u8))
            {
                _reader.Read();
                if (Utf8Parser.TryParse(_reader.ValueSpan, out long v, out _)) result = v;
            }
            else
                _reader.Skip();
        }

        return result;
    }

    private double? ParseAnyValueAsDouble()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return null;

        double? result = null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            if (_reader.ValueTextEquals("doubleValue"u8))
            {
                _reader.Read();
                if (_reader.TokenType == JsonTokenType.Number) result = _reader.GetDouble();
            }
            else if (_reader.ValueTextEquals("stringValue"u8))
            {
                _reader.Read();
                if (Utf8Parser.TryParse(_reader.ValueSpan, out double v, out _)) result = v;
            }
            else
                _reader.Skip();
        }

        return result;
    }

    private object? ParseAnyValueAsObject()
    {
        if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartObject) return null;

        object? result = null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType != JsonTokenType.PropertyName) continue;

            var propName = _reader.ValueSpan;
            _reader.Read();

            if (propName.SequenceEqual("stringValue"u8))
                result = _reader.GetString();
            else if (propName.SequenceEqual("intValue"u8))
            {
                result = _reader.TokenType == JsonTokenType.Number
                    ? _reader.GetInt64()
                    : long.Parse(_reader.GetString()!);
            }
            else if (propName.SequenceEqual("doubleValue"u8))
                result = _reader.GetDouble();
            else if (propName.SequenceEqual("boolValue"u8))
                result = _reader.GetBoolean();
            else if (propName.SequenceEqual("bytesValue"u8))
                result = _reader.GetBytesFromBase64();
            else if (propName.SequenceEqual("arrayValue"u8))
                result = ParseArrayValue();
            else if (propName.SequenceEqual("kvlistValue"u8)) result = ParseKvListValue();
        }

        return result;
    }

    private List<object?>? ParseArrayValue()
    {
        if (_reader.TokenType != JsonTokenType.StartObject) return null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName &&
                _reader.ValueTextEquals("values"u8))
            {
                if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return null;

                List<object?> list = [];
                while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
                {
                    if (_reader.TokenType == JsonTokenType.StartObject)
                        list.Add(ParseAnyValueInner());
                }

                return list;
            }

            if (_reader.TokenType == JsonTokenType.PropertyName) _reader.Skip();
        }

        return null;
    }

    private object? ParseAnyValueInner()
    {
        object? result = null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = _reader.ValueSpan;
                _reader.Read();

                if (propName.SequenceEqual("stringValue"u8))
                    result = _reader.GetString();
                else if (propName.SequenceEqual("intValue"u8))
                {
                    result = _reader.TokenType == JsonTokenType.Number
                        ? _reader.GetInt64()
                        : long.Parse(_reader.GetString()!);
                }
                else if (propName.SequenceEqual("doubleValue"u8))
                    result = _reader.GetDouble();
                else if (propName.SequenceEqual("boolValue"u8))
                    result = _reader.GetBoolean();
                else
                    _reader.Skip();
            }
        }

        return result;
    }

    private Dictionary<string, object?>? ParseKvListValue()
    {
        if (_reader.TokenType != JsonTokenType.StartObject) return null;

        while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
        {
            if (_reader.TokenType == JsonTokenType.PropertyName &&
                _reader.ValueTextEquals("values"u8))
            {
                if (!_reader.Read() || _reader.TokenType != JsonTokenType.StartArray) return null;

                Dictionary<string, object?> dict = [];
                while (_reader.Read() && _reader.TokenType != JsonTokenType.EndArray)
                {
                    if (_reader.TokenType != JsonTokenType.StartObject) continue;

                    string? key = null;
                    object? value = null;

                    while (_reader.Read() && _reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (_reader.TokenType != JsonTokenType.PropertyName) continue;

                        if (_reader.ValueTextEquals("key"u8))
                        {
                            _reader.Read();
                            key = _reader.GetString();
                        }
                        else if (_reader.ValueTextEquals("value"u8))
                            value = ParseAnyValueAsObject();
                        else
                            _reader.Skip();
                    }

                    if (key is not null) dict[key] = value;
                }

                return dict;
            }

            if (_reader.TokenType == JsonTokenType.PropertyName) _reader.Skip();
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetInternedString(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return string.Empty;

        var hash = ComputeHash(utf8);
        return InternedStrings.TryGetValue(hash, out var interned) ? interned : Encoding.UTF8.GetString(utf8);
    }
}
