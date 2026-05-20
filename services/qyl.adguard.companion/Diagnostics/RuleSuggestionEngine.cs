namespace Qyl.AdGuard.Companion.Diagnostics;

internal static class RuleSuggestionEngine
{
    public static RuleSuggestionResult Suggest(RuleSuggestParams parameters)
    {
        var pageHost = HostOrNull(parameters.PageUrl);
        var targetHost = HostOrNull(parameters.TargetUrl);
        var suggestions = new List<RuleSuggestion>();

        if (!string.IsNullOrWhiteSpace(parameters.Selector) && pageHost is not null)
        {
            var selector = SanitizeSelector(parameters.Selector);
            if (selector is not null)
            {
                suggestions.Add(new RuleSuggestion(
                    Kind: "cosmetic",
                    Rule: $"{pageHost}##{selector}",
                    Confidence: "medium",
                    Rationale: "Hides the selected element on the current site only."));
            }
        }

        if (targetHost is not null)
        {
            var domainPart = pageHost is null || pageHost.Equals(targetHost, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $",domain={pageHost}";
            suggestions.Add(new RuleSuggestion(
                Kind: "network",
                Rule: $"||{targetHost}^$third-party{domainPart}",
                Confidence: pageHost is null ? "medium" : "high",
                Rationale: "Blocks third-party requests to the observed host without changing AdGuard settings."));
        }

        if (suggestions.Count is 0)
        {
            suggestions.Add(new RuleSuggestion(
                Kind: "manual",
                Rule: string.Empty,
                Confidence: "low",
                Rationale: "No safe AdGuard rule could be derived from the summarized inputs."));
        }

        return new RuleSuggestionResult(
            PageHost: pageHost,
            TargetHost: targetHost,
            Suggestions: suggestions.ToArray(),
            MutatedAdGuard: false);
    }

    private static string? HostOrNull(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : null;

    private static string? SanitizeSelector(string selector)
    {
        var trimmed = selector.Trim();
        if (trimmed.Length is 0 or > 200)
            return null;

        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
            return null;

        return trimmed;
    }
}

internal sealed class RuleSuggestParams
{
    public string? PageUrl { get; init; }

    public string? TargetUrl { get; init; }

    public string? Selector { get; init; }

    public string? Reason { get; init; }
}

internal sealed record RuleSuggestionResult(
    string? PageHost,
    string? TargetHost,
    RuleSuggestion[] Suggestions,
    bool MutatedAdGuard);

internal sealed record RuleSuggestion(string Kind, string Rule, string Confidence, string Rationale);
