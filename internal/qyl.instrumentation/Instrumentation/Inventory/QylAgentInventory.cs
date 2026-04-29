using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

/// <summary>
///     Concurrent in-memory <see cref="IQylAgentInventory" />. Keyed by <see cref="AgentRegistration.Key" />;
///     last-writer-wins on concurrent registrations, which matches the "refresh metadata"
///     intent. Publishes <c>qyl_observability_inventory_size</c> as an observable gauge on
///     <see cref="ActivitySources.AgentMeter" /> so the dashboard can plot inventory growth
///     without polling the endpoint.
/// </summary>
/// <remarks>
///     Activity counters are bounded: each agent keeps a <see cref="ConcurrentQueue{DateTime}" />
///     of timestamps capped at <see cref="ActivityWindowCap" /> entries, with prune-on-read
///     dropping anything older than 24h. The cap protects a runaway emission storm from
///     unbounded heap growth; pruning happens in <see cref="Snapshot" /> so reads see a
///     correct 24h window without a background timer.
/// </remarks>
public sealed class QylAgentInventory : IQylAgentInventory
{
    /// <summary>
    ///     Per-agent activity-window cap. 24h × 60s = 86 400; 10 000 covers a sustained
    ///     8 invocations per minute without backpressure. Excess is dropped from the front.
    /// </summary>
    private const int ActivityWindowCap = 10_000;

    private readonly ConcurrentDictionary<string, AgentRegistration> _entries =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _activity =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _time;

    public QylAgentInventory(TimeProvider time)
    {
        _time = time;

        // Holding the gauge instance keeps the callback alive for the meter's lifetime.
        InventorySizeGauge = ActivitySources.AgentMeter.CreateObservableGauge(
            "qyl_observability_inventory_size",
            () => _entries.Count,
            unit: "{agent}",
            description: "Count of agents registered in the qyl inventory");
    }

    private ObservableGauge<int> InventorySizeGauge { get; }

    public void Register(AgentRegistration registration)
    {
        Guard.NotNullOrEmpty(registration.Key);

        var stamped = registration.RegisteredAtUtc == default
            ? registration with { RegisteredAtUtc = _time.GetUtcNow().UtcDateTime }
            : registration;

        _entries[registration.Key] = stamped;
    }

    public void RecordActivity(string agentName, DateTime timestamp)
    {
        if (string.IsNullOrEmpty(agentName)) return;

        // Fast-path: skip names that aren't registered. Avoids unbounded growth on
        // arbitrary gen_ai.agent.name values from non-qyl producers.
        if (!IsRegisteredName(agentName)) return;

        var queue = _activity.GetOrAdd(agentName, static _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(timestamp);

        while (queue.Count > ActivityWindowCap && queue.TryDequeue(out _))
        {
            // Bounded buffer — drop oldest until we're back under the cap.
        }
    }

    public IReadOnlyList<AgentRegistration> Snapshot()
    {
        var cutoff = _time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(24);
        var snapshot = new List<AgentRegistration>(_entries.Count);

        foreach (var entry in _entries.Values)
        {
            DateTime? lastSeen = null;
            long count24h = 0;

            if (_activity.TryGetValue(entry.Name, out var queue))
            {
                // Prune-on-read: drop anything older than 24h from the head.
                while (queue.TryPeek(out var head) && head < cutoff)
                    queue.TryDequeue(out _);

                count24h = queue.Count;
                if (count24h > 0)
                {
                    foreach (var ts in queue)
                    {
                        if (lastSeen is null || ts > lastSeen)
                            lastSeen = ts;
                    }
                }
            }

            snapshot.Add(entry with { LastSeenUtc = lastSeen, CallCount24h = count24h });
        }

        return snapshot;
    }

    private bool IsRegisteredName(string agentName)
    {
        foreach (var entry in _entries.Values)
        {
            if (string.Equals(entry.Name, agentName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Compute the SHA256 hex digest of an instruction string.</summary>
    public static string? HashInstructions(string? instructions) =>
        string.IsNullOrEmpty(instructions)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions)));
}
