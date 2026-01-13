// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T06:08:38.9200860+00:00
//     Strongly-typed scalar primitives
// =============================================================================
// To modify: update TypeSpec in schema/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Common;

/// <summary>Cost in USD (floating point)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(CostUsdJsonConverter))]
public readonly partial record struct CostUsd(double Value)
{
    public static implicit operator double(CostUsd v) => v.Value;
    public static implicit operator CostUsd(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class CostUsdJsonConverter : System.Text.Json.Serialization.JsonConverter<CostUsd>
{
    public override CostUsd Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, CostUsd value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Generic non-negative counter</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(CountJsonConverter))]
public readonly partial record struct Count(long Value)
{
    public static implicit operator long(Count v) => v.Value;
    public static implicit operator Count(long v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class CountJsonConverter : System.Text.Json.Serialization.JsonConverter<Count>
{
    public override Count Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetInt64());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, Count value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Duration in nanoseconds</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(DurationNsJsonConverter))]
public readonly partial record struct DurationNs(long Value)
{
    public static implicit operator long(DurationNs v) => v.Value;
    public static implicit operator DurationNs(long v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class DurationNsJsonConverter : System.Text.Json.Serialization.JsonConverter<DurationNs>
{
    public override DurationNs Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetInt64());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, DurationNs value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Session identifier (GUID without hyphens)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SessionIdJsonConverter))]
public readonly partial record struct SessionId(string Value)
{
    public static implicit operator string(SessionId v) => v.Value;
    public static implicit operator SessionId(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@"^[a-f0-9]{32}$", System.Text.RegularExpressions.RegexOptions.Compiled);
    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);
}

file sealed class SessionIdJsonConverter : System.Text.Json.Serialization.JsonConverter<SessionId>
{
    public override SessionId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SessionId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Unique span identifier (16 lowercase hex characters)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SpanIdJsonConverter))]
public readonly partial record struct SpanId(string Value) : System.IParsable<SpanId>, System.ISpanFormattable
{
    public static implicit operator string(SpanId v) => v.Value;
    public static implicit operator SpanId(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@"^[a-f0-9]{16}$", System.Text.RegularExpressions.RegexOptions.Compiled);
    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);

    public static SpanId Empty => new("0000000000000000");
    public bool IsEmpty => string.IsNullOrEmpty(Value) || Value == "0000000000000000";

    public static SpanId Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var r) ? r : throw new FormatException($"Invalid SpanId: {s}");
    public static bool TryParse(string? s, IFormatProvider? provider, out SpanId result)
    {
        if (s is { Length: 16 } && IsValidHex(s.AsSpan())) { result = new(s); return true; }
        result = default; return false;
    }
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SpanId result)
    {
        if (s.Length == 16 && IsValidHex(s)) { result = new(new string(s)); return true; }
        result = default; return false;
    }
    public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out SpanId result)
    {
        if (utf8.Length == 16 && IsValidHexUtf8(utf8))
        {
            Span<char> chars = stackalloc char[16];
            for (var i = 0; i < 16; i++) chars[i] = (char)utf8[i];
            result = new(new string(chars)); return true;
        }
        result = default; return false;
    }
    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (dest.Length < 16 || string.IsNullOrEmpty(Value)) { written = 0; return false; }
        Value.AsSpan().CopyTo(dest); written = 16; return true;
    }
    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;
    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
    static bool IsValidHexUtf8(ReadOnlySpan<byte> s) { foreach (var b in s) if (!((b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F'))) return false; return true; }
}

file sealed class SpanIdJsonConverter : System.Text.Json.Serialization.JsonConverter<SpanId>
{
    public override SpanId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SpanId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Temperature setting for LLM requests (0.0-2.0)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TemperatureJsonConverter))]
public readonly partial record struct Temperature(double Value)
{
    public static implicit operator double(Temperature v) => v.Value;
    public static implicit operator Temperature(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class TemperatureJsonConverter : System.Text.Json.Serialization.JsonConverter<Temperature>
{
    public override Temperature Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, Temperature value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Token count for LLM operations</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TokenCountJsonConverter))]
public readonly partial record struct TokenCount(long Value)
{
    public static implicit operator long(TokenCount v) => v.Value;
    public static implicit operator TokenCount(long v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class TokenCountJsonConverter : System.Text.Json.Serialization.JsonConverter<TokenCount>
{
    public override TokenCount Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetInt64());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, TokenCount value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Unique trace identifier (32 lowercase hex characters)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TraceIdJsonConverter))]
public readonly partial record struct TraceId(string Value) : System.IParsable<TraceId>, System.ISpanFormattable
{
    public static implicit operator string(TraceId v) => v.Value;
    public static implicit operator TraceId(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@"^[a-f0-9]{32}$", System.Text.RegularExpressions.RegexOptions.Compiled);
    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);

    public static TraceId Empty => new("00000000000000000000000000000000");
    public bool IsEmpty => string.IsNullOrEmpty(Value) || Value == "00000000000000000000000000000000";

    public static TraceId Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var r) ? r : throw new FormatException($"Invalid TraceId: {s}");
    public static bool TryParse(string? s, IFormatProvider? provider, out TraceId result)
    {
        if (s is { Length: 32 } && IsValidHex(s.AsSpan())) { result = new(s); return true; }
        result = default; return false;
    }
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TraceId result)
    {
        if (s.Length == 32 && IsValidHex(s)) { result = new(new string(s)); return true; }
        result = default; return false;
    }
    public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out TraceId result)
    {
        if (utf8.Length == 32 && IsValidHexUtf8(utf8))
        {
            Span<char> chars = stackalloc char[32];
            for (var i = 0; i < 32; i++) chars[i] = (char)utf8[i];
            result = new(new string(chars)); return true;
        }
        result = default; return false;
    }
    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (dest.Length < 32 || string.IsNullOrEmpty(Value)) { written = 0; return false; }
        Value.AsSpan().CopyTo(dest); written = 32; return true;
    }
    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;
    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
    static bool IsValidHexUtf8(ReadOnlySpan<byte> s) { foreach (var b in s) if (!((b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F'))) return false; return true; }
}

file sealed class TraceIdJsonConverter : System.Text.Json.Serialization.JsonConverter<TraceId>
{
    public override TraceId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, TraceId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Unix timestamp in nanoseconds since epoch</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(UnixNanoJsonConverter))]
public readonly partial record struct UnixNano(long Value)
{
    public static implicit operator long(UnixNano v) => v.Value;
    public static implicit operator UnixNano(long v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class UnixNanoJsonConverter : System.Text.Json.Serialization.JsonConverter<UnixNano>
{
    public override UnixNano Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetInt64());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, UnixNano value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

