namespace qyl.collector.Query;

/// <summary>
///     REST endpoint for executing read-only SQL queries against the DuckDB store.
///     Used by MCP agents and the dashboard for ad-hoc data exploration.
/// </summary>
internal static class QueryEndpoints
{
    private static readonly FrozenSet<string> BannedTokens = new[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "ATTACH", "DETACH", "COPY"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private const int DefaultLimit = 1000;
    private const int MaxLimit = 10_000;

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
        // Validate SQL is provided
        if (string.IsNullOrWhiteSpace(request.Sql))
            return Results.BadRequest(new { error = "SQL query is required." });

        string trimmed = request.Sql.Trim();
        string upper = trimmed.ToUpperInvariant();

        // Must start with SELECT or WITH (CTEs)
        if (!upper.StartsWithOrdinal("SELECT") &&
            !upper.StartsWithOrdinal("WITH"))
        {
            return Results.BadRequest(new { error = "Only SELECT and WITH (CTE) queries are allowed." });
        }

        // Scan for banned keywords
        string[] tokens = upper.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
        {
            if (BannedTokens.Contains(token))
            {
                return Results.BadRequest(new { error = $"Forbidden keyword detected: {token}" });
            }
        }

        // Apply safety LIMIT
        string sql = ApplySafetyLimit(trimmed, upper, request.Limit);

        try
        {
            await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
            await using DuckDBCommand cmd = lease.Connection.CreateCommand();
            cmd.CommandText = sql;

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            // Extract column names
            List<string> columns = new(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            // Read rows into dictionaries
            List<Dictionary<string, object?>> rows = [];
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                Dictionary<string, object?> row = new(reader.FieldCount, StringComparer.Ordinal);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object? value = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
                        ? null
                        : NormalizeValue(reader.GetValue(i));
                    row[columns[i]] = value;
                }
                rows.Add(row);
            }

            return Results.Ok(new { columns, rows, rowCount = rows.Count });
        }
        catch (DuckDBException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static string ApplySafetyLimit(string sql, string upper, int? requestedLimit)
    {
        if (!upper.ContainsOrdinal("LIMIT"))
        {
            int limit = Math.Clamp(requestedLimit ?? DefaultLimit, 1, MaxLimit);
            return $"{sql} LIMIT {limit}";
        }

        // SQL already contains LIMIT — cap it to MaxLimit
        // Find the last LIMIT token and its numeric argument
        int limitIndex = upper.LastIndexOf("LIMIT", StringComparison.Ordinal);
        string afterLimit = sql[(limitIndex + 5)..].Trim();

        // Extract the numeric value after LIMIT
        int endIndex = 0;
        while (endIndex < afterLimit.Length && char.IsDigit(afterLimit[endIndex]))
            endIndex++;

        if (endIndex > 0 && int.TryParse(afterLimit[..endIndex], CultureInfo.InvariantCulture, out int existingLimit))
        {
            if (existingLimit > MaxLimit)
            {
                string capped = sql[..(limitIndex + 5)] + " " + MaxLimit + afterLimit[endIndex..];
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
