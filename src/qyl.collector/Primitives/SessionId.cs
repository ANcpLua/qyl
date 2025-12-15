// =============================================================================
// qyl Telemetry Primitives - SessionId
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

namespace qyl.collector.Primitives;

/// <summary>
///     Strongly-typed session identifier for aggregation.
/// </summary>
public readonly record struct SessionId(string Value) :
    IUtf8SpanParsable<SessionId>,
    ISpanParsable<SessionId>
{
    /// <summary>Empty session ID.</summary>
    public static readonly SessionId Empty = new(string.Empty);

    /// <summary>Returns true if this session ID is empty or null.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    // =========================================================================
    // ISpanParsable<SessionId> - Parse from char span
    // =========================================================================

    /// <inheritdoc />
    public static SessionId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(s.ToString());

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(s.ToString());
        return true;
    }

    // =========================================================================
    // IParsable<SessionId> - String overloads
    // =========================================================================

    /// <inheritdoc />
    public static SessionId Parse(string s, IFormatProvider? provider) => new(s);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SessionId result)
    {
        result = s is not null ? new SessionId(s) : default;
        return s is not null;
    }

    // =========================================================================
    // IUtf8SpanParsable<SessionId> - Parse from UTF-8 bytes
    // =========================================================================

    /// <inheritdoc />
    public static SessionId Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        new(Encoding.UTF8.GetString(utf8Text));

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(Encoding.UTF8.GetString(utf8Text));
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
