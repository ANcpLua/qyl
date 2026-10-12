using System.Text.Json.Nodes;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    private JsonObject SavedQueries(JsonObject input)
    {
        var rows = snapshot.SavedQueries.Where(row => J.String(input, "signal") is not { } signal || row.Signal == signal)
            .OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.SavedQueryId, StringComparer.Ordinal)
            .Select(row => JsonSerializer.SerializeToNode(SavedQueryMapper.ToContract(row), QylSerializerContext.Default.SavedQuery)!.AsObject());
        return Page(input, "saved_queries", rows, 20, 100);
    }
    private JsonObject RunSavedQuery(JsonObject input)
    {
        var row = snapshot.SavedQueries.FirstOrDefault(row => row.SavedQueryId == J.String(input, "id"))
            ?? throw new AnalysisInputException("id", "Saved query was not found in this project.", "saved_query.not_found");
        var filter = JsonNode.Parse(row.FilterJson)!.AsObject();
        filter["id"] = row.SavedQueryId;
        foreach (var value in input.Where(p => p.Key != "id")) filter[value.Key] = value.Value?.DeepClone();
        _window = AnalysisWindow.Resolve(filter, now);
        if (J.String(filter, "cursor") is { } cursor && handles.Read(cursor, project, "cursor")["window"] is JsonObject window) _window = AnalysisWindow.Resolve(window, now);
        JsonObject result;
        if (row.Signal == "metrics") result = J.O(("signal", "metrics"), ("result", QueryMetric(filter)));
        else
        {
            var page = row.Signal == "traces" ? Traces(filter) : Logs(filter);
            result = J.O(("signal", row.Signal), ("items", page[row.Signal]), ("has_more", page["has_more"]), ("next_cursor", page["next_cursor"]));
        }
        return J.O(("result", result), ("mode", "live"));
    }
    private JsonObject Fetch(JsonObject input)
    {
        var scope = input.DeepClone().AsObject(); scope.Remove("since_cursor"); scope.Remove("fields");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope.ToJsonString())));
        if (J.String(input, "since_cursor") is { } cursor)
        {
            var previous = handles.Read(cursor, project, "refresh");
            if (J.String(previous, "scope") != fingerprint) throw new AnalysisInputException("since_cursor", "Use the refresh cursor with the same view and filters.", "cursor.invalid");
            if (J.Date(previous, "as_of") is { } last) _window = new(last, _window.End);
        }
        var view = J.String(input, "view") ?? "traces";
        var result = view switch
        {
            "trace" => Trace(input), "logs" => Logs(input), "mcp_stats" => McpDashboard(input),
            "compare" => J.O(("comparison", CompareTraces(input)), ("mode", "live")),
            "traces" => Traces(input), _ => throw new AnalysisInputException("view", "Unknown telemetry view.")
        };
        result["mode"] = "live";
        result["since_cursor"] = handles.Issue(project, "refresh", J.O(("scope", fingerprint), ("as_of", now.ToString("O"))));
        if (input["fields"] is JsonArray fields)
        {
            var selected = fields.Select(J.Text).ToHashSet(StringComparer.Ordinal);
            foreach (var key in result.Select(p => p.Key).Where(key => !selected.Contains(key) && key is not ("mode" or "window" or "coverage" or "since_cursor" or "next_cursor")).ToArray()) result.Remove(key);
        }
        return result;
    }
}
