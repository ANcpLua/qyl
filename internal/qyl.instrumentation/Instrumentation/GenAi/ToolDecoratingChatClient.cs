using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

internal sealed class ToolDecoratingChatClient(
    IChatClient inner,
    Func<AIFunction, AIFunction> decorator) : DelegatingChatClient(inner)
{
    private readonly Func<AIFunction, AIFunction> _decorator = decorator;

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(messages, PrepareOptions(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(messages, PrepareOptions(options), cancellationToken);

    private ChatOptions? PrepareOptions(ChatOptions? options)
    {
        if (options?.Tools is not { Count: > 0 } tools)
        {
            return options;
        }

        for (var i = 0; i < tools.Count; i++)
        {
            if (tools[i] is AIFunction function)
            {
                tools[i] = _decorator(function);
            }
        }

        return options;
    }
}
