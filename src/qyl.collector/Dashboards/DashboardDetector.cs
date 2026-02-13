namespace qyl.collector.Dashboards;

/// <summary>
///     Detects which dashboards should be available based on telemetry in DuckDB.
///     Each detection is a fast EXISTS query (returns immediately once one row matches).
/// </summary>
public sealed class DashboardDetector(DuckDbStore store)
{
    private static readonly IReadOnlyList<DashboardTemplate> Templates =
    [
        new("api-performance", "API Performance",
            "Routes ranked by p75/p95 latency, error rates, and throughput",
            "activity",
            "SELECT 1 FROM spans WHERE attributes_json LIKE '%http.request.method%' LIMIT 1"),

        new("external-apis", "External APIs",
            "Outbound HTTP calls, latency, and failure rates",
            "globe",
            "SELECT 1 FROM spans WHERE (attributes_json LIKE '%http.client%' OR kind = 3) AND attributes_json LIKE '%http.%' LIMIT 1"),

        new("genai", "AI / GenAI",
            "Token usage by model, cost tracking, and latency",
            "brain",
            "SELECT 1 FROM spans WHERE gen_ai_provider_name IS NOT NULL OR gen_ai_request_model IS NOT NULL LIMIT 1"),

        new("database", "Database",
            "Slow queries, call frequency, and error rates by system",
            "database",
            "SELECT 1 FROM spans WHERE attributes_json LIKE '%db.system%' LIMIT 1"),

        new("error-tracker", "Error Tracker",
            "Errors grouped by type, frequency, and recent occurrences",
            "alert-triangle",
            "SELECT 1 FROM logs WHERE severity_number >= 17 LIMIT 1"),

        new("messaging", "Message Queues",
            "Messaging throughput, latency, and consumer lag",
            "message-square",
            "SELECT 1 FROM spans WHERE attributes_json LIKE '%messaging.system%' LIMIT 1"),
    ];

    public async Task<IReadOnlyList<DashboardDefinition>> DetectAsync(CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        var results = new List<DashboardDefinition>(Templates.Count);

        foreach (var tpl in Templates)
        {
            var available = await ExistsAsync(lease.Connection, tpl.DetectionQuery, ct).ConfigureAwait(false);
            results.Add(new DashboardDefinition(tpl.Id, tpl.Title, tpl.Description, tpl.Icon, available));
        }

        return results;
    }

    private static async Task<bool> ExistsAsync(DuckDBConnection con, string query, CancellationToken ct)
    {
        try
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = query;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Table might not exist yet (no telemetry ingested)
            return false;
        }
    }

    internal sealed record DashboardTemplate(
        string Id,
        string Title,
        string Description,
        string Icon,
        string DetectionQuery);
}
