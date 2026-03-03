namespace qyl.collector.Autofix;

/// <summary>
///     Prompt builder for LLM-based pull request code review.
///     Returns a structured prompt that instructs the model to produce a JSON array
///     of actionable review comments.
/// </summary>
internal static class CodeReviewPrompt
{
    public static string Build(string prTitle, string diffContent, string? knownErrorPatterns = null) =>
        $$"""
        You are a code reviewer for an observability platform. Analyze this PR diff and provide structured feedback.

        ## PR: {{prTitle}}

        ## Diff
        ```
        {{diffContent}}
        ```

        {{(knownErrorPatterns is not null ? $"## Known Error Patterns in This Service\n{knownErrorPatterns}\n" : "")}}

        ## Instructions
        Review for: bugs, security issues, error handling, performance, and patterns that match known errors.

        Return a JSON array of review comments. Each comment:
        ```json
        [
          {
            "file": "path/to/file.cs",
            "line": 42,
            "severity": "critical|warning|suggestion",
            "comment": "Description of the issue",
            "suggestion": "Optional code suggestion"
          }
        ]
        ```

        If the code looks good, return an empty array: []
        Only include actionable, specific feedback. No generic praise.
        """;
}
