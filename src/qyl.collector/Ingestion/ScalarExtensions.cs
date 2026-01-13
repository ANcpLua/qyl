// =============================================================================
// qyl.collector - Scalar Type Extensions
// Extension methods for generated scalar types
// =============================================================================

namespace qyl.collector.Ingestion;

/// <summary>
///     Extension methods for TraceId, SpanId, and other generated scalar types.
/// </summary>
public static class ScalarExtensions
{
    // ═══════════════════════════════════════════════════════════════════════
    // TraceId Extensions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Checks if the TraceId is empty (all zeros or null).</summary>
    public static bool IsEmpty(this TraceId traceId) =>
        string.IsNullOrEmpty(traceId.Value) || traceId.Value == "00000000000000000000000000000000";

    /// <summary>Tries to parse a hex string as a TraceId.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Bytes, out TraceId result)
    {
        if (utf8Bytes.Length != 32)
        {
            result = default;
            return false;
        }

        Span<char> chars = stackalloc char[32];
        for (var i = 0; i < 32; i++)
        {
            var b = utf8Bytes[i];
            if (!IsHexChar(b))
            {
                result = default;
                return false;
            }

            chars[i] = (char)b;
        }

        result = new TraceId(new string(chars));
        return true;
    }

    /// <summary>Tries to format the TraceId to a span of bytes.</summary>
    public static bool TryFormat(this TraceId traceId, Span<byte> destination, out int bytesWritten)
    {
        if (string.IsNullOrEmpty(traceId.Value) || traceId.Value.Length != 32 || destination.Length < 32)
        {
            bytesWritten = 0;
            return false;
        }

        for (var i = 0; i < 32; i++)
        {
            destination[i] = (byte)traceId.Value[i];
        }

        bytesWritten = 32;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SpanId Extensions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Checks if the SpanId is empty (all zeros or null).</summary>
    public static bool IsEmpty(this SpanId spanId) =>
        string.IsNullOrEmpty(spanId.Value) || spanId.Value == "0000000000000000";

    /// <summary>Tries to parse a hex string as a SpanId.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Bytes, out SpanId result)
    {
        if (utf8Bytes.Length != 16)
        {
            result = default;
            return false;
        }

        Span<char> chars = stackalloc char[16];
        for (var i = 0; i < 16; i++)
        {
            var b = utf8Bytes[i];
            if (!IsHexChar(b))
            {
                result = default;
                return false;
            }

            chars[i] = (char)b;
        }

        result = new SpanId(new string(chars));
        return true;
    }

    /// <summary>Tries to format the SpanId to a span of bytes.</summary>
    public static bool TryFormat(this SpanId spanId, Span<byte> destination, out int bytesWritten)
    {
        if (string.IsNullOrEmpty(spanId.Value) || spanId.Value.Length != 16 || destination.Length < 16)
        {
            bytesWritten = 0;
            return false;
        }

        for (var i = 0; i < 16; i++)
        {
            destination[i] = (byte)spanId.Value[i];
        }

        bytesWritten = 16;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SessionId Extensions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Checks if the SessionId is empty.</summary>
    public static bool IsEmpty(this SessionId sessionId) =>
        string.IsNullOrEmpty(sessionId.Value);

    /// <summary>Tries to create a SessionId from a Guid.</summary>
    public static SessionId FromGuid(Guid id) =>
        new(id.ToString("N"));

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static bool IsHexChar(byte b) =>
        (b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F');
}
