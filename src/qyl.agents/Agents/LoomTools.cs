using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Contracts.Autofix;

namespace Qyl.Agents.Agents;

/// <summary>
///     Tools exposed to the LoomAgent. Tool calls drive AG-UI stage transitions:
///     <c>root_cause</c> → RootCause stage, <c>solution</c> → Solution stage,
///     <c>code_it_up</c> → CodeItUp stage.
/// </summary>
public static class LoomTools
{
    /// <summary>Key in AgentSession.StateBag for the current Loom session ID.</summary>
    public const string SessionIdKey = "qyl.loomSessionId";

    public static IReadOnlyList<AITool> All { get; } =
    [
        AIFunctionFactory.Create(RootCause),
        AIFunctionFactory.Create(Solution),
        AIFunctionFactory.Create(CodeItUp)
    ];

    [Description("Report the root cause analysis as a structured causal chain. " +
        "Call this once you've identified the root cause.")]
    private static LoomRootCauseResult RootCause(
        [Description("One-sentence summary of the root cause")]
        string summary,
        [Description("Ordered causal chain from trigger to root cause")]
        LoomCausalStep[] steps)
    {
        return new LoomRootCauseResult(summary, steps);
    }

    [Description("Report the proposed solution as implementation steps. " +
        "Call this after root cause analysis.")]
    private static LoomSolutionResult Solution(
        [Description("One-sentence summary of the fix")]
        string summary,
        [Description("Ordered implementation steps")]
        LoomSolutionStep[] steps)
    {
        return new LoomSolutionResult(summary, steps);
    }

    [Description("Generate a code fix and open a pull request. " +
        "Only call when the user explicitly asks to code it up.")]
    private static LoomCodeItUpResult CodeItUp(
        [Description("Repository full name (owner/repo)")]
        string repo,
        [Description("Base branch for the PR (default: main)")]
        string? baseBranch,
        AgentSession agentSession)
    {
        // code_it_up is a signal tool — returns intent, not action.
        // LoomAguiEndpoints intercepts TOOL_CALL_END for "code_it_up"
        // and orchestrates the actual fix run via AutofixOrchestrator.
        string? sessionId = agentSession.StateBag.TryGetValue<string>(LoomTools.SessionIdKey, out var sid) ? sid : null;
        if (sessionId is null)
            return new LoomCodeItUpResult(false, null, null, 0, "No session ID in StateBag");

        return new LoomCodeItUpResult(true, null, null, 0, $"code_it_up requested for {repo}:{baseBranch ?? "main"} on session {sessionId}");
    }
}
