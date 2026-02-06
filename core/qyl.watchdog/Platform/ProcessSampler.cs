namespace Qyl.Watchdog.Platform;

/// <summary>
/// Polls system process stats by computing CPU% from consecutive TotalProcessorTime deltas.
/// First sample for each process returns 0% CPU — baselines build from the second sample onward.
/// </summary>
public sealed class ProcessSampler(TimeProvider timeProvider, HashSet<string> ignoreProcesses)
{
    private readonly Dictionary<int, (TimeSpan CpuTime, long Tick)> _previous = [];

    public IReadOnlyList<ProcessSnapshot> Sample()
    {
        var results = new List<ProcessSnapshot>();
        var now = timeProvider.GetTimestamp();
        var nowMs = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var cpuCount = Environment.ProcessorCount;

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName;
                if (ignoreProcesses.Contains(name))
                    continue;

                var pid = proc.Id;
                var cpuTime = proc.TotalProcessorTime;
                var memBytes = proc.WorkingSet64;

                double cpuPercent = 0;
                if (_previous.TryGetValue(pid, out var prev))
                {
                    var elapsed = timeProvider.GetElapsedTime(prev.Tick, now);
                    if (elapsed.TotalMilliseconds > 100)
                    {
                        cpuPercent = (cpuTime - prev.CpuTime).TotalMilliseconds
                            / elapsed.TotalMilliseconds * 100.0
                            / cpuCount;
                    }
                }

                _previous[pid] = (cpuTime, now);
                results.Add(new ProcessSnapshot(pid, name, cpuPercent, memBytes, nowMs));
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally
            {
                proc.Dispose();
            }
        }

        return results;
    }

    public void PruneExited(IReadOnlySet<int> activePids)
    {
        var stale = _previous.Keys.Where(pid => !activePids.Contains(pid)).ToList();
        foreach (var pid in stale)
            _previous.Remove(pid);
    }
}
