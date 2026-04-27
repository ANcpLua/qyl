// Copyright (c) 2025-2026 ancplua

using System.Threading.Channels;

namespace Qyl.Loom.Autofix.Workflow;

internal interface IAutofixLifecycleBus
{
    void Publish(string runId, AutofixLifecycleEnvelope envelope);
    IAsyncEnumerable<AutofixLifecycleEnvelope> SubscribeAsync(string runId, CancellationToken ct);
    void Complete(string runId);
}

internal readonly record struct AutofixLifecycleEnvelope(
    string RunId,
    string Stage,
    string Kind,
    string PayloadJson,
    DateTimeOffset Timestamp);

internal sealed class InMemoryAutofixLifecycleBus : IAutofixLifecycleBus
{
    private readonly ConcurrentDictionary<string, Channel<AutofixLifecycleEnvelope>> _channels =
        new(StringComparer.Ordinal);

    public void Publish(string runId, AutofixLifecycleEnvelope envelope)
    {
        var channel = _channels.GetOrAdd(runId, _ => Channel.CreateUnbounded<AutofixLifecycleEnvelope>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            }));
        channel.Writer.TryWrite(envelope);
    }

    public async IAsyncEnumerable<AutofixLifecycleEnvelope> SubscribeAsync(
        string runId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = _channels.GetOrAdd(runId, _ => Channel.CreateUnbounded<AutofixLifecycleEnvelope>());
        await foreach (var envelope in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return envelope;
        }
    }

    public void Complete(string runId)
    {
        if (_channels.TryRemove(runId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
