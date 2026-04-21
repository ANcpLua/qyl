using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.CodeReview;

/// <summary>
///     Prompt builder for LLM-based pull request code review.
///     Exposed via MCP <c>prompts/list</c> so clients can fetch the structured review template.
/// </summary>
[McpServerPromptType]
internal sealed class CodeReviewPrompt
{
    [McpServerPrompt(Name = "qyl.code_review", Title = "Code Review")]
    [Description("Generates a structured JSON review of a pull request diff against known error patterns.")]
    public static string Build(
        [Description("Pull request title")] string prTitle,
        [Description("Unified diff content of the PR")]
        string diffContent,
        [Description("Known error patterns in this service (optional markdown list)")]
        string? knownErrorPatterns = null) =>
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
