using System.Text;

namespace qyl.collector.Insights;

/// <summary>
///     Detects error spikes, cost drift, and slow operations in the last hour.
/// </summary>
internal static class AlertsMaterializer
{
    public static async Task<string> MaterializeAsync(
        DuckDbStore store, TimeProvider timeProvider, CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine("## Known Issues (auto-detected)");

        var now = timeProvider.GetUtcNow();
        var oneHourAgoNanos = (decimal)((ulong)(now.AddHours(-1).ToUnixTimeMilliseconds() * 1_000_000));
        var twoHoursAgoNanos = (decimal)((ulong)(now.AddHours(-2).ToUnixTimeMilliseconds() * 1_000_000));
        var twentyFourHoursAgoNanos = (decimal)((ulong)(now.AddHours(-24).ToUnixTimeMilliseconds() * 1_000_000));

        var hasAlerts = false;

        // Error spikes (this hour vs prev hour)
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT service_name,
                       SUM(CASE WHEN start_time_unix_nano >= $1 THEN 1 ELSE 0 END) as this_hr,
                       SUM(CASE WHEN start_time_unix_nano < $1 THEN 1 ELSE 0 END) as prev_hr
                FROM spans WHERE status_code = 2 AND start_time_unix_nano >= $2
                GROUP BY service_name HAVING this_hr > prev_hr * 2 AND this_hr > 5
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });
            cmd.Parameters.Add(new DuckDBParameter { Value = twoHoursAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                hasAlerts = true;
                var service = reader.Col(0).AsString ?? "unknown";
                var thisHr = reader.Col(1).GetInt64(0);
                var prevHr = reader.Col(2).GetInt64(0);
                var changePct = prevHr > 0 ? ((thisHr - prevHr) * 100 / prevHr) : 100;
                sb.AppendLine($"- [ERROR SPIKE] {service}: {thisHr} errors/hr (was {prevHr}/hr, +{changePct}%)");
            }
        }

        // Cost drift (this hour vs 24h avg)
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(CASE WHEN start_time_unix_nano >= $1 THEN gen_ai_cost_usd ELSE 0 END),0) as this_hr,
                       COALESCE(SUM(gen_ai_cost_usd),0)/24.0 as avg_hr
                FROM spans WHERE gen_ai_provider_name IS NOT NULL AND start_time_unix_nano >= $2
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });
            cmd.Parameters.Add(new DuckDBParameter { Value = twentyFourHoursAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var thisHr = reader.Col(0).GetDouble(0);
                var avgHr = reader.Col(1).GetDouble(0);

                if (avgHr > 0.01 && thisHr > avgHr * 1.5)
                {
                    hasAlerts = true;
                    var changePct = ((thisHr - avgHr) / avgHr) * 100;
                    sb.AppendLine($"- [COST] Token spend this hour: ${thisHr:F2} vs ${avgHr:F2}/hr avg (+{changePct:F0}%)");
                }
            }
        }

        // Slow operations (P95 > 2s this hour)
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name, service_name,
                       PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns/1e6) as p95_ms, COUNT(*) as cnt
                FROM spans WHERE start_time_unix_nano >= $1
                GROUP BY name, service_name HAVING p95_ms > 2000 ORDER BY p95_ms DESC LIMIT 5
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                hasAlerts = true;
                var name = reader.GetString(0);
                var service = reader.Col(1).AsString ?? "unknown";
                var p95Ms = reader.Col(2).GetDouble(0);
                sb.AppendLine($"- [SLOW] {name} in {service}: P95={FormatMs(p95Ms)} (threshold: 2s)");
            }
        }

        if (!hasAlerts)
            sb.AppendLine("No anomalies detected in the last hour.");

        return sb.ToString().TrimEnd();
    }

    private static string FormatMs(double ms) => ms switch
    {
        < 1000 => $"{ms:F0}ms",
        _ => $"{ms / 1000:F1}s"
    };
}
