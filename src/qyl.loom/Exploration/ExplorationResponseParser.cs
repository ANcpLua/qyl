using System.Text.Json;

namespace Qyl.Loom.Exploration;

internal static class ExplorationResponseParser
{
    internal static ExplorationRootCause? TryParseRootCause(string text)
    {
        var jsonStart = text.IndexOf("{\"summary\"", StringComparison.Ordinal);
        if (jsonStart < 0)
            jsonStart = text.IndexOf("```json", StringComparison.Ordinal);

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
            return JsonSerializer.Deserialize(json, ExplorationJsonContext.Default.ExplorationRootCause);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static ExplorationSolution? TryParseSolution(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;

        var json = ExtractJsonObject(text, start);
        if (json == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, ExplorationJsonContext.Default.ExplorationSolution);
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
