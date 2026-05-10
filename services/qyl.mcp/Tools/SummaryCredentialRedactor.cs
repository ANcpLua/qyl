using System.Text.Json;
using System.Text.RegularExpressions;

namespace qyl.mcp.Tools;

/// <summary>
/// Redacts credentials from summary text using shared credential patterns.
/// </summary>
public static class SummaryCredentialRedactor
{
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(250);
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
            try
            {
                result = rule.Regex.Replace(result, rule.Replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                result = "<redacted>";
                break;
            }
        }
        return result;
    }

    private static List<RedactionRule> LoadRules()
    {
        var patternsPath = Path.Join(AppContext.BaseDirectory, "credential-patterns.json");

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

            if (flags.ContainsOrdinal("i"))
            {
                options |= RegexOptions.IgnoreCase;
            }
            if (flags.ContainsOrdinal("m"))
            {
                options |= RegexOptions.Multiline;
            }
            if (flags.ContainsOrdinal("s"))
            {
                options |= RegexOptions.Singleline;
            }

            var regex = new Regex(pattern.Pattern, options | RegexOptions.Compiled, s_regexTimeout);
            rules.Add(new RedactionRule(regex, pattern.Replacement));
        }

        return rules;
    }
}
