// =============================================================================
// qyl Telemetry Primitives - TraceId
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace qyl.collector.Primitives;

/// <summary>
///     128-bit W3C Trace ID. Stored as two ulongs for efficient comparison.
///     Wire format: 32 lowercase hex characters.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TraceId :
    IUtf8SpanParsable<TraceId>,
    ISpanParsable<TraceId>,
    IUtf8SpanFormattable,
    ISpanFormattable
{
    /// <summary>Empty trace ID (all zeros).</summary>
    public static readonly TraceId Empty;

    private const int HexLength = 32;
    private const int ByteLength = 16;

    /// <summary>Creates a TraceId from high and low 64-bit values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraceId(ulong high, ulong low)
    {
        High = high;
        Low = low;
    }

    /// <summary>Creates a TraceId from a 16-byte span (big-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraceId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength) ThrowByteLengthException(bytes.Length);

        High = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        Low = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
    }

    /// <summary>Returns true if this is the empty/zero trace ID.</summary>
    public bool IsEmpty => High is 0 && Low is 0;

    /// <summary>Gets the high 64 bits of the trace ID.</summary>
    public ulong High { get; }

    /// <summary>Gets the low 64 bits of the trace ID.</summary>
    public ulong Low { get; }

    // =========================================================================
    // IUtf8SpanParsable<TraceId> - Zero allocation from UTF-8 bytes
    // =========================================================================

    /// <inheritdoc />
    public static TraceId Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (!TryParse(utf8Text, provider, out var result)) ThrowFormatException(utf8Text.Length);

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out TraceId result)
    {
        result = default;
        if (utf8Text.Length != HexLength) return false;

        Span<byte> bytes = stackalloc byte[ByteLength];
        if (Convert.FromHexString(utf8Text, bytes, out _, out _) != OperationStatus.Done) return false;

        result = new TraceId(bytes);
        return true;
    }

    // =========================================================================
    // ISpanParsable<TraceId> - Zero allocation from char span
    // =========================================================================

    /// <inheritdoc />
    public static TraceId Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) ThrowFormatException(s.Length);

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TraceId result)
    {
        result = default;
        if (s.Length != HexLength) return false;

        Span<byte> bytes = stackalloc byte[ByteLength];
        if (Convert.FromHexString(s, bytes, out _, out _) != OperationStatus.Done) return false;

        result = new TraceId(bytes);
        return true;
    }

    // =========================================================================
    // IParsable<TraceId> - String overload delegates to span
    // =========================================================================

    /// <inheritdoc />
    public static TraceId Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TraceId result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), provider, out result);
    }

    // =========================================================================
    // IUtf8SpanFormattable - Zero allocation to UTF-8
    // =========================================================================

    /// <inheritdoc />
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        bytesWritten = 0;
        if (utf8Destination.Length < HexLength) return false;

        Span<byte> bytes = stackalloc byte[ByteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, High);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], Low);

        var hex = Convert.ToHexStringLower(bytes);
        bytesWritten = Encoding.UTF8.GetBytes(hex, utf8Destination);
        return true;
    }

    // =========================================================================
    // ISpanFormattable - Zero allocation to char span
    // =========================================================================

    /// <inheritdoc />
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        charsWritten = 0;
        if (destination.Length < HexLength) return false;

        Span<byte> bytes = stackalloc byte[ByteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, High);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], Low);

        var hex = Convert.ToHexStringLower(bytes);
        hex.CopyTo(destination);
        charsWritten = HexLength;
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, High);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], Low);
        return Convert.ToHexStringLower(bytes);
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    // =========================================================================
    // Exception helpers (cold path)
    // =========================================================================

    [DoesNotReturn]
    private static void ThrowFormatException(int actualLength) =>
        throw new FormatException($"Invalid TraceId format: expected {HexLength} hex characters, got {actualLength}");

    [DoesNotReturn]
    private static void ThrowByteLengthException(int actualLength) =>
        throw new ArgumentException($"Expected {ByteLength} bytes, got {actualLength}");
}
