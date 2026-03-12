using Microsoft.Agents.AI;
using Qyl.Contracts.Copilot;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Agents.Context;

/// <summary>
///     Injects issue-specific observability context into agent runs when an issue id
///     is present in the session state bag.
/// </summary>
public sealed class ObservabilityContextProvider(IIssueContextSource contextSource)
    : MessageAIContextProvider
{
    public const string IssueIdKey = "qyl.issueId";

    protected override async ValueTask<IEnumerable<AiChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        string? issueId = context.Session?.StateBag?.GetValue<string>(IssueIdKey);
        if (string.IsNullOrWhiteSpace(issueId))
            return [];

        string formatted = await contextSource
            .GetFormattedContextAsync(issueId, ct: cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(formatted))
            return [];

        return [new AiChatMessage(AiChatRole.System, $"## Error Context\n{formatted}")];
    }
}
