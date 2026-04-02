using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Ensures tool calls are instrumented whenever qyl is already wrapping a chat client.
/// </summary>
internal sealed class ToolInstrumentingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
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

    internal static ChatOptions? PrepareOptions(ChatOptions? options)
    {
        options?.AddInstrumentedTools();
        return options;
    }
}
