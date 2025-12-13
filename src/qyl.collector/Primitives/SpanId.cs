// =============================================================================
// qyl Telemetry Primitives - SpanId
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace qyl.collector.Primitives;

/// <summary>
///     64-bit Span ID. Wire format: 16 lowercase hex characters.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct SpanId :
    IUtf8SpanParsable<SpanId>,
    ISpanParsable<SpanId>,
    IUtf8SpanFormattable,
    ISpanFormattable
{
    /// <summary>Empty span ID (zero).</summary>
    public static readonly SpanId Empty;

    private const int _hexLength = 16;
    private const int _byteLength = 8;

    /// <summary>Creates a SpanId from a 64-bit value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanId(ulong value)
    {
        Value = value;
    }

    /// <summary>Creates a SpanId from an 8-byte span (big-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != _byteLength) ThrowByteLengthException(bytes.Length);

        Value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    /// <summary>Returns true if this is the empty/zero span ID.</summary>
    public bool IsEmpty => Value == 0;

    /// <summary>Gets the underlying 64-bit value.</summary>
    public ulong Value { get; }

    // =========================================================================
    // IUtf8SpanParsable<SpanId> - Zero allocation from UTF-8 bytes
    // =========================================================================

    /// <inheritdoc />
    public static SpanId Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (!TryParse(utf8Text, provider, out var result)) ThrowFormatException(utf8Text.Length);

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SpanId result)
    {
        result = default;
        if (utf8Text.Length != _hexLength) return false;

        Span<byte> bytes = stackalloc byte[_byteLength];
        if (Convert.FromHexString(utf8Text, bytes, out _, out _) != OperationStatus.Done) return false;

        result = new SpanId(bytes);
        return true;
    }

    // =========================================================================
    // ISpanParsable<SpanId> - Zero allocation from char span
    // =========================================================================

    /// <inheritdoc />
    public static SpanId Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) ThrowFormatException(s.Length);

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SpanId result)
    {
        result = default;
        if (s.Length != _hexLength) return false;

        Span<byte> bytes = stackalloc byte[_byteLength];
        if (Convert.FromHexString(s, bytes, out _, out _) != OperationStatus.Done) return false;

        result = new SpanId(bytes);
        return true;
    }

    // =========================================================================
    // IParsable<SpanId> - String overload delegates to span
    // =========================================================================

    /// <inheritdoc />
    public static SpanId Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SpanId result)
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
        if (utf8Destination.Length < _hexLength) return false;

        Span<byte> bytes = stackalloc byte[_byteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);

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
        if (destination.Length < _hexLength) return false;

        Span<byte> bytes = stackalloc byte[_byteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);

        var hex = Convert.ToHexStringLower(bytes);
        hex.CopyTo(destination);
        charsWritten = _hexLength;
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[_byteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);
        return Convert.ToHexStringLower(bytes);
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

    // =========================================================================
    // Exception helpers (cold path)
    // =========================================================================

    [DoesNotReturn]
    private static void ThrowFormatException(int actualLength)
    {
        throw new FormatException($"Invalid SpanId format: expected {_hexLength} hex characters, got {actualLength}");
    }

    [DoesNotReturn]
    private static void ThrowByteLengthException(int actualLength)
    {
        throw new ArgumentException($"Expected {_byteLength} bytes, got {actualLength}");
    }
}