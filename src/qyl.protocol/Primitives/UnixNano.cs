// =============================================================================
// qyl.protocol - UnixNano Primitive
// Unix timestamp in nanoseconds with zero-allocation parsing
// =============================================================================

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Qyl.Protocol.Primitives;

/// <summary>
///     Unix timestamp in nanoseconds. Zero-allocation parsing from string/UTF-8.
/// </summary>
public readonly record struct UnixNano :
    IUtf8SpanParsable<UnixNano>,
    ISpanParsable<UnixNano>,
    IComparable<UnixNano>
{
    private const long _nanosPerMillisecond = 1_000_000;
    private const long _ticksPerNano = 100; // 1 tick = 100 nanoseconds

    /// <summary>Zero timestamp.</summary>
    public static readonly UnixNano Zero;

    /// <summary>Creates a UnixNano from a nanosecond value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnixNano(long value)
    {
        Value = value;
    }

    /// <summary>Gets the raw nanosecond value.</summary>
    public long Value { get; }

    // =========================================================================
    // IComparable<UnixNano> and operators
    // =========================================================================

    /// <inheritdoc />
    public int CompareTo(UnixNano other)
    {
        return Value.CompareTo(other.Value);
    }

    // =========================================================================
    // ISpanParsable<UnixNano> - Zero allocation from char span
    // =========================================================================

    /// <inheritdoc />
    public static UnixNano Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return new UnixNano(long.Parse(s, provider));
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnixNano result)
    {
        if (long.TryParse(s, provider, out var value))
        {
            result = new UnixNano(value);
            return true;
        }

        result = default;
        return false;
    }

    // =========================================================================
    // IParsable<UnixNano> - String overload delegates to span
    // =========================================================================

    /// <inheritdoc />
    public static UnixNano Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UnixNano result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), provider, out result);
    }


    // =========================================================================
    // IUtf8SpanParsable<UnixNano> - Zero allocation from UTF-8 bytes
    // =========================================================================

    /// <inheritdoc />
    public static UnixNano Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (Utf8Parser.TryParse(utf8Text, out long value, out _)) return new UnixNano(value);

        ThrowFormatException(utf8Text.Length);
        return default;
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out UnixNano result)
    {
        if (Utf8Parser.TryParse(utf8Text, out long value, out _))
        {
            result = new UnixNano(value);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Converts to DateTimeOffset.</summary>
    public DateTimeOffset ToDateTimeOffset()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Value / _nanosPerMillisecond);
    }

    /// <summary>Converts to TimeSpan.</summary>
    public TimeSpan ToTimeSpan()
    {
        return TimeSpan.FromTicks(Value / _ticksPerNano);
    }

    /// <summary>Creates UnixNano from DateTimeOffset.</summary>
    public static UnixNano FromDateTimeOffset(DateTimeOffset dto)
    {
        return new UnixNano(dto.ToUnixTimeMilliseconds() * _nanosPerMillisecond);
    }

    /// <summary>Gets current time as UnixNano with sub-millisecond precision.</summary>
    public static UnixNano Now()
    {
        return new UnixNano(TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * _nanosPerMillisecond +
                            Stopwatch.GetTimestamp() % _nanosPerMillisecond);
    }

    public static bool operator <(UnixNano left, UnixNano right)
    {
        return left.Value < right.Value;
    }

    public static bool operator >(UnixNano left, UnixNano right)
    {
        return left.Value > right.Value;
    }

    public static bool operator <=(UnixNano left, UnixNano right)
    {
        return left.Value <= right.Value;
    }

    public static bool operator >=(UnixNano left, UnixNano right)
    {
        return left.Value >= right.Value;
    }

    public static UnixNano operator -(UnixNano left, UnixNano right)
    {
        return new UnixNano(left.Value - right.Value);
    }

    public static UnixNano operator +(UnixNano left, UnixNano right)
    {
        return new UnixNano(left.Value + right.Value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    // =========================================================================
    // Exception helpers (cold path)
    // =========================================================================

    [DoesNotReturn]
    private static void ThrowFormatException(int actualLength)
    {
        throw new FormatException($"Invalid UnixNano format (length: {actualLength})");
    }
}
