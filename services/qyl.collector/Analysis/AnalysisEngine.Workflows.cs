using System.Text.Json.Nodes;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed partial class AnalysisEngine
{
    // The run's own window and page override the saved filter; nothing else of the request does.
    private static readonly string[] s_runOverrides = ["lookback_minutes", "start_time", "end_time", "limit", "cursor"];

    // Without an id, the project's saved queries; with one, that query answered over current telemetry.
    private JsonObject RunSavedQuery(JsonObject input)
    {
        if (J.String(input, "id") is not { } id)
            return Page(input, "saved_queries", snapshot.SavedQueries.Where(row => J.String(input, "signal") is not { } signal || row.Signal == signal)
                .OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.SavedQueryId, StringComparer.Ordinal).Select(SavedQueryJson));
        var row = snapshot.SavedQueries.FirstOrDefault(row => row.SavedQueryId == id)
            ?? throw new AnalysisInputException("id", $"Saved query {id} was not found in this project; run_saved_query without id lists the saved queries.", "saved_query.not_found") { Missing = id };
        var filter = JsonNode.Parse(row.FilterJson)!.AsObject();
        filter["id"] = row.SavedQueryId;
        foreach (var name in s_runOverrides) if (input[name] is { } value) filter[name] = value.DeepClone();
        _window = AnalysisWindow.Resolve(filter, now);
        if (J.String(filter, "cursor") is { } cursor && handles.Read(cursor, project, "cursor")["window"] is JsonObject window) _window = AnalysisWindow.Resolve(window, now);
        var output = J.O(("saved_query", SavedQueryJson(row)));
        if (row.Signal == "metrics")
        {
            var metric = QueryMetric(filter);
            output["coverage"] = metric["coverage"]?.DeepClone();
            output["metric"] = metric;
            return output;
        }
        var page = row.Signal == "traces" ? Traces(filter) : Logs(filter);
        // The run covers what its saved filter examined and matched, not the bare id request.
        foreach (var field in new[] { row.Signal, "has_more", "next_cursor", "coverage" }) output[field] = page[field]?.DeepClone();
        return output;
    }

    private static JsonObject SavedQueryJson(SavedQueryRow row) =>
        JsonSerializer.SerializeToNode(SavedQueryMapper.ToContract(row), QylSerializerContext.Default.SavedQuery)!.AsObject();
}
