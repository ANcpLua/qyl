using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using DuckDB.NET.Data;

namespace Qyl.DuckDb.Aot;

/// <summary>
/// Native-AOT bridge for <c>DuckDB.NET.Data</c> (verified against 1.5.3), pending upstream
/// annotations (https://github.com/Giorgi/DuckDB.NET/issues/339).
///
/// DuckDB.NET's read path resolves <c>Nullable&lt;U&gt;</c> columns through
/// <c>MethodInfo.MakeGenericMethod</c> over an internal generic virtual method
/// (<c>VectorDataReaderBase.GetValidValue&lt;U&gt;</c>), and materializes LIST/MAP columns
/// through <c>Type.MakeGenericType</c> + <c>Activator.CreateInstance</c>. Under ILC those
/// lookups only succeed when the exact value-type instantiations already exist in the native
/// image; <c>[DynamicDependency]</c> roots metadata but does NOT create value-type generic
/// instantiations, so the only reliable mechanism is static reachability.
///
/// <see cref="Warmup"/> therefore executes one real query against an in-memory DuckDB
/// database and reads every DuckDB-supported scalar type through both the non-nullable and
/// nullable typed accessors, plus LIST columns for the common element types. The static
/// <c>GetFieldValue&lt;U&gt;</c> call sites make ILC expand the generic-virtual-method slot
/// across every vector reader (whole-program GVM analysis), which is exactly the set
/// <c>MakeGenericMethod</c> needs at runtime — and because the warmup runs the real read
/// path, a missing instantiation fails loudly at startup instead of corrupting a query later.
/// </summary>
public static class DuckDbAot
{
    private static readonly Lock Gate = new();
    private static bool _completed;

    /// <summary>
    /// Roots and verifies DuckDB.NET's generic read paths. Call once at startup, before the
    /// first query. Idempotent and thread-safe; subsequent calls are no-ops. On a
    /// CoreCLR/JIT runtime this is a cheap self-test; on Native AOT it is load-bearing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A supported type failed to round-trip — on Native AOT this means a generic
    /// instantiation is missing from the image and DuckDB reads WOULD fail later.
    /// </exception>
    public static void Warmup()
    {
        if (_completed)
            return;

        lock (Gate)
        {
            if (_completed)
                return;

            RootCompositeTypes();

            using var connection = new DuckDBConnection("DataSource=:memory:");
            connection.Open();

            VerifyScalars(connection);
            VerifyNullableScalars(connection);
            VerifyLists(connection);
            VerifyMapAndStruct(connection);

            _completed = true;
        }
    }

    /// <summary>Test hook: forces the next <see cref="Warmup"/> to run the full verification.</summary>
    internal static void ResetForVerification()
    {
        lock (Gate)
        {
            _completed = false;
        }
    }

    /// <summary>
    /// Preserves the shape of a POCO that is materialized from a DuckDB STRUCT column.
    /// DuckDB.NET populates STRUCT results via <c>Activator.CreateInstance</c> +
    /// <c>Type.GetProperties()</c>; trimming strips unreferenced constructors and setters
    /// silently (properties simply stay default). Call this once per POCO type read from a
    /// STRUCT column; the annotation makes the trimmer keep what the provider needs.
    /// Deliberately inert: it never executes the POCO's constructor — the
    /// <c>[DynamicallyAccessedMembers]</c> dataflow annotation alone is what the trimmer
    /// and ILC act on.
    /// </summary>
    public static void RootStruct<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
                                    DynamicallyAccessedMemberTypes.PublicProperties)] TPoco>()
    {
        KeepAlive(typeof(TPoco));
    }

    /// <summary>
    /// Roots the typed-accessor generic instantiation for <typeparamref name="T"/> without
    /// executing anything: constructing a delegate to <c>GetFieldValue&lt;T&gt;</c> makes the
    /// instantiation statically reachable, so ILC compiles the generic-virtual-method chain
    /// (<c>GetValue&lt;T&gt;</c> → <c>GetValidValue&lt;T&gt;</c>) across all vector readers.
    /// Needed for value types the warmup's closed set cannot know: consumer-defined enums
    /// read as <c>TEnum?</c> (call <c>RootFieldType&lt;TEnum&gt;()</c> — the NON-nullable type —
    /// because that is the instantiation <c>NullableHandler&lt;TEnum?&gt;</c> probes for), and
    /// DuckDB provider-specific structs (<c>DuckDBHugeInt</c>, …). Rooting is proven by the
    /// mechanism, not runtime-verified — pair it with a query-shaped test in your own suite.
    /// </summary>
    public static void RootFieldType<T>()
    {
        KeepAlive((Func<DbDataReader, int, T>)(static (reader, ordinal) => reader.GetFieldValue<T>(ordinal)));
    }

    /// <summary>
    /// Statically constructs the closed generic types DuckDB.NET creates reflectively:
    /// <c>List&lt;T&gt;</c> for LIST columns, <c>Dictionary&lt;string, TValue&gt;</c> for MAP columns
    /// with VARCHAR keys (the provider builds <c>Dictionary&lt;K,V&gt;</c> from the CONCRETE
    /// key/value CLR types via MakeGenericType — the key/value space is combinatorial, so this
    /// roots the string-keyed family and consumers with other key types call
    /// <see cref="RootMap{TKey, TValue}"/>), and <c>Dictionary&lt;string, object&gt;</c>, which is
    /// the ClrType of STRUCT columns read through the boxed path. A constructed
    /// <c>new List&lt;T&gt;()</c>/<c>new Dictionary&lt;K,V&gt;()</c> call site satisfies both the
    /// MakeGenericType lookup and the Activator parameterless-ctor requirement.
    /// </summary>
    private static void RootCompositeTypes()
    {
        RootMap<string, bool>();
        RootMap<string, sbyte>();
        RootMap<string, byte>();
        RootMap<string, short>();
        RootMap<string, ushort>();
        RootMap<string, int>();
        RootMap<string, uint>();
        RootMap<string, long>();
        RootMap<string, ulong>();
        RootMap<string, float>();
        RootMap<string, double>();
        RootMap<string, decimal>();
        RootMap<string, DateTime>();
        RootMap<string, DateOnly>();
        RootMap<string, TimeOnly>();
        RootMap<string, DateTimeOffset>();
        RootMap<string, TimeSpan>();
        RootMap<string, Guid>();
        RootMap<string, BigInteger>();
        RootMap<string, string>();
        KeepAlive(new List<bool>());
        KeepAlive(new List<sbyte>());
        KeepAlive(new List<byte>());
        KeepAlive(new List<short>());
        KeepAlive(new List<ushort>());
        KeepAlive(new List<int>());
        KeepAlive(new List<uint>());
        KeepAlive(new List<long>());
        KeepAlive(new List<ulong>());
        KeepAlive(new List<float>());
        KeepAlive(new List<double>());
        KeepAlive(new List<decimal>());
        KeepAlive(new List<DateTime>());
        KeepAlive(new List<DateOnly>());
        KeepAlive(new List<TimeOnly>());
        KeepAlive(new List<DateTimeOffset>());
        KeepAlive(new List<TimeSpan>());
        KeepAlive(new List<Guid>());
        KeepAlive(new List<BigInteger>());
        KeepAlive(new List<string>());
        KeepAlive(new List<bool?>());
        KeepAlive(new List<sbyte?>());
        KeepAlive(new List<byte?>());
        KeepAlive(new List<short?>());
        KeepAlive(new List<ushort?>());
        KeepAlive(new List<int?>());
        KeepAlive(new List<uint?>());
        KeepAlive(new List<long?>());
        KeepAlive(new List<ulong?>());
        KeepAlive(new List<float?>());
        KeepAlive(new List<double?>());
        KeepAlive(new List<decimal?>());
        KeepAlive(new List<DateTime?>());
        KeepAlive(new List<DateOnly?>());
        KeepAlive(new List<TimeOnly?>());
        KeepAlive(new List<DateTimeOffset?>());
        KeepAlive(new List<TimeSpan?>());
        KeepAlive(new List<Guid?>());
        KeepAlive(new List<BigInteger?>());
        // STRUCT columns read through the boxed path materialize Dictionary<string, object>.
        KeepAlive(new Dictionary<string, object>());

        // Provider-specific structs (DuckDB.NET.Native) readable through typed accessors;
        // rooted so their Nullable<T> reads resolve, mechanism-proven but not query-verified.
        RootFieldType<DuckDB.NET.Native.DuckDBHugeInt>();
        RootFieldType<DuckDB.NET.Native.DuckDBUHugeInt>();
        RootFieldType<DuckDB.NET.Native.DuckDBInterval>();
        RootFieldType<DuckDB.NET.Native.DuckDBDateOnly>();
        RootFieldType<DuckDB.NET.Native.DuckDBTimeOnly>();
        RootFieldType<DuckDB.NET.Native.DuckDBTimestamp>();
    }

    /// <summary>
    /// Statically constructs <c>Dictionary&lt;TKey, TValue&gt;</c> so a MAP(K, V) column whose
    /// concrete CLR pair this matches can be materialized under Native AOT. VARCHAR-keyed
    /// maps over the primitive value set are pre-rooted; call this for any other combination
    /// your queries read (e.g. <c>RootMap&lt;int, string&gt;()</c> for MAP(INTEGER, VARCHAR)).
    /// </summary>
    public static void RootMap<TKey, TValue>() where TKey : notnull
    {
        KeepAlive(new Dictionary<TKey, TValue>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KeepAlive(object instance) => GC.KeepAlive(instance);

    /// <summary>
    /// One row, every DuckDB scalar type, read through the typed generic accessor
    /// <c>GetFieldValue&lt;U&gt;</c>. Each call site is a static generic instantiation that
    /// ILC expands across all vector readers — the prerequisite for the nullable path below.
    /// </summary>
    private static void VerifyScalars(DuckDBConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                true                                          AS c_bool,
                CAST(-1   AS TINYINT)                         AS c_sbyte,
                CAST(1    AS UTINYINT)                        AS c_byte,
                CAST(-2   AS SMALLINT)                        AS c_short,
                CAST(2    AS USMALLINT)                       AS c_ushort,
                CAST(-3   AS INTEGER)                         AS c_int,
                CAST(3    AS UINTEGER)                        AS c_uint,
                CAST(-4   AS BIGINT)                          AS c_long,
                CAST(4    AS UBIGINT)                         AS c_ulong,
                CAST(1.5  AS FLOAT)                           AS c_float,
                CAST(2.5  AS DOUBLE)                          AS c_double,
                CAST(3.50 AS DECIMAL(18,2))                   AS c_decimal,
                CAST(5    AS HUGEINT)                         AS c_hugeint,
                'qyl'                                         AS c_string,
                CAST('00000000-0000-0000-0000-000000000001' AS UUID)  AS c_guid,
                TIMESTAMP '2026-01-02 03:04:05'               AS c_datetime,
                DATE '2026-01-02'                             AS c_date,
                TIME '03:04:05'                               AS c_time,
                TIMESTAMPTZ '2026-01-02 03:04:05+00'          AS c_dto,
                INTERVAL 90 SECOND                            AS c_interval,
                CAST('ab' AS BLOB)                            AS c_blob
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("DuckDB AOT warmup query returned no row.");

        Expect(reader.GetFieldValue<bool>(0), true, "bool");
        Expect(reader.GetFieldValue<sbyte>(1), (sbyte)-1, "sbyte");
        Expect(reader.GetFieldValue<byte>(2), (byte)1, "byte");
        Expect(reader.GetFieldValue<short>(3), (short)-2, "short");
        Expect(reader.GetFieldValue<ushort>(4), (ushort)2, "ushort");
        Expect(reader.GetFieldValue<int>(5), -3, "int");
        Expect(reader.GetFieldValue<uint>(6), 3u, "uint");
        Expect(reader.GetFieldValue<long>(7), -4L, "long");
        Expect(reader.GetFieldValue<ulong>(8), 4ul, "ulong");
        Expect(reader.GetFieldValue<float>(9), 1.5f, "float");
        Expect(reader.GetFieldValue<double>(10), 2.5d, "double");
        Expect(reader.GetFieldValue<decimal>(11), 3.50m, "decimal");
        Expect(reader.GetFieldValue<BigInteger>(12), new BigInteger(5), "BigInteger");
        Expect(reader.GetFieldValue<string>(13), "qyl", "string");
        Expect(reader.GetFieldValue<Guid>(14), new Guid("00000000-0000-0000-0000-000000000001"), "Guid");
        Expect(reader.GetFieldValue<DateTime>(15), new DateTime(2026, 1, 2, 3, 4, 5), "DateTime");
        Expect(reader.GetFieldValue<DateOnly>(16), new DateOnly(2026, 1, 2), "DateOnly");
        Expect(reader.GetFieldValue<TimeOnly>(17), new TimeOnly(3, 4, 5), "TimeOnly");
        Expect(reader.GetFieldValue<DateTimeOffset>(18), new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), "DateTimeOffset");
        Expect(reader.GetFieldValue<TimeSpan>(19), TimeSpan.FromSeconds(90), "TimeSpan");
        // GetValue (the boxed, non-generic path the ADO.NET surface uses) for BLOB —
        // DuckDB.NET surfaces BLOB as a Stream over the native vector memory.
        if (reader.GetValue(20) is not Stream { Length: 2 })
            throw Mismatch("BLOB", "2-byte Stream", reader.GetValue(20));
    }

    /// <summary>
    /// The same scalar set as <c>Nullable&lt;U&gt;</c> reads (one NULL row, one value row).
    /// This is the path that runs <c>NullableHandler&lt;U?&gt;.Compile()</c> —
    /// <c>MakeGenericMethod(GetValidValue&lt;U&gt;)</c> — at runtime; it succeeds only because
    /// <see cref="VerifyScalars"/> made every <c>GetValidValue&lt;U&gt;</c> statically reachable.
    /// </summary>
    private static void VerifyNullableScalars(DuckDBConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM (VALUES
                (CAST(NULL AS BOOLEAN), CAST(NULL AS TINYINT), CAST(NULL AS UTINYINT),
                 CAST(NULL AS SMALLINT), CAST(NULL AS USMALLINT), CAST(NULL AS INTEGER),
                 CAST(NULL AS UINTEGER), CAST(NULL AS BIGINT), CAST(NULL AS UBIGINT),
                 CAST(NULL AS FLOAT), CAST(NULL AS DOUBLE), CAST(NULL AS DECIMAL(18,2)),
                 CAST(NULL AS HUGEINT), CAST(NULL AS UUID), CAST(NULL AS TIMESTAMP),
                 CAST(NULL AS DATE), CAST(NULL AS TIME), CAST(NULL AS TIMESTAMPTZ),
                 CAST(NULL AS INTERVAL)),
                (true, CAST(-1 AS TINYINT), CAST(1 AS UTINYINT),
                 CAST(-2 AS SMALLINT), CAST(2 AS USMALLINT), CAST(-3 AS INTEGER),
                 CAST(3 AS UINTEGER), CAST(-4 AS BIGINT), CAST(4 AS UBIGINT),
                 CAST(1.5 AS FLOAT), CAST(2.5 AS DOUBLE), CAST(3.50 AS DECIMAL(18,2)),
                 CAST(5 AS HUGEINT), CAST('00000000-0000-0000-0000-000000000001' AS UUID),
                 TIMESTAMP '2026-01-02 03:04:05', DATE '2026-01-02', TIME '03:04:05',
                 TIMESTAMPTZ '2026-01-02 03:04:05+00', INTERVAL 90 SECOND)
            ) t(c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12, c13, c14, c15, c16, c17, c18)
            ORDER BY c0 NULLS FIRST
            """;
        using var reader = command.ExecuteReader();

        if (!reader.Read())
            throw new InvalidOperationException("DuckDB AOT warmup nullable query returned no NULL row.");
        ReadNullableRow(reader, expectNull: true);

        if (!reader.Read())
            throw new InvalidOperationException("DuckDB AOT warmup nullable query returned no value row.");
        ReadNullableRow(reader, expectNull: false);
    }

    private static void ReadNullableRow(DbDataReader reader, bool expectNull)
    {
        ExpectNullable(reader.GetFieldValue<bool?>(0), expectNull, true, "bool?");
        ExpectNullable(reader.GetFieldValue<sbyte?>(1), expectNull, (sbyte)-1, "sbyte?");
        ExpectNullable(reader.GetFieldValue<byte?>(2), expectNull, (byte)1, "byte?");
        ExpectNullable(reader.GetFieldValue<short?>(3), expectNull, (short)-2, "short?");
        ExpectNullable(reader.GetFieldValue<ushort?>(4), expectNull, (ushort)2, "ushort?");
        ExpectNullable(reader.GetFieldValue<int?>(5), expectNull, -3, "int?");
        ExpectNullable(reader.GetFieldValue<uint?>(6), expectNull, 3u, "uint?");
        ExpectNullable(reader.GetFieldValue<long?>(7), expectNull, -4L, "long?");
        ExpectNullable(reader.GetFieldValue<ulong?>(8), expectNull, 4ul, "ulong?");
        ExpectNullable(reader.GetFieldValue<float?>(9), expectNull, 1.5f, "float?");
        ExpectNullable(reader.GetFieldValue<double?>(10), expectNull, 2.5d, "double?");
        ExpectNullable(reader.GetFieldValue<decimal?>(11), expectNull, 3.50m, "decimal?");
        ExpectNullable(reader.GetFieldValue<BigInteger?>(12), expectNull, new BigInteger(5), "BigInteger?");
        ExpectNullable(reader.GetFieldValue<Guid?>(13), expectNull, new Guid("00000000-0000-0000-0000-000000000001"), "Guid?");
        ExpectNullable(reader.GetFieldValue<DateTime?>(14), expectNull, new DateTime(2026, 1, 2, 3, 4, 5), "DateTime?");
        ExpectNullable(reader.GetFieldValue<DateOnly?>(15), expectNull, new DateOnly(2026, 1, 2), "DateOnly?");
        ExpectNullable(reader.GetFieldValue<TimeOnly?>(16), expectNull, new TimeOnly(3, 4, 5), "TimeOnly?");
        ExpectNullable(reader.GetFieldValue<DateTimeOffset?>(17), expectNull, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), "DateTimeOffset?");
        ExpectNullable(reader.GetFieldValue<TimeSpan?>(18), expectNull, TimeSpan.FromSeconds(90), "TimeSpan?");
    }

    /// <summary>
    /// LIST columns through the non-generic <c>GetValue</c> path (MakeGenericType +
    /// Activator.CreateInstance inside the provider) for the element types
    /// <see cref="RootCompositeTypes"/> constructed statically.
    /// </summary>
    private static void VerifyLists(DuckDBConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ['a', 'b']                    AS l_string,
                [1, 2]                        AS l_int,
                [CAST(1 AS BIGINT), 2]        AS l_long,
                [CAST(1.5 AS DOUBLE), 2.5]    AS l_double,
                [CAST(1.5 AS DECIMAL(18,2)), 2.50] AS l_decimal,
                [CAST(1 AS INTEGER), NULL]    AS l_int_null
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("DuckDB AOT warmup list query returned no row.");

        ExpectList<string>(reader.GetValue(0), 2, "LIST(VARCHAR)");
        ExpectList<int>(reader.GetValue(1), 2, "LIST(INTEGER)");
        ExpectList<long>(reader.GetValue(2), 2, "LIST(BIGINT)");
        ExpectList<double>(reader.GetValue(3), 2, "LIST(DOUBLE)");
        ExpectList<decimal>(reader.GetValue(4), 2, "LIST(DECIMAL)");
        // NULL-containing lists require the nullable-element generic read — this drives the
        // provider's BuildList<int?> and, inside it, NullableHandler<int?> under real data.
        ExpectList<int?>(reader.GetFieldValue<List<int?>>(5), 2, "LIST(INTEGER) with NULL");
    }

    /// <summary>
    /// MAP columns materialize <c>Dictionary&lt;K,V&gt;</c> from the CONCRETE key/value CLR types
    /// (MakeGenericType + Activator inside MapVectorDataReader) — NOT Dictionary&lt;string,object&gt;,
    /// which is the STRUCT ClrType. Verify one value-typed and one string-valued MAP plus a
    /// STRUCT literal so both reflective constructions run for real.
    /// </summary>
    private static void VerifyMapAndStruct(DuckDBConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                MAP {'a': 1, 'b': 2}          AS m_int,
                MAP {'a': 'x'}                AS m_string,
                {'n': 1, 's': 'x'}            AS struct_col
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("DuckDB AOT warmup map/struct query returned no row.");

        if (reader.GetValue(0) is not Dictionary<string, int> { Count: 2 })
            throw Mismatch("MAP(VARCHAR,INTEGER)", "Dictionary<string,int> of 2", reader.GetValue(0));
        if (reader.GetValue(1) is not Dictionary<string, string> { Count: 1 })
            throw Mismatch("MAP(VARCHAR,VARCHAR)", "Dictionary<string,string> of 1", reader.GetValue(1));
        if (reader.GetValue(2) is not Dictionary<string, object> { Count: 2 })
            throw Mismatch("STRUCT", "Dictionary<string,object> of 2", reader.GetValue(2));
    }

    private static void Expect<T>(T actual, T expected, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw Mismatch(label, expected, actual);
    }

    private static void ExpectNullable<T>(T? actual, bool expectNull, T expected, string label) where T : struct
    {
        if (expectNull)
        {
            if (actual is not null)
                throw Mismatch(label, "NULL", actual);
        }
        else
        {
            if (actual is null || !EqualityComparer<T>.Default.Equals(actual.Value, expected))
                throw Mismatch(label, expected, actual);
        }
    }

    private static void ExpectList<T>(object value, int count, string label)
    {
        if (value is not IReadOnlyList<T> list || list.Count != count)
            throw Mismatch(label, $"IReadOnlyList<{typeof(T).Name}> of {count}", value);
    }

    private static InvalidOperationException Mismatch(string label, object? expected, object? actual) =>
        new($"DuckDB AOT warmup failed for {label}: expected {expected}, got {actual ?? "<null>"} " +
            $"({actual?.GetType().FullName ?? "null"}). On Native AOT this means a generic " +
            "instantiation is missing from the image; on JIT it indicates a DuckDB.NET behavior change. " +
            "See internal/qyl.duckdb.aot/README.md and https://github.com/Giorgi/DuckDB.NET/issues/339.");
}
