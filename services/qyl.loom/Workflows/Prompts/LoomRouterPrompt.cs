// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Workflows.Prompts;

/// <summary>
///     Router prompt — the one entry point for "I don't know which Loom workflow I want."
///     Embeds the four workflow shapes and the clarifying-question policy so downstream
///     agents route identically to the deterministic <see cref="LoomWorkflowRouter" />.
/// </summary>
[McpServerPromptType]
internal sealed class LoomRouterPrompt
{
    [McpServerPrompt(Name = "qyl.loom.route",
        Title = "Route a user request across Loom workflows")]
    [Description("Classifies a user request into one of the four Loom workflows; asks a clarifying question when ambiguous.")]
    public static string Route(
        [Description("User request in natural language.")]
        string userRequest,
        [Description("Optional JSON signals (pull_request_number, review_bot_author, issue_id). May be empty.")]
        string? signalsJson = null) =>
        $$"""
          You are Loom's workflow router. Classify the request into exactly one of four workflows,
          or return a single clarifying question when the signal is ambiguous.

          ## The four workflows
          | Workflow                 | Fetch this MCP prompt            | Use when                                                                         |
          |--------------------------|----------------------------------|----------------------------------------------------------------------------------|
          | Fix production issue     | `qyl.loom.fix_issue`             | User mentions fixing errors, debugging exceptions, investigating production bugs |
          | Review bot PR comments   | `qyl.loom.review_bot_pr`         | User mentions `sentry[bot]`, `seer-by-sentry[bot]`, or "resolve PR bot comments" |
          | Set up .NET SDK          | `qyl.loom.setup_dotnet`          | User mentions installing the Sentry .NET SDK, error monitoring, tracing, etc.    |
          | Set up AI monitoring     | `qyl.loom.setup_ai_monitoring`   | User mentions monitoring LLM calls, OpenAI, Anthropic, token usage, gen_ai spans |

          ## Input
          - User request: {{userRequest}}
          - Structured signals (JSON): `{{signalsJson ?? "{}"}}`

          ## Rules (hard)
          1. **Structured signals win.** If `signals.pull_request_number` AND `signals.review_bot_author`
             are set → bot-PR workflow. If `signals.issue_id` is set → fix-production workflow.
          2. **No guessing.** If the request matches tokens from ≥2 disjoint workflows, return a
             clarifying question listing the conflicting workflows. Do **not** pick the "most
             likely" one silently.
          3. **SDK + AI monitoring is not ambiguous.** AI monitoring requires the base SDK. Pick
             AI monitoring and note the SDK prerequisite.
          4. **Empty or unmatched request → clarify.** Never invent a workflow.

          ## Output format
          Return a JSON object with this exact shape:
          ```json
          {
            "workflow": "fix_production_issue | review_bot_pr_comments | setup_dotnet_sdk | setup_ai_monitoring | clarify",
            "confidence": 0.0,
            "rationale": "one sentence explaining why this workflow was chosen",
            "prompt_ids": ["qyl.loom.<picked>"],
            "clarifying_question": "only set when workflow == 'clarify'; otherwise null"
          }
          ```

          Do not output anything outside the JSON object. If you produce markdown, a header, or a
          preamble, the router caller will discard your output.
          """;
}
