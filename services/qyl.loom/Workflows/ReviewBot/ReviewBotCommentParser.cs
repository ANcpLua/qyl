// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Qyl.Loom.Workflows.ReviewBot;

/// <summary>
///     Deterministic parser for <c>sentry[bot]</c> and <c>seer-by-sentry[bot]</c> PR review
///     comments. Only processes comments whose author login starts with <c>sentry</c> or
///     matches <c>seer-by-sentry[bot]</c>.
/// </summary>
/// <remarks>
///     <para>Pure — no IO, no LLM, no Sentry SDK. Input is an already-fetched list of
///     GitHub review comments (author / file / line / body). Output is a structured batch
///     ready for either direct LLM consumption or deterministic triage.</para>
///     <para>Unknown comment shapes are preserved best-effort: missing sections resolve to
///     empty strings rather than exceptions, so a partial match still yields actionable data.</para>
/// </remarks>
public static partial class ReviewBotCommentParser
{
    [GeneratedRegex(
        @"<sub>\s*Severity\s*:\s*(?<sev>[A-Za-z0-9_ -]+?)(?:\s*\|\s*Confidence\s*:\s*(?<conf>[0-9.]+))?\s*</sub>",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex SeverityRegex();

    [GeneratedRegex(
        @"\*\*Bug\s*:\s*\*\*\s*(?<bug>.+?)(?=\r?\n|$)",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex BugRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex CollapseSpaceRegex();

    /// <summary>
    ///     Bot author logins Loom treats as review-bot comments. Case-insensitive match.
    /// </summary>
    public static readonly ImmutableArray<string> KnownBotLogins =
    [
        "sentry[bot]",
        "sentry-io[bot]",
        "seer-by-sentry[bot]",
    ];

    /// <summary>
    ///     True when <paramref name="login" /> is a review bot. Case-insensitive. The
    ///     <c>startswith("sentry")</c> rule is intentionally broader than the exact-match
    ///     list to tolerate bot-login evolution.
    /// </summary>
    public static bool IsReviewBot(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return false;
        if (KnownBotLogins.Contains(login, StringComparer.OrdinalIgnoreCase)) return true;
        return login.StartsWith("sentry", StringComparison.OrdinalIgnoreCase) ||
               login.StartsWith("seer-by-sentry", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Parse a batch of raw GitHub review comments. Non-review-bot authors are silently
    ///     dropped (NOT errors — PRs often have <c>cursor[bot]</c>, <c>dependabot[bot]</c>,
    ///     etc. alongside).
    /// </summary>
    public static ImmutableArray<ReviewBotComment> Parse(IEnumerable<ReviewBotRawComment> comments)
    {
        ArgumentNullException.ThrowIfNull(comments);

        var builder = ImmutableArray.CreateBuilder<ReviewBotComment>();
        foreach (var raw in comments)
        {
            if (!IsReviewBot(raw.Author)) continue;
            builder.Add(ParseOne(raw));
        }

        return builder.ToImmutable();
    }

    /// <summary>Parse a single raw comment. Caller is responsible for bot-filtering.</summary>
    public static ReviewBotComment ParseOne(ReviewBotRawComment raw)
    {
        var body = raw.Body ?? "";

        var bug = BugRegex().Match(body) is { Success: true } bugMatch
            ? StripHtml(bugMatch.Groups["bug"].Value).Trim()
            : "";

        var severityText = "";
        var severity = ReviewBotSeverity.Unknown;
        double? confidence = null;

        if (SeverityRegex().Match(body) is { Success: true } sevMatch)
        {
            severityText = sevMatch.Groups["sev"].Value.Trim();
            severity = ClassifySeverity(severityText);

            if (sevMatch.Groups["conf"].Success &&
                double.TryParse(sevMatch.Groups["conf"].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedConf))
            {
                confidence = parsedConf;
            }
        }

        var analysis = ExtractDetailsSection(body, "Detailed Analysis");
        var fix = ExtractDetailsSection(body, "Suggested Fix");
        var prompt = ExtractDetailsSection(body, "Prompt for AI Agent");

        return new ReviewBotComment
        {
            Author = raw.Author ?? "",
            File = raw.File ?? "",
            Line = raw.Line,
            Bug = bug,
            Severity = severity,
            SeverityText = severityText,
            Confidence = confidence,
            DetailedAnalysis = analysis,
            SuggestedFix = fix,
            AgentPrompt = prompt,
        };
    }

    /// <summary>
    ///     Human-readable summary of a parsed batch. Ordered by severity descending,
    ///     confidence descending, then file / line. Safe to include in a prompt.
    /// </summary>
    public static string BuildSummary(IEnumerable<ReviewBotComment> comments)
    {
        ArgumentNullException.ThrowIfNull(comments);

        var ordered = comments
            .OrderByDescending(static c => (int)c.Severity)
            .ThenByDescending(static c => c.Confidence ?? 0)
            .ThenBy(static c => c.File, StringComparer.Ordinal)
            .ThenBy(static c => c.Line ?? 0)
            .ToArray();

        if (ordered.Length is 0)
            return "No review-bot comments parsed.";

        var sb = new System.Text.StringBuilder(capacity: 1024);
        sb.Append("Parsed ").Append(ordered.Length).Append(" review-bot comment(s).\n");

        for (var i = 0; i < ordered.Length; i++)
        {
            var c = ordered[i];
            var location = c.Line is int line ? $"{c.File}:{line}" : c.File;
            sb.Append("\n[").Append(i + 1).Append("] ").Append(location)
                .Append("  (author=").Append(c.Author)
                .Append(", severity=").Append(c.Severity).Append('/').Append(c.SeverityText);
            if (c.Confidence is double conf)
                sb.Append(", confidence=").Append(conf.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(")\n  bug: ").Append(c.Bug.Length > 0 ? c.Bug : "(no header)");
            if (c.SuggestedFix.Length > 0)
                sb.Append("\n  suggested fix: ").Append(Truncate(c.SuggestedFix, 240));
        }

        return sb.ToString();
    }

    private static ReviewBotSeverity ClassifySeverity(string text) => text.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => ReviewBotSeverity.Critical,
        "HIGH" => ReviewBotSeverity.High,
        "MEDIUM" or "MED" => ReviewBotSeverity.Medium,
        "LOW" => ReviewBotSeverity.Low,
        "INFO" or "INFORMATIONAL" => ReviewBotSeverity.Info,
        _ => ReviewBotSeverity.Unknown,
    };

    private static string ExtractDetailsSection(string body, string summaryTitle)
    {
        var lowerBody = body;
        var lowerTitle = summaryTitle.ToLowerInvariant();

        var searchFrom = 0;
        while (searchFrom < lowerBody.Length)
        {
            var detailsIdx = lowerBody.IndexOf("<details", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (detailsIdx < 0) return "";

            var detailsEnd = lowerBody.IndexOf("</details>", detailsIdx, StringComparison.OrdinalIgnoreCase);
            if (detailsEnd < 0) return "";

            var block = lowerBody.AsSpan(detailsIdx, detailsEnd - detailsIdx);
            if (block.ToString().Contains(lowerTitle, StringComparison.OrdinalIgnoreCase))
            {
                var summaryCloseIdx = lowerBody.IndexOf("</summary>", detailsIdx, StringComparison.OrdinalIgnoreCase);
                if (summaryCloseIdx < 0 || summaryCloseIdx > detailsEnd) return "";
                var inner = lowerBody.AsSpan(summaryCloseIdx + "</summary>".Length,
                    detailsEnd - (summaryCloseIdx + "</summary>".Length));
                return Normalise(StripHtml(inner.ToString()));
            }

            searchFrom = detailsEnd + "</details>".Length;
        }

        return "";
    }

    private static string StripHtml(string text) => HtmlTagRegex().Replace(text, "");

    private static string Normalise(string text)
    {
        var trimmed = text.Trim();
        trimmed = CollapseSpaceRegex().Replace(trimmed, " ");
        return trimmed;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return string.Concat(text.AsSpan(0, maxLength - 1), "…");
    }
}
