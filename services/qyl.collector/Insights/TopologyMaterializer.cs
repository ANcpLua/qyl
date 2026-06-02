namespace Qyl.Collector.Insights;

internal static class TopologyMaterializer
{
    public static Task<string> MaterializeAsync(
        DuckDbStore store, TimeProvider _, CancellationToken ct) =>
        store.ExecuteReadAsync(con =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Topology (discovered)");

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT service_name, COUNT(*) as span_count
                                  FROM spans WHERE service_name IS NOT NULL
                                  GROUP BY service_name ORDER BY span_count DESC LIMIT 20
                                  """;

                using var reader = cmd.ExecuteReader();
                var services = new List<string>();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var count = reader.GetInt64(1);
                    services.Add($"{name} ({count:N0} spans)");
                }

                if (services.Count > 0)
                    sb.AppendLine($"- {services.Count} service(s): {string.Join(", ", services)}");
                else
                    sb.AppendLine("- No services discovered yet.");
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT gen_ai_request_model, gen_ai_provider_name,
                                         COUNT(*) as call_count,
                                         ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER(), 1) as pct
                                  FROM spans WHERE gen_ai_request_model IS NOT NULL
                                  GROUP BY gen_ai_request_model, gen_ai_provider_name
                                  ORDER BY call_count DESC LIMIT 10
                                  """;

                using var reader = cmd.ExecuteReader();
                var models = new List<string>();
                while (reader.Read())
                {
                    var model = reader.GetString(0);
                    var provider = reader.Col(1).AsString ?? "unknown";
                    var pct = reader.Col(3).GetDouble(0);
                    models.Add($"{model} via {provider} ({pct:F1}%)");
                }

                if (models.Count > 0)
                    sb.AppendLine($"- {models.Count} AI model(s): {string.Join(", ", models)}");
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = """
                                  SELECT service_name, status_message, COUNT(*) as error_count
                                  FROM spans WHERE TRY_CAST(status_code AS INTEGER) = 2 AND status_message IS NOT NULL
                                  GROUP BY service_name, status_message ORDER BY error_count DESC LIMIT 5
                                  """;

                using var reader = cmd.ExecuteReader();
                var hasErrors = false;
                while (reader.Read())
                {
                    hasErrors = true;
                    var service = reader.Col(0).AsString ?? "unknown";
                    var message = reader.GetString(1);
                    var count = reader.GetInt64(2);
                    if (message.Length > 80)
                        message = string.Concat(message.AsSpan(0, 77), "...");
                    sb.AppendLine($"- Top error: {message} in {service} ({count:N0} occurrences)");
                }

                if (!hasErrors)
                    sb.AppendLine("- No errors recorded.");
            }

            return sb.ToString().TrimEnd();
        }, ct);
}
