namespace Qyl.Collector.Autofix;

internal static class LoomResponseParser
{
    internal static LoomRootCause? TryParseRootCause(string text)
    {
        var jsonStart = text.IndexOfOrdinal("{\"summary\"");
        if (jsonStart < 0)
            jsonStart = text.IndexOfOrdinal("```json");

        if (jsonStart < 0)
            return null;

        if (text[jsonStart] == '`')
        {
            jsonStart = text.IndexOf('{', jsonStart);
            if (jsonStart < 0)
                return null;
        }

        var json = ExtractJsonObject(text, jsonStart);
        if (json == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, LoomInsightJsonContext.Default.LoomRootCause);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static LoomSolution? TryParseSolution(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;

        var json = ExtractJsonObject(text, start);
        if (json == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, LoomInsightJsonContext.Default.LoomSolution);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractJsonObject(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
                depth--;

            if (depth == 0)
                return text[start..(i + 1)];
        }

        return "{}";
    }
}
