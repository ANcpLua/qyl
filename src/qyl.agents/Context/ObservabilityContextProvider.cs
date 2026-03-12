using Microsoft.Agents.AI;
using Qyl.Contracts.Copilot;

using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Agents.Context;

/// <summary>
///     Auto-injects formatted issue context as a system message at the start of
///     every agent invocation that carries a <c>qyl.issueId</c> in the session
///     state bag.
/// </summary>
public class ObservabilityContextProvider(IIssueContextSource contextSource)
    : MessageAIContextProvider
{
    /// <summary>
    ///     Key used to store the active issue ID in <see cref="AgentSession.StateBag"/>.
    /// </summary>
    public const string IssueIdKey = "qyl.issueId";

    /// <inheritdoc/>
    protected override async ValueTask<IEnumerable<AiChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        context.Session.StateBag.TryGetValue<string>(IssueIdKey, out string? issueId, null!);
        if (issueId is null) return [];

        string formatted = await contextSource
            .GetFormattedContextAsync(issueId, ct: cancellationToken);

        if (string.IsNullOrEmpty(formatted)) return [];

        return [new AiChatMessage(AiChatRole.System, $"## Error Context\n{formatted}")];
    }
}
