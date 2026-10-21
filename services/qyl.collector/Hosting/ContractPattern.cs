using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Qyl.Collector.Hosting;

/// <summary>
/// A contract pattern, an ECMAScript regular expression as JSON Schema's pattern and the MCP's Ajv read it, matched as
/// they match it. .NET reads the patterns the contract writes alike but for its <c>$</c>, which also matches before a final
/// newline, and its <c>\d</c> and <c>\w</c>, which take every Unicode digit and letter: <c>$</c> is read as <c>\z</c>, the
/// end of the value, and <c>\d</c> and <c>\w</c> as the ASCII classes ECMAScript's are.
/// </summary>
internal static class ContractPattern
{
    private static readonly ConcurrentDictionary<string, Regex> s_compiled = new(StringComparer.Ordinal);

    internal static bool IsMatch(string pattern, string value) =>
        s_compiled.GetOrAdd(pattern, static source => new Regex(Translate(source), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))).IsMatch(value);

    internal static string Translate(string pattern)
    {
        var translated = new StringBuilder(pattern.Length + 8);
        var inClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                var escaped = pattern[++i];
                translated.Append(escaped switch
                {
                    'd' => inClass ? "0-9" : "[0-9]",
                    'w' => inClass ? "A-Za-z0-9_" : "[A-Za-z0-9_]",
                    'D' or 'W' when inClass => throw new NotSupportedException($"Contract pattern {pattern} negates a class inside a class."),
                    'D' => "[^0-9]",
                    'W' => "[^A-Za-z0-9_]",
                    _ => $"\\{escaped}"
                });
                continue;
            }
            if (inClass) inClass = c != ']';
            else if (c == '[') inClass = true;
            translated.Append(!inClass && c == '$' ? "\\z" : c.ToString());
        }
        return translated.ToString();
    }
}
