namespace Qyl.Collector.Storage;

internal static class DuckDbValueReader
{
    // DuckDB aggregates (SUM over BIGINT, COUNT) return HUGEINT, which the provider surfaces as
    // System.Numerics.BigInteger. BigInteger is not IConvertible, so Convert.ToXxx throws on it.
    // Normalize it to a value Convert understands before any numeric read.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object Normalize(object value) =>
        value is System.Numerics.BigInteger big ? (decimal)big : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(DbDataReader reader, int ordinal, string defaultValue) =>
        ReadString(reader, ordinal) ?? defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong? ReadUInt64(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToUInt64(Normalize(reader.GetValue(ordinal)), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64(DbDataReader reader, int ordinal, ulong defaultValue) =>
        ReadUInt64(reader, ordinal) ?? defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? ReadInt64(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt64(Normalize(reader.GetValue(ordinal)), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(DbDataReader reader, int ordinal, long defaultValue) =>
        ReadInt64(reader, ordinal) ?? defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int? ReadInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(Normalize(reader.GetValue(ordinal)), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(DbDataReader reader, int ordinal, int defaultValue) =>
        ReadInt32(reader, ordinal) ?? defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte? ReadByte(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToByte(Normalize(reader.GetValue(ordinal)), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(DbDataReader reader, int ordinal, byte defaultValue) =>
        ReadByte(reader, ordinal) ?? defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? ReadDouble(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDouble(Normalize(reader.GetValue(ordinal)), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDouble(DbDataReader reader, int ordinal, double defaultValue) =>
        ReadDouble(reader, ordinal) ?? defaultValue;

    public static DateTimeOffset? ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        return reader.GetValue(ordinal) switch
        {
            DateTimeOffset value => value,
            DateTime value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
            string value when DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) => parsed,
            _ => throw new InvalidCastException($"Cannot read column {ordinal} as DateTimeOffset.")
        };
    }
}
