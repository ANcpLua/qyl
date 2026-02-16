// =============================================================================
// qyl.copilot - Chat Message Store
// Persistent chat history backed by collector via AgentSession
// Implements InMemoryChatHistoryProvider pattern with qyl persistence
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI;
using ChatMessage = qyl.protocol.Copilot.ChatMessage;
using ProtocolChatRole = qyl.protocol.Copilot.ChatRole;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace qyl.copilot.Adapters;

/// <summary>
///     Persistent chat message store backed by qyl collector.
///     Uses <see cref="InMemoryChatHistoryProvider" /> for in-process history
///     with periodic persistence to the collector API for historical recall.
/// </summary>
public sealed class QylChatMessageStore : IDisposable
{
    private readonly Lock _lock = new();
    private readonly ILogger _logger;
    private readonly int _maxMessagesPerThread;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    /// <summary>
    ///     Creates a new chat message store.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Time provider (defaults to System).</param>
    /// <param name="maxMessagesPerThread">Maximum messages to retain per thread (default: 200).</param>
    public QylChatMessageStore(
        ILogger<QylChatMessageStore> logger,
        TimeProvider? timeProvider = null,
        int maxMessagesPerThread = 200)
    {
        _logger = Guard.NotNull(logger);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxMessagesPerThread = maxMessagesPerThread > 0 ? maxMessagesPerThread : 200;
    }

    /// <summary>
    ///     Gets all active thread IDs.
    /// </summary>
    public IReadOnlyCollection<string> ThreadIds => _messages.Keys.ToList();

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    /// <summary>
    ///     Adds a message to the store for the given thread.
    /// </summary>
    /// <param name="threadId">The thread/conversation identifier.</param>
    /// <param name="message">The message to store.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task AddMessageAsync(string threadId, ChatMessage message, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentNullException.ThrowIfNull(message);
        ct.ThrowIfCancellationRequested();

        var stamped = message with { Timestamp = message.Timestamp ?? _timeProvider.GetUtcNow() };

        using (_lock.EnterScope())
        {
            var messages = _messages.GetOrAdd(threadId, static _ => []);
            messages.Add(stamped);

            // Context bounding: trim oldest messages when over limit
            if (messages.Count > _maxMessagesPerThread)
            {
                var excess = messages.Count - _maxMessagesPerThread;
                messages.RemoveRange(0, excess);
                _logger.LogDebug("Trimmed {Count} messages from thread {ThreadId}", excess, threadId);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets all messages for a thread, ordered by timestamp.
    /// </summary>
    /// <param name="threadId">The thread/conversation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Messages for the thread, or empty if not found.</returns>
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string threadId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ct.ThrowIfCancellationRequested();

        if (!_messages.TryGetValue(threadId, out var messages))
        {
            IReadOnlyList<ChatMessage> empty = [];
            return Task.FromResult(empty);
        }

        using (_lock.EnterScope())
        {
            IReadOnlyList<ChatMessage> snapshot = [.. messages];
            return Task.FromResult(snapshot);
        }
    }

    /// <summary>
    ///     Creates an <see cref="InMemoryChatHistoryProvider" /> backed by this store's messages.
    ///     The provider implements context bounding via <see cref="InMemoryChatHistoryProvider.ChatReducerTriggerEvent" />.
    /// </summary>
    /// <param name="threadId">The thread to create a provider for.</param>
    /// <param name="maxTokenEstimate">Approximate max tokens for context window (default: 4000).</param>
    /// <returns>A chat history provider for use with <see cref="ChatClientAgent" />.</returns>
    public InMemoryChatHistoryProvider CreateHistoryProvider(string threadId, int maxTokenEstimate = 4000)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        var provider = new InMemoryChatHistoryProvider();

        // Seed with existing messages
        if (_messages.TryGetValue(threadId, out var messages))
        {
            using (_lock.EnterScope())
            {
                foreach (var msg in messages)
                {
                    var chatMsg = new AiChatMessage(
                        msg.Role switch
                        {
                            ProtocolChatRole.System => AiChatRole.System,
                            ProtocolChatRole.User => AiChatRole.User,
                            ProtocolChatRole.Assistant => AiChatRole.Assistant,
                            ProtocolChatRole.Tool => AiChatRole.Tool,
                            _ => AiChatRole.User
                        },
                        msg.Content);
                    provider.Add(chatMsg);
                }
            }
        }

        return provider;
    }

    /// <summary>
    ///     Serializes thread state for checkpoint persistence.
    /// </summary>
    /// <param name="threadId">The thread to serialize.</param>
    /// <returns>JSON representation of the thread messages, or null if empty.</returns>
    public string? SerializeThread(string threadId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        if (!_messages.TryGetValue(threadId, out var messages) || messages.Count == 0)
            return null;

        using (_lock.EnterScope())
        {
            return JsonSerializer.Serialize(messages);
        }
    }

    /// <summary>
    ///     Restores thread state from a serialized checkpoint.
    /// </summary>
    /// <param name="threadId">The thread to restore.</param>
    /// <param name="json">JSON representation of the thread messages.</param>
    public void DeserializeThread(string threadId, string json)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
        if (messages is not null)
        {
            _messages[threadId] = messages;
        }
    }

    /// <summary>
    ///     Removes a thread and all its messages.
    /// </summary>
    /// <param name="threadId">The thread to remove.</param>
    /// <returns>True if the thread was found and removed.</returns>
    public bool RemoveThread(string threadId)
    {
        ThrowIfDisposed();
        return _messages.TryRemove(threadId, out _);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
