using System.Buffers.Binary;

namespace qyl.collector.Logs;

/// <summary>
///     Opaque cursor encoding for log timeline deltas.
///     Encodes a Unix nano timestamp into a compact URL-safe token.
/// </summary>
internal static class LogCursor
{
    private const string Prefix = "c_";

    public static string Encode(ulong unixNano)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unixNano);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return Prefix + token;
    }

    public static bool TryDecode(string? cursor, out ulong unixNano)
    {
        unixNano = 0;
        if (string.IsNullOrWhiteSpace(cursor) || !cursor.StartsWithOrdinal(Prefix))
            return false;

        try
        {
            var raw = cursor[Prefix.Length..]
                .Replace('-', '+')
                .Replace('_', '/');

            var padded = raw.PadRight(raw.Length + ((4 - raw.Length % 4) % 4), '=');
            var bytes = Convert.FromBase64String(padded);
            if (bytes.Length != sizeof(ulong))
                return false;

            unixNano = BinaryPrimitives.ReadUInt64BigEndian(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
