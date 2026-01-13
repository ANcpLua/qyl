using System.Text;
using qyl.collector.Query;
using qyl.collector.Storage;

namespace qyl.collector.tests.Diagnostics;

public class DiagnosticTest : IAsyncLifetime
{
    private DuckDbStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
    }

    [Fact]
    public async Task Debug_TestErrorSummaryQuery()
    {
        // Insert test data - non-GenAI span with OK status
        var span = SpanBuilder.Create("trace-1", "span-1")
            .WithSessionId("no-errors")
            .WithStatusCode(1) // OK
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Check raw data
        await using var checkCmd = _store.Connection.CreateCommand();
        checkCmd.CommandText = "SELECT trace_id, session_id, status_code, start_time FROM spans";
        await using var checkReader = await checkCmd.ExecuteReaderAsync();

        Console.WriteLine("Raw data in spans table:");
        while (await checkReader.ReadAsync())
        {
            var traceId = checkReader.GetString(0);
            var sessionId = checkReader.IsDBNull(1) ? "NULL" : checkReader.GetString(1);
            var statusCode = checkReader.IsDBNull(2)
                ? "NULL"
                : checkReader.GetInt32(2).ToString(CultureInfo.InvariantCulture);
            var startTime = checkReader.IsDBNull(3) ? "NULL" : checkReader.GetDateTime(3).ToString("o");
            Console.WriteLine(
                $"  trace={traceId}, session={sessionId}, status_code={statusCode}, start_time={startTime}");

            // Debug: show bytes
            if (!checkReader.IsDBNull(1))
            {
                var bytes = Encoding.UTF8.GetBytes(sessionId);
                Console.WriteLine($"  session_id bytes: {BitConverter.ToString(bytes)}");
                Console.WriteLine($"  session_id length: {sessionId.Length}");
            }
        }

        // Simple count with parameter
        await using var countCmd = _store.Connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM spans WHERE session_id = $1";
        countCmd.Parameters.Add(new DuckDBParameter { Value = "no-errors" });
        var count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"\nSimple COUNT(*) WHERE session_id = $1 (param): {count}");

        // Simple count with literal
        await using var countCmd2 = _store.Connection.CreateCommand();
        countCmd2.CommandText = "SELECT COUNT(*) FROM spans WHERE session_id = 'no-errors'";
        var count2 = (long)(await countCmd2.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"Simple COUNT(*) WHERE session_id = 'no-errors' (literal): {count2}");

        // LIKE workaround
        await using var countCmd3 = _store.Connection.CreateCommand();
        countCmd3.CommandText = "SELECT COUNT(*) FROM spans WHERE session_id LIKE $1";
        countCmd3.Parameters.Add(new DuckDBParameter { Value = "%no-errors%" });
        var count3 = (long)(await countCmd3.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"Simple COUNT(*) WHERE session_id LIKE '%no-errors%' (param): {count3}");

        // Test with lower()
        await using var countCmd4 = _store.Connection.CreateCommand();
        countCmd4.CommandText = "SELECT COUNT(*) FROM spans WHERE lower(session_id) = 'no-errors'";
        var count4 = (long)(await countCmd4.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"COUNT(*) WHERE lower(session_id) = 'no-errors': {count4}");

        // Test with raw SQL comparison
        await using var countCmd5 = _store.Connection.CreateCommand();
        countCmd5.CommandText =
            "SELECT COUNT(*), session_id, session_id = 'no-errors' as eq_test FROM spans GROUP BY session_id";
        await using var reader5 = await countCmd5.ExecuteReaderAsync();
        while (await reader5.ReadAsync())
        {
            var cnt = reader5.GetInt64(0);
            var sid = reader5.GetString(1);
            var eqResult = reader5.GetBoolean(2);
            Console.WriteLine($"Raw comparison: count={cnt}, session_id='{sid}', equals='no-errors' is {eqResult}");
        }

        // Test with subquery
        await using var countCmd6 = _store.Connection.CreateCommand();
        countCmd6.CommandText = "SELECT COUNT(*) FROM (SELECT * FROM spans) sub WHERE sub.session_id = 'no-errors'";
        var count6 = (long)(await countCmd6.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"Subquery WHERE session_id = 'no-errors': {count6}");

        // Test with IS NOT DISTINCT FROM
        await using var countCmd7 = _store.Connection.CreateCommand();
        countCmd7.CommandText = "SELECT COUNT(*) FROM spans WHERE session_id IS NOT DISTINCT FROM 'no-errors'";
        var count7 = (long)(await countCmd7.ExecuteScalarAsync() ?? 0L);
        Console.WriteLine($"WHERE session_id IS NOT DISTINCT FROM 'no-errors': {count7}");

        // === HEX DEBUG - what does DuckDB actually see? ===
        Console.WriteLine("\n=== DuckDB HEX DEBUG ===");
        await using var hexCmd = _store.Connection.CreateCommand();
        hexCmd.CommandText =
            "SELECT length(session_id) as len, hex(session_id) as hex_val, typeof(session_id) as dtype FROM spans";
        await using var hexReader = await hexCmd.ExecuteReaderAsync();
        while (await hexReader.ReadAsync())
        {
            var len = hexReader.GetInt64(0);
            var hexVal = hexReader.GetString(1);
            var dtype = hexReader.GetString(2);
            Console.WriteLine($"DuckDB sees: length={len}, hex={hexVal}, type={dtype}");
            Console.WriteLine("Expected hex: 6E6F2D6572726F7273 (no-errors)");
        }

        // Test what DuckDB thinks about equality
        await using var eqCmd = _store.Connection.CreateCommand();
        eqCmd.CommandText = """
                            SELECT
                                session_id,
                                session_id = 'no-errors' as direct_eq,
                                CAST(session_id AS VARCHAR) = 'no-errors' as cast_eq,
                                trim(session_id) = 'no-errors' as trim_eq,
                                hex('no-errors') as literal_hex
                            FROM spans
                            """;
        await using var eqReader = await eqCmd.ExecuteReaderAsync();
        while (await eqReader.ReadAsync())
        {
            Console.WriteLine($"session_id='{eqReader.GetString(0)}'");
            Console.WriteLine($"  direct_eq={eqReader.GetBoolean(1)}");
            Console.WriteLine($"  cast_eq={eqReader.GetBoolean(2)}");
            Console.WriteLine($"  trim_eq={eqReader.GetBoolean(3)}");
            Console.WriteLine($"  literal 'no-errors' hex={eqReader.GetString(4)}");
        }

        // Check schema
        await using var schemaCmd = _store.Connection.CreateCommand();
        schemaCmd.CommandText =
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'spans' AND column_name = 'session_id'";
        await using var schemaReader = await schemaCmd.ExecuteReaderAsync();
        while (await schemaReader.ReadAsync())
            Console.WriteLine($"\nSchema: column={schemaReader.GetString(0)}, type={schemaReader.GetString(1)}");
        Console.WriteLine("=== END HEX DEBUG ===\n");

        // Generate and run the ErrorSummary query
        var sql = SpanQueryBuilder.Create()
            .SelectCount("total_spans")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count")
            .Select("SUM(CASE WHEN status_code = 2 THEN 1.0 ELSE 0.0 END) / COUNT(*) * 100 AS error_rate")
            .WhereOptional(SpanColumn.SessionId, 1)
            .WhereRaw("($2::TIMESTAMP IS NULL OR start_time >= $2)")
            .Build();

        Console.WriteLine("\nGenerated SQL:");
        Console.WriteLine(sql);

        await using var cmd = _store.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = "no-errors" });
        cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });

        Console.WriteLine("\nParameters: $1='no-errors', $2=NULL");

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var totalSpans = reader.IsDBNull(0) ? -1 : reader.GetInt64(0);
            var errorCount = reader.IsDBNull(1) ? -1 : reader.GetInt64(1);
            var errorRate = reader.IsDBNull(2) ? -1.0 : reader.GetDouble(2);
            Console.WriteLine($"\nResult: total_spans={totalSpans}, error_count={errorCount}, error_rate={errorRate}");
            Console.WriteLine("(Note: -1 means NULL was returned)");
            Assert.Equal(1, totalSpans);
        }
        else
        {
            Console.WriteLine("\nNo rows returned!");
            Assert.Fail("Expected 1 row");
        }
    }
}