using ANcpLua.Agents;
using ANcpLua.Roslyn.Utilities;

namespace Qyl.Loom.Exploration;

internal static class ExplorationResponseParser
{
    internal static ExplorationRootCause? TryParseRootCause(string text)
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

        return JsonHelper.TryDeserialize(json, ExplorationJsonContext.Default.ExplorationRootCause);
    }

    internal static ExplorationSolution? TryParseSolution(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;

        var json = ExtractJsonObject(text, start);
        if (json == "{}")
            return null;

        return JsonHelper.TryDeserialize(json, ExplorationJsonContext.Default.ExplorationSolution);
    }

    private static string ExtractJsonObject(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
            }

            if (depth is 0)
                return text[start..(i + 1)];
        }

        return "{}";
    }
}
