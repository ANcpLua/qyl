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
public sealed class QylAgentInventory : IQylAgentInventory
{
    private readonly ConcurrentDictionary<string, AgentRegistration> _entries =
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

    public IReadOnlyList<AgentRegistration> Snapshot()
    {
        var snapshot = new List<AgentRegistration>(_entries.Count);
        foreach (var entry in _entries.Values)
            snapshot.Add(entry);
        return snapshot;
    }

    /// <summary>Compute the SHA256 hex digest of an instruction string.</summary>
    public static string? HashInstructions(string? instructions) =>
        string.IsNullOrEmpty(instructions)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions)));
}
