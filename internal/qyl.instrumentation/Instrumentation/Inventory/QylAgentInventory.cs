using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

public sealed class QylAgentInventory : IQylAgentInventory
{
    private const int ActivityWindowCap = 10_000;

    private readonly ConcurrentDictionary<string, AgentRegistration> _entries =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _activity =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _time;

    public QylAgentInventory(TimeProvider time)
    {
        _time = time;

        // Discarded: the Meter holds a strong reference internally, so we don't need a field.
        _ = ActivitySources.AgentMeter.CreateObservableGauge(
            "qyl_observability_inventory_size",
            () => _entries.Count,
            unit: "{agent}",
            description: "Count of agents registered in the qyl inventory");
    }

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

        if (!IsRegisteredName(agentName)) return;

        var queue = _activity.GetOrAdd(agentName, static _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(timestamp);

        while (queue.Count > ActivityWindowCap && queue.TryDequeue(out _))
        {
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

    public static string? HashInstructions(string? instructions) =>
        string.IsNullOrEmpty(instructions)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions)));
}
