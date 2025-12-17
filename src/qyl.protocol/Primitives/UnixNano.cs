// =============================================================================
// qyl.protocol - UnixNano Primitive
// Unix timestamp in nanoseconds with zero-allocation parsing
// =============================================================================

using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace qyl.protocol.Primitives;

/// <summary>
///     Unix timestamp in nanoseconds. Zero-allocation parsing from string/UTF-8.
/// </summary>
public readonly record struct UnixNano :
    IUtf8SpanParsable<UnixNano>,
    ISpanParsable<UnixNano>,
    IComparable<UnixNano>
{
    private const long NanosPerMillisecond = 1_000_000;
    private const long TicksPerNano = 100; // 1 tick = 100 nanoseconds

    /// <summary>Zero timestamp.</summary>
    public static readonly UnixNano Zero;

    /// <summary>Creates a UnixNano from a nanosecond value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnixNano(long value) => Value = value;

    /// <summary>Gets the raw nanosecond value.</summary>
    public long Value { get; }

    // =========================================================================
    // IComparable<UnixNano> and operators
    // =========================================================================

    /// <inheritdoc />
    public int CompareTo(UnixNano other) => Value.CompareTo(other.Value);

    // =========================================================================
    // ISpanParsable<UnixNano> - Zero allocation from char span
    // =========================================================================

    /// <inheritdoc />
    public static UnixNano Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(long.Parse(s, provider));

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
    public static UnixNano Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

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
    public DateTimeOffset ToDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(Value / NanosPerMillisecond);

    /// <summary>Converts to TimeSpan.</summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Value / TicksPerNano);

    /// <summary>Creates UnixNano from DateTimeOffset.</summary>
    public static UnixNano FromDateTimeOffset(DateTimeOffset dto) =>
        new(dto.ToUnixTimeMilliseconds() * NanosPerMillisecond);

    /// <summary>Gets current time as UnixNano with sub-millisecond precision.</summary>
    public static UnixNano Now() =>
        new((TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * NanosPerMillisecond) +
            (Stopwatch.GetTimestamp() % NanosPerMillisecond));

    public static bool operator <(UnixNano left, UnixNano right) => left.Value < right.Value;

    public static bool operator >(UnixNano left, UnixNano right) => left.Value > right.Value;

    public static bool operator <=(UnixNano left, UnixNano right) => left.Value <= right.Value;

    public static bool operator >=(UnixNano left, UnixNano right) => left.Value >= right.Value;

    public static UnixNano operator -(UnixNano left, UnixNano right) => new(left.Value - right.Value);

    public static UnixNano operator +(UnixNano left, UnixNano right) => new(left.Value + right.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    // =========================================================================
    // Exception helpers (cold path)
    // =========================================================================

    [DoesNotReturn]
    private static void ThrowFormatException(int actualLength) =>
        throw new FormatException($"Invalid UnixNano format (length: {actualLength})");
}
