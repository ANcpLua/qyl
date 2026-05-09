using System.Text.Json;
using System.Text.RegularExpressions;

namespace qyl.mcp.Tools;

/// <summary>
/// Redacts credentials from summary text using shared credential patterns.
/// </summary>
public static class SummaryCredentialRedactor
{
    private static readonly Lazy<List<RedactionRule>> s_rules = new(LoadRules);

    private sealed record CredentialPattern(
        string Description,
        string Pattern,
        string Replacement,
        string? Flags);

    private sealed record RedactionRule(Regex Regex, string Replacement);

    /// <summary>
    /// Redacts credentials and sensitive data from the input text.
    /// </summary>
    /// <param name="input">The text to redact.</param>
    /// <returns>The redacted text with credentials replaced.</returns>
    public static string Redact(string input)
    {
        var result = input;
        foreach (var rule in s_rules.Value)
        {
            result = rule.Regex.Replace(result, rule.Replacement);
        }
        return result;
    }

    private static List<RedactionRule> LoadRules()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(SummaryCredentialRedactor).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to determine assembly location");
        var patternsPath = Path.Combine(assemblyDir, "credential-patterns.json");

        if (!File.Exists(patternsPath))
        {
            throw new FileNotFoundException(
                $"Credential patterns file not found at {patternsPath}");
        }

        var json = File.ReadAllText(patternsPath);
        var patterns = JsonSerializer.Deserialize<List<CredentialPattern>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize credential patterns");

        var rules = new List<RedactionRule>();
        foreach (var pattern in patterns)
        {
            var options = RegexOptions.None;
            var flags = pattern.Flags ?? "gi";

            if (flags.Contains('i', StringComparison.Ordinal))
            {
                options |= RegexOptions.IgnoreCase;
            }
            if (flags.Contains('m', StringComparison.Ordinal))
            {
                options |= RegexOptions.Multiline;
            }
            if (flags.Contains('s', StringComparison.Ordinal))
            {
                options |= RegexOptions.Singleline;
            }

            var regex = new Regex(pattern.Pattern, options | RegexOptions.Compiled);
            rules.Add(new RedactionRule(regex, pattern.Replacement));
        }

        return rules;
    }
}
