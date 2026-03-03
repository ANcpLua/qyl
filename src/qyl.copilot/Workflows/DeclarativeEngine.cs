// =============================================================================
// qyl.copilot - DeclarativeEngine
// Thin adapter over DeclarativeWorkflowBuilder: loads .yaml AdaptiveDialog
// workflows, wires an IChatClient via ChatClientResponseAgentProvider, and
// streams results as IAsyncEnumerable<StreamUpdate>.
//
// Produces the same StreamUpdate contract as WorkflowEngine (markdown-based),
// making both engines interchangeable from the collector layer's perspective.
//
// Usage:
//   var engine = new DeclarativeEngine(chatClient, agentName: "daily-qa");
//   await engine.LoadAsync(".qyl/workflows/daily-qa.yaml");
//   await foreach (var update in engine.ExecuteAsync(input, ct)) { ... }
// =============================================================================

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;
using qyl.protocol.Copilot;

namespace qyl.copilot.Workflows;

/// <summary>
///     Executes YAML <c>AdaptiveDialog</c> workflows using
///     <see cref="DeclarativeWorkflowBuilder"/> + an in-process executor,
///     streaming results as <see cref="StreamUpdate"/> events.
/// </summary>
public sealed class DeclarativeEngine
{
    private readonly string _agentName;
    private readonly IChatClient _chatClient;
    private Workflow? _workflow;

    /// <summary>Creates a new engine.</summary>
    /// <param name="chatClient">The chat client used by the agent provider.</param>
    /// <param name="agentName">Identifies the agent in OTel spans.</param>
    public DeclarativeEngine(IChatClient chatClient, string agentName = "declarative-agent")
    {
        _chatClient = Guard.NotNull(chatClient);
        _agentName = Guard.NotNullOrWhiteSpace(agentName);
    }

    /// <summary>
    ///     Loads and compiles a YAML workflow file.
    ///     Must be called before <see cref="ExecuteAsync"/>.
    /// </summary>
    /// <param name="yamlFile">Absolute or relative path to the <c>.yaml</c> workflow.</param>
    public ValueTask LoadAsync(string yamlFile)
    {
        Guard.NotNullOrWhiteSpace(yamlFile);
        var agentProvider = new ChatClientResponseAgentProvider(_chatClient);
        var options = new DeclarativeWorkflowOptions(agentProvider);
        _workflow = DeclarativeWorkflowBuilder.Build<string>(yamlFile, options);
        return default;
    }

    /// <summary>
    ///     Executes the loaded workflow, streaming <see cref="StreamUpdate"/> events.
    /// </summary>
    /// <param name="input">Free-text input passed as the first user message.</param>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<StreamUpdate> ExecuteAsync(
        string input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_workflow is null)
            throw new InvalidOperationException("Call LoadAsync() before ExecuteAsync().");

        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        StreamingRun run = await InProcessExecution.Default
            .RunStreamingAsync(_workflow, input, cancellationToken: ct)
            .ConfigureAwait(false);

        List<StreamUpdate> updates = [];
        Exception? caughtException = null;

        IAsyncEnumerator<WorkflowEvent> enumerator = run.WatchStreamAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                WorkflowEvent evt = enumerator.Current;
                switch (evt.Data)
                {
                    case AgentResponseUpdateEvent updateEvent:
                        string text = updateEvent.Update.Text ?? string.Empty;
                        if (text.Length > 0)
                        {
                            updates.Add(new StreamUpdate
                            {
                                Kind = StreamUpdateKind.Content,
                                Content = text,
                                Timestamp = TimeProvider.System.GetUtcNow()
                            });
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            caughtException = ex;
        }
        catch (HttpRequestException ex)
        {
            caughtException = ex;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        foreach (StreamUpdate update in updates)
            yield return update;

        if (caughtException is not null)
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = caughtException.Message,
                Timestamp = TimeProvider.System.GetUtcNow()
            };
        }
        else
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Completed,
                Timestamp = TimeProvider.System.GetUtcNow()
            };
        }
    }

    // ── Private: IChatClient → ResponseAgentProvider adapter ─────────────────

    /// <summary>
    ///     Bridges any <see cref="IChatClient"/> into the
    ///     <see cref="ResponseAgentProvider"/> contract required by
    ///     <see cref="DeclarativeWorkflowOptions"/>. Maintains in-memory
    ///     conversation history keyed by conversation ID.
    /// </summary>
    private sealed class ChatClientResponseAgentProvider(IChatClient chatClient)
        : ResponseAgentProvider
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();
        private readonly ConcurrentDictionary<string, ChatMessage> _messages = new();

        public override Task<string> CreateConversationAsync(
            CancellationToken cancellationToken = default)
        {
            string id = Guid.NewGuid().ToString("N");
            _conversations[id] = [];
            return Task.FromResult(id);
        }

        public override Task<ChatMessage> CreateMessageAsync(
            string conversationId,
            ChatMessage conversationMessage,
            CancellationToken cancellationToken = default)
        {
            ChatMessage stored = new(conversationMessage.Role, conversationMessage.Contents)
            {
                MessageId = Guid.NewGuid().ToString("N")
            };
            _messages[stored.MessageId] = stored;
            if (_conversations.TryGetValue(conversationId, out List<ChatMessage>? history))
                history.Add(stored);
            return Task.FromResult(stored);
        }

        public override Task<ChatMessage> GetMessageAsync(
            string conversationId,
            string messageId,
            CancellationToken cancellationToken = default) =>
            _messages.TryGetValue(messageId, out ChatMessage? msg)
                ? Task.FromResult(msg)
                : Task.FromException<ChatMessage>(
                    new KeyNotFoundException($"Message {messageId} not found."));

        public override async IAsyncEnumerable<AgentResponseUpdate> InvokeAgentAsync(
            string agentId,
            string? agentVersion,
            string? conversationId,
            IEnumerable<ChatMessage>? messages,
            IDictionary<string, object?>? inputArguments,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            List<ChatMessage> history = [];
            if (conversationId is not null &&
                _conversations.TryGetValue(conversationId, out List<ChatMessage>? stored))
            {
                history.AddRange(stored);
            }
            if (messages is not null)
                history.AddRange(messages);

            await foreach (StreamingChatCompletionUpdate update in chatClient
                .GetStreamingResponseAsync(history, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                string text = update.Text ?? string.Empty;
                if (text.Length > 0)
                    yield return new AgentResponseUpdate(ChatRole.Assistant, text);
            }
        }

        public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
            string conversationId,
            int? limit = null,
            string? after = null,
            string? before = null,
            bool newestFirst = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_conversations.TryGetValue(conversationId, out List<ChatMessage>? history))
                yield break;

            IEnumerable<ChatMessage> items = newestFirst
                ? ((IEnumerable<ChatMessage>)history).Reverse()
                : history;

            if (limit.HasValue)
                items = items.Take(limit.Value);

            foreach (ChatMessage message in items)
                yield return message;

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
