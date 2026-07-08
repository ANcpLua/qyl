using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

public sealed class QylAgentInventory : IQylAgentInventory
{
    internal const string InventorySizeMetricName = "qyl.observability.inventory.size";

    private readonly ConcurrentDictionary<string, AgentRegistration> _entries =
        new(StringComparer.Ordinal);

    // Keyed by agent Name (multiple registrations sharing a Name share one window,
    // matching the previous queue-per-name behavior). Windows exist only for
    // registered names, so RecordActivity stays a single O(1) lookup on the span
    // export path — this replaces a per-span linear scan over all registrations.
    private readonly ConcurrentDictionary<string, AgentActivityWindow> _activityByName =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _time;

    public QylAgentInventory(TimeProvider time)
    {
        _time = time;

        _ = ActivitySources.AgentMeter.CreateObservableGauge(
            InventorySizeMetricName,
            () => (long)_entries.Count,
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

        if (!string.IsNullOrEmpty(registration.Name))
            _ = _activityByName.GetOrAdd(registration.Name, static _ => new AgentActivityWindow());
    }

    public void RecordActivity(string agentName, DateTime timestamp)
    {
        if (string.IsNullOrEmpty(agentName)) return;

        if (_activityByName.TryGetValue(agentName, out var window))
            window.Record(timestamp);
    }

    public IReadOnlyList<AgentRegistration> Snapshot()
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var snapshot = new List<AgentRegistration>(_entries.Count);

        foreach (var entry in _entries.Values)
        {
            DateTime? lastSeen = null;
            long count24h = 0;

            if (_activityByName.TryGetValue(entry.Name, out var window))
                (count24h, lastSeen) = window.Read(nowUtc);

            snapshot.Add(entry with { LastSeenUtc = lastSeen, CallCount24h = count24h });
        }

        return snapshot;
    }

    public static string? HashInstructions(string? instructions) =>
        string.IsNullOrEmpty(instructions)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions)));

    /// <summary>
    ///     24h sliding call window as interval-accounted counters (the
    ///     dotnet/runtime EventCounter/RateLimiter pattern): one count per
    ///     15-minute slot in a fixed ring instead of one timestamp per call.
    ///     Bounds memory to the ring regardless of call volume (the previous
    ///     queue kept up to 10k DateTimes per agent and undercounted past the
    ///     cap); the window edge has 15-minute granularity.
    /// </summary>
    private sealed class AgentActivityWindow
    {
        private const long TicksPerSlot = TimeSpan.TicksPerMinute * 15;
        private const int SlotsPerWindow = 96;
        private const int SlotCount = SlotsPerWindow + 1;

        private readonly Lock _lock = new();
        private readonly long[] _slotNumbers = new long[SlotCount];
        private readonly long[] _counts = new long[SlotCount];
        private long _lastSeenTicks;

        public void Record(DateTime timestampUtc)
        {
            var slot = timestampUtc.Ticks / TicksPerSlot;
            var index = (int)(slot % SlotCount);

            lock (_lock)
            {
                if (_slotNumbers[index] != slot)
                {
                    _slotNumbers[index] = slot;
                    _counts[index] = 0;
                }

                _counts[index]++;

                if (timestampUtc.Ticks > _lastSeenTicks)
                    _lastSeenTicks = timestampUtc.Ticks;
            }
        }

        public (long Count24h, DateTime? LastSeenUtc) Read(DateTime nowUtc)
        {
            var currentSlot = nowUtc.Ticks / TicksPerSlot;
            var oldestIncludedSlot = currentSlot - SlotsPerWindow;

            lock (_lock)
            {
                long count = 0;
                for (var i = 0; i < SlotCount; i++)
                {
                    if (_slotNumbers[i] >= oldestIncludedSlot && _counts[i] > 0)
                        count += _counts[i];
                }

                return count > 0
                    ? (count, new DateTime(_lastSeenTicks, DateTimeKind.Utc))
                    : (0, null);
            }
        }
    }
}
