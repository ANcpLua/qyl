// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Workflows.Prompts;

/// <summary>
///     MCP prompt for reviewing and resolving review-bot (<c>sentry[bot]</c>,
///     <c>seer-by-sentry[bot]</c>) comments on GitHub pull requests. Encodes the exact
///     parsing contract so the caller can filter / verify / fix each comment
///     deterministically, without guessing.
/// </summary>
[McpServerPromptType]
internal sealed class ReviewBotPrompts
{
    [McpServerPrompt(Name = "qyl.loom.review_bot_pr",
        Title = "Review sentry[bot] / seer-by-sentry[bot] PR comments")]
    [Description("Processes review-bot PR comments. Caller provides the pre-parsed summary (from loom_parse_review_bot_comments).")]
    public static string ReviewBotPr(
        [Description("GitHub repo in owner/repo format.")]
        string repoFullName,
        [Description("Pull request number.")]
        int prNumber,
        [Description("Pre-parsed comment summary (from loom_parse_review_bot_comments). Required.")]
        string parsedSummary) =>
        $$"""
          You are driving Loom's **review-bot PR** workflow on `{{repoFullName}}#{{prNumber}}`.

          ## Scope
          - **Only process comments authored by Sentry-family review bots.** Bot logins Loom
            treats as review bots: `sentry[bot]`, `sentry-io[bot]`, `seer-by-sentry[bot]`, plus
            any login starting with `sentry` or `seer-by-sentry`. Silently skip every other bot
            (`cursor[bot]`, `dependabot[bot]`, `copilot[bot]`, etc.).
          - Pre-parsed comment batch follows. Use it as the source of truth — Loom's parser
            already stripped HTML, extracted severity/confidence, and separated the
            <i>Detailed Analysis</i>, <i>Suggested Fix</i>, and <i>Prompt for AI Agent</i> sections.

          ## Parsed comment batch
          ```
          {{parsedSummary}}
          ```

          ## Your workflow

          ### 1. Verify each comment still applies
          For every entry in the batch:
          - Read the referenced file at the referenced line.
          - Confirm the problematic code is still present — the PR may have been pushed again
            and already fixed the issue. Mark those "skipped (already fixed)" and move on.
          - If the line number is null (Seer file-level comment), scan the file for the pattern
            the Detailed Analysis describes.

          ### 2. Classify by severity + confidence
          Use the parsed `severity` + `confidence` fields:
          - `Critical` or `High` + `confidence >= 0.8` → always act on unless the issue is already
            fixed or a clear false positive.
          - `Medium` + `confidence >= 0.7` → act unless context strongly suggests otherwise.
          - `Low` / `Info` or `confidence < 0.5` → review manually; do not auto-apply.
          - `Unknown` severity → treat as `Medium`.

          ### 3. Implement targeted fixes
          - Use the `Suggested Fix` section as a starting point; the `Prompt for AI Agent` gives
            the specific instructions.
          - Make the minimal change that addresses the **root cause** — not a local patch that
            just silences the bot.
          - Respect the surrounding codebase style; no refactors outside the flagged lines.
          - If the suggested fix conflicts with the project's conventions, apply the spirit of
            the fix, not the letter, and note the deviation in the report.

          ### 4. Reject false positives explicitly
          When you skip an item, record **why** (already fixed in later commit, false positive due
          to context the bot missed, conflicts with an intentional design decision). The report
          section covers this — do not leave silently-skipped items.

          ### 5. Report
          Produce this structure:

          ```markdown
          ## Review-Bot Pass: {{repoFullName}} PR #{{prNumber}}

          ### Resolved
          | File:Line | Severity | Confidence | Fix Applied |
          |-----------|----------|------------|-------------|
          | path/to/file.cs:42 | HIGH | 0.92 | what you did |

          ### Skipped
          | File:Line | Severity | Reason |
          |-----------|----------|--------|
          | path/to/other.cs:17 | LOW | Already fixed in commit abc123 |

          ### Summary
          - Resolved: X
          - Skipped (already fixed): Y
          - Skipped (false positive): Z
          - Manual review required: W
          ```

          Do not fabricate commit hashes. If you cannot identify why a fix appears to be already
          applied, mark the row "manual review required" instead.
          """;
}
