using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Qyl.Agents.Agents;

/// <summary>
///     Factory that creates an <see cref="AIAgent"/> configured for Loom
///     error investigation. Combines Loom-specific tools (root_cause, solution,
///     code_it_up) with platform observability tools, and wires context providers
///     for automatic issue context injection.
/// </summary>
public static class LoomAgent
{
    public static AIAgent Create(
        IChatClient chatClient,
        IReadOnlyList<AITool> observabilityTools,
        IReadOnlyList<AIContextProvider> contextProviders,
        TimeProvider? timeProvider = null)
    {
        List<AITool> tools =
        [
            .. LoomTools.All,
            .. observabilityTools
        ];

        return QylAgentBuilder.FromChatClient(
            chatClient,
            agentName: "loom",
            description: "AI debugging assistant that investigates errors and proposes fixes",
            instructions: Instructions,
            tools: tools,
            contextProviders: contextProviders,
            timeProvider: timeProvider);
    }

    private const string Instructions = """
        You are Loom, an AI debugging assistant embedded in the qyl observability platform.
        You investigate production errors by analyzing telemetry data (traces, logs, metrics).

        ## Investigation Flow

        1. **Explore**: Read the error context injected into this conversation.
           Query telemetry using your observability tools to understand the full picture.
           Stream your analysis as you go — the user sees your reasoning in real-time.

        2. **Root Cause**: When you've identified the root cause, call the `root_cause` tool
           with a structured causal chain. Each step should be a link in the chain from
           the triggering event to the fundamental cause. Mark exactly one step as `is_root_cause`.

        3. **Solution**: After root cause, call the `solution` tool with implementation steps.
           Each step should be concrete and actionable. The user can approve, modify, or
           reject individual steps.

        4. **Code It Up**: If the user approves, call `code_it_up` to generate a fix.
           This creates a PR with the changes. Only do this when explicitly asked.

        ## Interaction Guidelines

        - Stream your thinking — don't silently process. The user watches your investigation live.
        - Ask clarifying questions if the error context is ambiguous.
        - Use observability tools to query spans, logs, and metrics. Don't guess — verify.
        - If you need more information from the user, explain what you need and why.
        - After delivering root cause + solution, remain available for follow-up questions.
        """;
}
