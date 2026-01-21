// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    /Users/ancplua/qyl/core/openapi/openapi.yaml
//     Generated: 2026-01-16T09:00:34.9249700+00:00
//     Strongly-typed scalar primitives
// =============================================================================

#nullable enable

namespace Qyl;

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

/// <summary>Duration in milliseconds</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(DurationMsJsonConverter))]
public readonly partial record struct DurationMs(double Value)
{
    public static implicit operator double(DurationMs v) => v.Value;
    public static implicit operator DurationMs(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class DurationMsJsonConverter : System.Text.Json.Serialization.JsonConverter<DurationMs>
{
    public override DurationMs Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, DurationMs value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
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

/// <summary>Duration in seconds</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(DurationSJsonConverter))]
public readonly partial record struct DurationS(double Value)
{
    public static implicit operator double(DurationS v) => v.Value;
    public static implicit operator DurationS(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class DurationSJsonConverter : System.Text.Json.Serialization.JsonConverter<DurationS>
{
    public override DurationS Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, DurationS value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>IP address (IPv4 or IPv6)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(IpAddressJsonConverter))]
public readonly partial record struct IpAddress(string Value)
{
    public static implicit operator string(IpAddress v) => v.Value;
    public static implicit operator IpAddress(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
}

file sealed class IpAddressJsonConverter : System.Text.Json.Serialization.JsonConverter<IpAddress>
{
    public override IpAddress Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, IpAddress value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Percentage value (0.0 to 100.0)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(PercentageJsonConverter))]
public readonly partial record struct Percentage(double Value)
{
    public static implicit operator double(Percentage v) => v.Value;
    public static implicit operator Percentage(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class PercentageJsonConverter : System.Text.Json.Serialization.JsonConverter<Percentage>
{
    public override Percentage Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, Percentage value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Ratio value (0.0 to 1.0)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(RatioJsonConverter))]
public readonly partial record struct Ratio(double Value)
{
    public static implicit operator double(Ratio v) => v.Value;
    public static implicit operator Ratio(double v) => new(v);
    public override string ToString() => Value.ToString();
    public bool IsValid => true;
}

file sealed class RatioJsonConverter : System.Text.Json.Serialization.JsonConverter<Ratio>
{
    public override Ratio Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetDouble());
    public override void Write(System.Text.Json.Utf8JsonWriter writer, Ratio value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

/// <summary>Semantic version string (e.g., 1.2.3)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SemVerJsonConverter))]
public readonly partial record struct SemVer(string Value)
{
    public static implicit operator string(SemVer v) => v.Value;
    public static implicit operator SemVer(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$", System.Text.RegularExpressions.RegexOptions.Compiled);
    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);
}

file sealed class SemVerJsonConverter : System.Text.Json.Serialization.JsonConverter<SemVer>
{
    public override SemVer Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SemVer value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Unique session identifier</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SessionIdJsonConverter))]
public readonly partial record struct SessionId(string Value)
{
    public static implicit operator string(SessionId v) => v.Value;
    public static implicit operator SessionId(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
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
    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (dest.Length < 16 || string.IsNullOrEmpty(Value)) { written = 0; return false; }
        Value.AsSpan().CopyTo(dest); written = 16; return true;
    }
    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;
    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
}

file sealed class SpanIdJsonConverter : System.Text.Json.Serialization.JsonConverter<SpanId>
{
    public override SpanId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SpanId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Token count (for LLM operations)</summary>
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
    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (dest.Length < 32 || string.IsNullOrEmpty(Value)) { written = 0; return false; }
        Value.AsSpan().CopyTo(dest); written = 32; return true;
    }
    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;
    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
}

file sealed class TraceIdJsonConverter : System.Text.Json.Serialization.JsonConverter<TraceId>
{
    public override TraceId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, TraceId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>W3C Trace Context tracestate header (vendor-specific key-value pairs)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TraceStateJsonConverter))]
public readonly partial record struct TraceState(string Value)
{
    public static implicit operator string(TraceState v) => v.Value;
    public static implicit operator TraceState(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
}

file sealed class TraceStateJsonConverter : System.Text.Json.Serialization.JsonConverter<TraceState>
{
    public override TraceState Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, TraceState value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>URL string (absolute)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(UrlStringJsonConverter))]
public readonly partial record struct UrlString(string Value)
{
    public static implicit operator string(UrlString v) => v.Value;
    public static implicit operator UrlString(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
}

file sealed class UrlStringJsonConverter : System.Text.Json.Serialization.JsonConverter<UrlString>
{
    public override UrlString Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, UrlString value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>User agent string</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(UserAgentJsonConverter))]
public readonly partial record struct UserAgent(string Value)
{
    public static implicit operator string(UserAgent v) => v.Value;
    public static implicit operator UserAgent(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
}

file sealed class UserAgentJsonConverter : System.Text.Json.Serialization.JsonConverter<UserAgent>
{
    public override UserAgent Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, UserAgent value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>User identifier (pseudonymized for privacy)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(UserIdJsonConverter))]
public readonly partial record struct UserId(string Value)
{
    public static implicit operator string(UserId v) => v.Value;
    public static implicit operator UserId(string v) => new(v);
    public override string ToString() => Value ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(Value);
}

file sealed class UserIdJsonConverter : System.Text.Json.Serialization.JsonConverter<UserId>
{
    public override UserId Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new(reader.GetString() ?? string.Empty);
    public override void Write(System.Text.Json.Utf8JsonWriter writer, UserId value, System.Text.Json.JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

