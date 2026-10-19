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
            // SavedQueryRunRequest.limit defaults to the shared page size, not to the list tool's.
            filter["limit"] ??= 100;
            var page = row.Signal == "traces" ? Traces(filter) : Logs(filter);
            result = J.O(("signal", row.Signal), ("items", page[row.Signal]), ("has_more", page["has_more"]), ("next_cursor", page["next_cursor"]));
        }
        return J.O(("result", result), ("mode", "live"));
    }
}
