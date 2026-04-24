// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Workflows.ReviewBot;

/// <summary>
///     A single parsed review-bot comment. Field shape mirrors the markdown body posted by
///     <c>sentry[bot]</c> and <c>seer-by-sentry[bot]</c> on GitHub pull requests:
///     <list type="bullet">
///         <item><c>**Bug:** [Issue description]</c></item>
///         <item><c>&lt;sub&gt;Severity: X | Confidence: X.XX&lt;/sub&gt;</c></item>
///         <item><c>&lt;details&gt;&lt;summary&gt;🔍 &lt;b&gt;Detailed Analysis&lt;/b&gt;&lt;/summary&gt;...</c></item>
///         <item><c>&lt;details&gt;&lt;summary&gt;💡 &lt;b&gt;Suggested Fix&lt;/b&gt;&lt;/summary&gt;...</c></item>
///         <item><c>&lt;details&gt;&lt;summary&gt;🤖 &lt;b&gt;Prompt for AI Agent&lt;/b&gt;&lt;/summary&gt;...</c></item>
///     </list>
///     Every field is trimmed plain text. Parser never propagates raw HTML to callers.
/// </summary>
public sealed record ReviewBotComment
{
    /// <summary>Bot author login (e.g. <c>sentry[bot]</c>, <c>seer-by-sentry[bot]</c>).</summary>
    public required string Author { get; init; }

    /// <summary>Source file path from the comment metadata. Repo-relative.</summary>
    public required string File { get; init; }

    /// <summary>Line number (nullable — Seer emits file-level comments too).</summary>
    public required int? Line { get; init; }

    /// <summary>Header <c>**Bug:**</c> text, trimmed. Empty if the body did not carry one.</summary>
    public required string Bug { get; init; }

    /// <summary>Parsed severity, <see cref="ReviewBotSeverity.Unknown" /> if absent.</summary>
    public required ReviewBotSeverity Severity { get; init; }

    /// <summary>Original severity text verbatim (e.g. "CRITICAL", "HIGH") — preserved for reporting.</summary>
    public required string SeverityText { get; init; }

    /// <summary>Confidence 0–1, <c>null</c> if absent.</summary>
    public required double? Confidence { get; init; }

    /// <summary>Plain-text detailed analysis (HTML stripped).</summary>
    public required string DetailedAnalysis { get; init; }

    /// <summary>Plain-text suggested fix description (HTML stripped).</summary>
    public required string SuggestedFix { get; init; }

    /// <summary>Plain-text agent prompt (HTML stripped).</summary>
    public required string AgentPrompt { get; init; }
}

/// <summary>
///     Parsed severity labels. Unknown values round-trip as <see cref="Unknown" /> while
///     preserving the original text on <see cref="ReviewBotComment.SeverityText" />.
/// </summary>
public enum ReviewBotSeverity
{
    /// <summary>Severity text absent or unrecognised.</summary>
    Unknown = 0,

    /// <summary>INFO severity.</summary>
    Info = 1,

    /// <summary>LOW severity.</summary>
    Low = 2,

    /// <summary>MEDIUM severity.</summary>
    Medium = 3,

    /// <summary>HIGH severity.</summary>
    High = 4,

    /// <summary>CRITICAL severity.</summary>
    Critical = 5,
}

/// <summary>
///     Raw comment shape fetched from GitHub (<c>gh api /repos/{owner}/{repo}/pulls/{n}/comments</c>).
///     Author is the bot login; Body is the unparsed markdown/HTML.
/// </summary>
public sealed record ReviewBotRawComment(string? Author, string? File, int? Line, string? Body);
