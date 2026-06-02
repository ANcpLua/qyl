using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Insights;

internal static class AlertsMaterializer
{
    public static Task<string> MaterializeAsync(
        DuckDbStore store, TimeProvider timeProvider, CancellationToken ct) =>
        store.ExecuteReadAsync(con =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Known Issues (auto-detected)");

            var now = timeProvider.GetUtcNow();
            var oneHourAgoNanos = (decimal)TimeConversions.ToUnixNanoUnsigned(now.AddHours(-1));
            var twoHoursAgoNanos = (decimal)TimeConversions.ToUnixNanoUnsigned(now.AddHours(-2));
            var twentyFourHoursAgoNanos = (decimal)TimeConversions.ToUnixNanoUnsigned(now.AddHours(-24));

            var hasAlerts = false;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT service_name,
                                         SUM(CASE WHEN start_time_unix_nano >= $1 THEN 1 ELSE 0 END) as this_hr,
                                         SUM(CASE WHEN start_time_unix_nano < $1 THEN 1 ELSE 0 END) as prev_hr
                                  FROM spans WHERE TRY_CAST(status_code AS INTEGER) = 2 AND start_time_unix_nano >= $2
                                  GROUP BY service_name HAVING this_hr > prev_hr * 2 AND this_hr > 5
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });
                cmd.Parameters.Add(new DuckDBParameter { Value = twoHoursAgoNanos });

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    hasAlerts = true;
                    var service = reader.Col(0).AsString ?? "unknown";
                    var thisHr = reader.Col(1).GetInt64(0);
                    var prevHr = reader.Col(2).GetInt64(0);
                    var changePct = prevHr > 0 ? (thisHr - prevHr) * 100 / prevHr : 100;
                    sb.AppendLine($"- [ERROR SPIKE] {service}: {thisHr} errors/hr (was {prevHr}/hr, +{changePct}%)");
                }
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT COALESCE(SUM(CASE WHEN start_time_unix_nano >= $1 THEN gen_ai_cost_usd ELSE 0 END),0) as this_hr,
                                         COALESCE(SUM(gen_ai_cost_usd),0)/24.0 as avg_hr
                                  FROM spans WHERE gen_ai_provider_name IS NOT NULL AND start_time_unix_nano >= $2
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });
                cmd.Parameters.Add(new DuckDBParameter { Value = twentyFourHoursAgoNanos });

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var thisHr = reader.Col(0).GetDouble(0);
                    var avgHr = reader.Col(1).GetDouble(0);

                    if (avgHr > 0.01 && thisHr > avgHr * 1.5)
                    {
                        hasAlerts = true;
                        var changePct = (thisHr - avgHr) / avgHr * 100;
                        sb.AppendLine(
                            $"- [COST] Token spend this hour: ${thisHr:F2} vs ${avgHr:F2}/hr avg (+{changePct:F0}%)");
                    }
                }
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT name, service_name,
                                         PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns/1e6) as p95_ms, COUNT(*) as cnt
                                  FROM spans WHERE start_time_unix_nano >= $1
                                  GROUP BY name, service_name HAVING p95_ms > 2000 ORDER BY p95_ms DESC LIMIT 5
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = oneHourAgoNanos });

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
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
        }, ct);

    private static string FormatMs(double ms) => ms switch
    {
        < 1000 => $"{ms:F0}ms",
        _ => $"{ms / 1000:F1}s"
    };
}
