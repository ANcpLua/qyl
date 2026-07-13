using System.Text;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static class TracePageCursorCodec
{
    private const int TraceIdLength = 32;
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);

    internal static string Encode(TracePageCursor cursor)
    {
        var payload = string.Concat(
            cursor.ActivityUnixNano.ToString("x16", CultureInfo.InvariantCulture),
            ".",
            cursor.TraceId);
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static bool TryDecode(string? encoded, out TracePageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 128) return false;

        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding is 2) base64 += "==";
        else if (padding is 3) base64 += "=";
        else if (padding is not 0) return false;

        string payload;
        try
        {
            payload = s_strictUtf8.GetString(Convert.FromBase64String(base64));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return false;
        }

        if (payload.Length != 16 + 1 + TraceIdLength || payload[16] != '.') return false;
        if (!ulong.TryParse(
                payload.AsSpan(0, 16),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var activity))
        {
            return false;
        }

        var traceId = payload[17..];
        if (!traceId.All(Uri.IsHexDigit)) return false;

        cursor = new TracePageCursor(activity, traceId.ToLowerInvariant());
        return true;
    }
}
