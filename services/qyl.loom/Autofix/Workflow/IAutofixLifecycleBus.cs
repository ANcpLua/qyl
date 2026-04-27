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

/// Broadcast bus — every subscriber for a given runId receives every event for
/// that run. Each subscriber gets its own bounded-throughput channel; writes
/// fan out across active subscribers. Subscribers that disconnect (cancel
/// their token) are removed and their channels closed; <see cref="Complete" />
/// closes any remaining subscribers and clears the per-run subscriber list.
internal sealed class InMemoryAutofixLifecycleBus : IAutofixLifecycleBus
{
    private readonly ConcurrentDictionary<string, RunSubscribers> _runs = new(StringComparer.Ordinal);

    public void Publish(string runId, AutofixLifecycleEnvelope envelope)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        run.Broadcast(envelope);
    }

    public async IAsyncEnumerable<AutofixLifecycleEnvelope> SubscribeAsync(
        string runId, [EnumeratorCancellation] CancellationToken ct)
    {
        var run = _runs.GetOrAdd(runId, _ => new RunSubscribers());
        var channel = run.Subscribe();

        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return envelope;
            }
        }
        finally
        {
            run.Unsubscribe(channel);
            // Don't TryRemove the run here — Publish may still arrive after the last
            // subscriber leaves and we want to deliver-and-drop those events.
            // Complete() removes the run entry once the workflow finishes.
        }
    }

    public void Complete(string runId)
    {
        if (_runs.TryRemove(runId, out var run))
        {
            run.CompleteAll();
        }
    }

    private sealed class RunSubscribers
    {
        private readonly Lock _gate = new();
        private readonly List<Channel<AutofixLifecycleEnvelope>> _subscribers = [];

        public Channel<AutofixLifecycleEnvelope> Subscribe()
        {
            var channel = Channel.CreateUnbounded<AutofixLifecycleEnvelope>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });

            lock (_gate) _subscribers.Add(channel);
            return channel;
        }

        public void Unsubscribe(Channel<AutofixLifecycleEnvelope> channel)
        {
            lock (_gate) _subscribers.Remove(channel);
            channel.Writer.TryComplete();
        }

        public void Broadcast(AutofixLifecycleEnvelope envelope)
        {
            Channel<AutofixLifecycleEnvelope>[] snapshot;
            lock (_gate) snapshot = [.. _subscribers];
            foreach (var channel in snapshot)
            {
                channel.Writer.TryWrite(envelope);
            }
        }

        public void CompleteAll()
        {
            Channel<AutofixLifecycleEnvelope>[] snapshot;
            lock (_gate)
            {
                snapshot = [.. _subscribers];
                _subscribers.Clear();
            }
            foreach (var channel in snapshot)
            {
                channel.Writer.TryComplete();
            }
        }
    }
}
