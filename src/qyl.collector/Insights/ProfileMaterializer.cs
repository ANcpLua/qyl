using System.Text;

namespace qyl.collector.Insights;

/// <summary>
///     Computes latency percentiles, token spend, WoW trends, and error rates
///     over a rolling 7-day window.
/// </summary>
internal static class ProfileMaterializer
{
    public static async Task<string> MaterializeAsync(
        DuckDbStore store, TimeProvider timeProvider, CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine("## Performance Profile (last 7d)");

        var now = timeProvider.GetUtcNow();
        var sevenDaysAgoNanos = (decimal)((ulong)(now.AddDays(-7).ToUnixTimeMilliseconds() * 1_000_000));
        var fourteenDaysAgoNanos = (decimal)((ulong)(now.AddDays(-14).ToUnixTimeMilliseconds() * 1_000_000));

        // Latency percentiles
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY duration_ns/1e6) as p50,
                       PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns/1e6) as p95,
                       PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns/1e6) as p99,
                       COUNT(*) as total
                FROM spans WHERE start_time_unix_nano >= $1
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sevenDaysAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var p50 = reader.Col(0).AsDouble;
                var p95 = reader.Col(1).AsDouble;
                var p99 = reader.Col(2).AsDouble;
                var total = reader.Col(3).GetInt64(0);

                if (total > 0 && p50.HasValue)
                    sb.AppendLine($"- Latency: P50={FormatMs(p50.Value)} | P95={FormatMs(p95 ?? 0)} | P99={FormatMs(p99 ?? 0)} ({total:N0} spans)");
                else
                    sb.AppendLine("- No span data in the last 7 days.");
            }
        }

        // Daily token cost
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(gen_ai_cost_usd),0)/7.0 as daily_cost,
                       COALESCE(SUM(gen_ai_input_tokens+gen_ai_output_tokens),0) as total_tokens
                FROM spans WHERE gen_ai_provider_name IS NOT NULL AND start_time_unix_nano >= $1
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sevenDaysAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var dailyCost = reader.Col(0).GetDouble(0);
                var totalTokens = reader.Col(1).GetInt64(0);

                if (totalTokens > 0)
                    sb.AppendLine($"- Token spend: ~${dailyCost:F2}/day ({totalTokens:N0} tokens total)");
            }
        }

        // WoW cost trend
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(CASE WHEN start_time_unix_nano >= $1 THEN gen_ai_cost_usd ELSE 0 END),0) as this_week,
                       COALESCE(SUM(CASE WHEN start_time_unix_nano < $1 THEN gen_ai_cost_usd ELSE 0 END),0) as last_week
                FROM spans WHERE gen_ai_provider_name IS NOT NULL AND start_time_unix_nano >= $2
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sevenDaysAgoNanos });
            cmd.Parameters.Add(new DuckDBParameter { Value = fourteenDaysAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var thisWeek = reader.Col(0).GetDouble(0);
                var lastWeek = reader.Col(1).GetDouble(0);

                if (lastWeek > 0.01)
                {
                    var changePercent = ((thisWeek - lastWeek) / lastWeek) * 100;
                    var direction = changePercent >= 0 ? "up" : "down";
                    sb.AppendLine($"- Cost trend: ${thisWeek:F2} this week vs ${lastWeek:F2} last week ({direction} {Math.Abs(changePercent):F0}% WoW)");
                }
            }
        }

        // Hottest operations
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name, COUNT(*) as cnt, ROUND(COUNT(*)*100.0/SUM(COUNT(*)) OVER(),1) as pct
                FROM spans WHERE start_time_unix_nano >= $1
                GROUP BY name ORDER BY cnt DESC LIMIT 5
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sevenDaysAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var first = true;
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (first)
                {
                    var name = reader.GetString(0);
                    var pct = reader.Col(2).GetDouble(0);
                    sb.AppendLine($"- Hottest operation: {name} ({pct:F1}% of traffic)");
                    first = false;
                }
            }
        }

        // Error rate
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(*) FILTER (WHERE status_code=2) as errors, COUNT(*) as total
                FROM spans WHERE start_time_unix_nano >= $1
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sevenDaysAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var errors = reader.Col(0).GetInt64(0);
                var total = reader.Col(1).GetInt64(0);

                if (total > 0)
                {
                    var rate = (double)errors / total * 100;
                    sb.AppendLine($"- Error rate: {rate:F1}% ({errors:N0}/{total:N0})");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMs(double ms) => ms switch
    {
        < 1 => $"{ms:F2}ms",
        < 1000 => $"{ms:F0}ms",
        _ => $"{ms / 1000:F1}s"
    };
}
