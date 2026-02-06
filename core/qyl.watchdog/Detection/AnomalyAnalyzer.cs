using Qyl.Watchdog.Platform;

namespace Qyl.Watchdog.Detection;

public sealed class AnomalyAnalyzer(double spikeMultiplier, int sustainedCount)
{
    private readonly ConcurrentDictionary<int, ProcessBaseline> _baselines = new();

    public AnomalyResult Analyze(ProcessSnapshot snapshot) =>
        _baselines.GetOrAdd(snapshot.Pid,
            _ => new ProcessBaseline(snapshot.Name, spikeMultiplier, sustainedCount))
        .Update(snapshot);

    public void PruneExited(IReadOnlySet<int> activePids)
    {
        foreach (var pid in _baselines.Keys)
        {
            if (!activePids.Contains(pid))
                _baselines.TryRemove(pid, out _);
        }
    }
}
