using System.Buffers.Binary;

namespace Qyl.Collector.Query;

internal static class LogCursor
{
    private const string Prefix = "c_";

    public static string Encode(ulong unixNano)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unixNano);
        return Prefix + Base64Url.Encode(bytes);
    }

    public static bool TryDecode(string? cursor, out ulong unixNano)
    {
        unixNano = 0;
        if (string.IsNullOrWhiteSpace(cursor) || !cursor.StartsWithOrdinal(Prefix))
            return false;
        if (!Base64Url.TryDecode(cursor[Prefix.Length..], out var bytes) || bytes.Length != sizeof(ulong))
            return false;
        unixNano = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return true;
    }
}
