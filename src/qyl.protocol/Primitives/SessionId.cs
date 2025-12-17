// =============================================================================
// qyl.protocol - SessionId Primitive
// Strongly-typed session identifier with zero-allocation parsing
// Owner: qyl.protocol | Consumers: [collector, mcp]
// =============================================================================

using System.Runtime.CompilerServices;

namespace qyl.protocol.Primitives;

/// <summary>
///     Strongly-typed session identifier. Wraps a string with validation.
/// </summary>
public readonly record struct SessionId :
    IUtf8SpanParsable<SessionId>,
    ISpanParsable<SessionId>,
    IComparable<SessionId>
{
    /// <summary>Empty session ID.</summary>
    public static readonly SessionId Empty = new(string.Empty);

    private readonly string _value;

    /// <summary>Creates a SessionId from a string value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SessionId(string value) => _value = value ?? string.Empty;

    /// <summary>Gets the raw string value.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Returns true if the session ID is empty or null.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    // =========================================================================
    // IComparable<SessionId>
    // =========================================================================

    /// <inheritdoc />
    public int CompareTo(SessionId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    // =========================================================================
    // ISpanParsable<SessionId>
    // =========================================================================

    /// <inheritdoc />
    public static SessionId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(s.ToString());

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(s.ToString());
        return true;
    }

    /// <inheritdoc />
    public static SessionId Parse(string s, IFormatProvider? provider) => new(s);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(s ?? string.Empty);
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

    /// <summary>Creates a new random session ID using a GUID.</summary>
    public static SessionId NewId() => new(Guid.NewGuid().ToString("N"));

    /// <summary>Creates a SessionId from a trace ID (first 32 chars of hex).</summary>
    public static SessionId FromTraceId(string traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return Empty;

        // Use first 32 characters as session ID
        return new SessionId(traceId.Length >= 32 ? traceId[..32] : traceId);
    }

    // =========================================================================
    // Operators
    // =========================================================================

    public static implicit operator string(SessionId id) => id.Value;

    public static explicit operator SessionId(string value) => new(value);

    public static bool operator <(SessionId left, SessionId right) => left.CompareTo(right) < 0;

    public static bool operator >(SessionId left, SessionId right) => left.CompareTo(right) > 0;

    public static bool operator <=(SessionId left, SessionId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SessionId left, SessionId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;
}
