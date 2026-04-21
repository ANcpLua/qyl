// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

/// <summary>
///     LLM-output JSON extraction helper. Handles responses where the model pads the structured JSON
///     with commentary or markdown fences by scanning for the first balanced <c>{...}</c>.
/// </summary>
internal static class AutofixJson
{
    /// <summary>
    ///     Returns the first balanced JSON object substring in <paramref name="text" />, or <c>"{}"</c>
    ///     if the text does not contain a balanced object.
    /// </summary>
    public static string ExtractObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return "{}";

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

            if (depth is 0) return text[start..(i + 1)];
        }

        return "{}";
    }
}
