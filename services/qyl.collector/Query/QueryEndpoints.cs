namespace Qyl.Collector.Query;

internal static class QueryEndpoints
{
    private const int DefaultLimit = 1000;
    private const int MaxLimit = 10_000;

    private static readonly FrozenSet<string> s_bannedTokens = new[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "ATTACH", "DETACH", "COPY"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    [QylMapEndpoints]
    public static WebApplication MapQueryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/query", ExecuteQueryAsync);
        return app;
    }

    private static async Task<IResult> ExecuteQueryAsync(
        QueryRequest request,
        DuckDbStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Sql))
            return TypedResults.BadRequest(new { error = "SQL query is required." });

        var trimmed = request.Sql.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (!upper.StartsWithOrdinal("SELECT") &&
            !upper.StartsWithOrdinal("WITH"))
        {
            return TypedResults.BadRequest(new { error = "Only SELECT and WITH (CTE) queries are allowed." });
        }

        var tokens = upper.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (s_bannedTokens.Contains(token))
            {
                return TypedResults.BadRequest(new { error = $"Forbidden keyword detected: {token}" });
            }
        }

        var sql = ApplySafetyLimit(trimmed, upper, request.Limit);

        try
        {
            return await store.ExecuteReadAsync(con =>
            {
                using var cmd = con.CreateCommand();
                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();

                List<string> columns = new(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                List<Dictionary<string, object?>> rows = [];
                while (reader.Read())
                {
                    Dictionary<string, object?> row = new(reader.FieldCount, StringComparer.Ordinal);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.IsDBNull(i)
                            ? null
                            : NormalizeValue(reader.GetValue(i));
                        row[columns[i]] = value;
                    }

                    rows.Add(row);
                }

                return TypedResults.Ok(new { columns, rows, rowCount = rows.Count });
            }, ct);
        }
        catch (DuckDBException)
        {
            return TypedResults.BadRequest(new { error = "Request failed" });
        }
    }

    private static string ApplySafetyLimit(string sql, string upper, int? requestedLimit)
    {
        if (!upper.ContainsOrdinal("LIMIT"))
        {
            var limit = Math.Clamp(requestedLimit ?? DefaultLimit, 1, MaxLimit);
            return $"{sql} LIMIT {limit}";
        }

        var limitIndex = upper.LastIndexOf("LIMIT", StringComparison.Ordinal);
        var afterLimit = sql[(limitIndex + 5)..].Trim();

        var endIndex = 0;
        while (endIndex < afterLimit.Length && char.IsDigit(afterLimit[endIndex]))
            endIndex++;

        if (endIndex > 0 && int.TryParse(afterLimit[..endIndex], CultureInfo.InvariantCulture, out var existingLimit))
        {
            if (existingLimit > MaxLimit)
            {
                var capped = sql[..(limitIndex + 5)] + " " + MaxLimit + afterLimit[endIndex..];
                return capped;
            }
        }

        return sql;
    }

    private static object? NormalizeValue(object value) => value switch
    {
        string s => s,
        int i => i,
        long l => l,
        double d => d,
        float f => (double)f,
        bool b => b,
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        decimal dec => (double)dec,
        _ => value.ToString()
    };
}

internal sealed record QueryRequest(string Sql, int? Limit = null);
