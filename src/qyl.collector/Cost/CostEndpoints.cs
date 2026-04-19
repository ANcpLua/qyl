namespace Qyl.Collector.Cost;

internal static class CostEndpoints
{
    private static readonly FrozenSet<string> ValidGroupBy = FrozenSet.Create(
        StringComparer.Ordinal,
        ["session_id, service_name", "service_name", "gen_ai_provider_name, gen_ai_request_model"]);

    private static readonly FrozenSet<string> ValidTruncInterval = FrozenSet.Create(
        StringComparer.Ordinal,
        ["day", "hour"]);

    [QylMapEndpoints]
    public static WebApplication MapCostEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cost");

        group.MapGet("/by-session", static (DuckDbStore store, int? limit, int? hours, CancellationToken ct) =>
            AggregateAsync(store, "session_id, service_name", limit, hours, null, ct));

        group.MapGet("/by-service", static (DuckDbStore store, int? limit, int? hours, CancellationToken ct) =>
            AggregateAsync(store, "service_name", limit, hours, null, ct));

        group.MapGet("/by-model",
            static (DuckDbStore store, int? limit, int? hours, string? provider, CancellationToken ct) =>
                AggregateAsync(store, "gen_ai_provider_name, gen_ai_request_model", limit, hours, provider, ct));

        group.MapGet("/timeseries", GetCostTimeseriesAsync);
        group.MapGet("/budget", GetBudgetAsync);
        group.MapPost("/sync-pricing", static async (ModelPricingService svc, CancellationToken ct) =>
        {
            await svc.InitializeAsync(ct).ConfigureAwait(false);
            return TypedResults.Ok(new { status = "synced" });
        });
        group.MapPut("/pricing/{provider}/{model}", UpsertPricingAsync);

        return app;
    }

    private static async Task<IResult> AggregateAsync(
        DuckDbStore store, string groupBy, int? limit, int? hours,
        string? providerFilter, CancellationToken ct)
    {
        var safeGroupBy = SqlBuilder.Whitelisted(groupBy, ValidGroupBy);
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 1000);
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var providerClause = string.IsNullOrWhiteSpace(providerFilter)
            ? ""
            : " AND gen_ai_provider_name = $" + cmd.AddParam(providerFilter);

        cmd.CommandText = "SELECT " + safeGroupBy + ", COUNT(*) AS call_count,"
            + " COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,"
            + " COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,"
            + " COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost"
            + " FROM spans"
            + " WHERE gen_ai_request_model IS NOT NULL"
            + TimeFilter(hours) + providerClause
            + " GROUP BY " + safeGroupBy
            + " ORDER BY total_cost DESC LIMIT " + boundedLimit.ToString(CultureInfo.InvariantCulture);

        var items = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var groupCols = safeGroupBy.Split(',', StringSplitOptions.TrimEntries);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < groupCols.Length; i++)
                row[ToCamel(groupCols[i])] = reader.Col(i).AsString;

            var offset = groupCols.Length;
            row["callCount"] = reader.Col(offset).GetInt64(0);
            row["totalInputTokens"] = reader.Col(offset + 1).GetInt64(0);
            row["totalOutputTokens"] = reader.Col(offset + 2).GetInt64(0);
            row["totalCost"] = reader.Col(offset + 3).GetDouble(0);
            items.Add(row);
        }

        return TypedResults.Ok(new { items, total = items.Count });
    }

    private static async Task<IResult> GetCostTimeseriesAsync(
        DuckDbStore store, string? bucket, int? hours,
        string? service, string? model, CancellationToken ct)
    {
        var trunc = SqlBuilder.Whitelisted(bucket is "day" ? "day" : "hour", ValidTruncInterval);
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var extra = "";
        if (!string.IsNullOrWhiteSpace(service))
            extra += " AND service_name = $" + cmd.AddParam(service);
        if (!string.IsNullOrWhiteSpace(model))
            extra += " AND gen_ai_request_model = $" + cmd.AddParam(model);

        cmd.CommandText = "SELECT date_trunc('" + trunc + "', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket,"
            + " COUNT(*) AS call_count,"
            + " COALESCE(SUM(gen_ai_input_tokens), 0),"
            + " COALESCE(SUM(gen_ai_output_tokens), 0),"
            + " COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost"
            + " FROM spans"
            + " WHERE gen_ai_request_model IS NOT NULL"
            + TimeFilter(hours ?? 168) + extra
            + " GROUP BY bucket ORDER BY bucket ASC";

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new
            {
                bucket = reader.Col(0).AsDateTime,
                callCount = reader.Col(1).GetInt64(0),
                totalInputTokens = reader.Col(2).GetInt64(0),
                totalOutputTokens = reader.Col(3).GetInt64(0),
                totalCost = reader.Col(4).GetDouble(0)
            });
        }

        return TypedResults.Ok(new { items, total = items.Count, bucketSize = trunc });
    }

    private static async Task<IResult> GetBudgetAsync(
        DuckDbStore store, IConfiguration configuration, int? hours, CancellationToken ct)
    {
        var budgetUsd = double.TryParse(
            configuration["QYL_BUDGET_USD"], CultureInfo.InvariantCulture, out var b)
            ? b
            : (double?)null;

        var period = hours ?? 720;
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(gen_ai_cost_usd), 0), COUNT(*) FROM spans"
            + " WHERE gen_ai_request_model IS NOT NULL" + TimeFilter(period);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await reader.ReadAsync(ct).ConfigureAwait(false);
        var spent = reader.Col(0).GetDouble(0);
        var calls = reader.Col(1).GetInt64(0);

        return TypedResults.Ok(new
        {
            budgetUsd,
            totalSpent = spent,
            remaining = budgetUsd.HasValue ? budgetUsd.Value - spent : (double?)null,
            percentUsed = budgetUsd is > 0 ? spent / budgetUsd.Value * 100 : (double?)null,
            totalCalls = calls,
            periodHours = period
        });
    }

    private static async Task<IResult> UpsertPricingAsync(
        string provider, string model, PricingOverrideRequest request,
        DuckDbStore store, ModelPricingService pricingService, CancellationToken ct)
    {
        if (request.InputCost < 0 || request.OutputCost < 0)
            return TypedResults.BadRequest(new { error = "Costs must be non-negative." });

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, wct) =>
        {
            await using var expire = con.CreateCommand();
            expire.CommandText =
                "UPDATE model_pricing SET valid_to = $1 WHERE provider = $2 AND model = $3 AND valid_to IS NULL";
            expire.Parameters.Add(new DuckDBParameter { Value = now });
            expire.Parameters.Add(new DuckDBParameter { Value = provider });
            expire.Parameters.Add(new DuckDBParameter { Value = model });
            await expire.ExecuteNonQueryAsync(wct).ConfigureAwait(false);

            await using var insert = con.CreateCommand();
            insert.CommandText = """
                                 INSERT INTO model_pricing (provider, model, input_cost, output_cost,
                                     reasoning_cost, cache_read_cost, cache_write_cost, valid_from)
                                 VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                                 """;
            insert.Parameters.Add(new DuckDBParameter { Value = provider });
            insert.Parameters.Add(new DuckDBParameter { Value = model });
            insert.Parameters.Add(new DuckDBParameter { Value = request.InputCost });
            insert.Parameters.Add(new DuckDBParameter { Value = request.OutputCost });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)request.ReasoningCost ?? DBNull.Value });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)request.CacheReadCost ?? DBNull.Value });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)request.CacheWriteCost ?? DBNull.Value });
            insert.Parameters.Add(new DuckDBParameter { Value = now });
            await insert.ExecuteNonQueryAsync(wct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        await pricingService.RefreshCacheAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(new
        {
            provider,
            model,
            inputCost = request.InputCost,
            outputCost = request.OutputCost,
            validFrom = now
        });
    }

    private static string TimeFilter(int? hours) =>
        hours is > 0
            ? " AND start_time_unix_nano > (CAST(epoch_ns(now()) AS UBIGINT) - CAST("
              + hours.Value.ToString(CultureInfo.InvariantCulture)
              + " AS UBIGINT) * 3600000000000)"
            : "";

    private static string ToCamel(string snakeCol)
    {
        Span<char> buf = stackalloc char[snakeCol.Length];
        var j = 0;
        var upper = false;
        foreach (var c in snakeCol)
        {
            if (c is '_')
            {
                upper = true;
                continue;
            }

            buf[j++] = upper ? char.ToUpperInvariant(c) : c;
            upper = false;
        }

        return new string(buf[..j]);
    }
}

public sealed record PricingOverrideRequest(
    decimal InputCost,
    decimal OutputCost,
    decimal? ReasoningCost = null,
    decimal? CacheReadCost = null,
    decimal? CacheWriteCost = null);
