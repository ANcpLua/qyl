// =============================================================================
// qyl.protocol - SessionId Primitive
// Strongly-typed session identifier with zero-allocation parsing
// =============================================================================

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Qyl.Protocol.Primitives;

/// <summary>
///     Strongly-typed session identifier. Wraps a string with validation.
/// </summary>
public readonly record struct SessionId :
    ISpanParsable<SessionId>,
    IComparable<SessionId>
{
    /// <summary>Empty session ID.</summary>
    public static readonly SessionId Empty = new(string.Empty);

    private readonly string _value;

    /// <summary>Creates a SessionId from a string value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SessionId(string value)
    {
        _value = value ?? string.Empty;
    }

    /// <summary>Gets the raw string value.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Returns true if the session ID is empty or null.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    // =========================================================================
    // IComparable<SessionId>
    // =========================================================================

    /// <inheritdoc />
    public int CompareTo(SessionId other)
    {
        return string.Compare(Value, other.Value, StringComparison.Ordinal);
    }

    // =========================================================================
    // ISpanParsable<SessionId>
    // =========================================================================

    /// <inheritdoc />
    public static SessionId Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return new SessionId(s.ToString());
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(s.ToString());
        return true;
    }

    /// <inheritdoc />
    public static SessionId Parse(string s, IFormatProvider? provider)
    {
        return new SessionId(s);
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SessionId result)
    {
        result = new SessionId(s ?? string.Empty);
        return s is not null;
    }

    /// <summary>Creates a new random session ID using a GUID.</summary>
    public static SessionId NewId()
    {
        return new SessionId(Guid.NewGuid().ToString("N"));
    }

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

    public static implicit operator string(SessionId id)
    {
        return id.Value;
    }

    public static explicit operator SessionId(string value)
    {
        return new SessionId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
