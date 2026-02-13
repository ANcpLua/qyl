using Qyl.Watchdog.Platform;

namespace Qyl.Watchdog.Detection;

/// <summary>
/// Tracks per-process CPU baseline via exponential moving average.
/// Only triggers anomaly after sustained spike (consecutive samples above threshold).
/// Baseline is NOT updated during spikes to prevent inflation.
/// </summary>
public sealed class ProcessBaseline(string name, double spikeMultiplier, int sustainedCount)
{
    private double _cpuEma;
    private int _consecutiveSpikes;
    private const double Alpha = 0.1;

    public AnomalyResult Update(ProcessSnapshot snapshot)
    {
        var threshold = Math.Max(_cpuEma * spikeMultiplier, 50.0);
        var isSpike = snapshot.CpuPercent > threshold;

        if (isSpike)
        {
            _consecutiveSpikes++;
            if (_consecutiveSpikes >= sustainedCount)
            {
                return AnomalyResult.Anomaly(
                    snapshot.Pid, name, snapshot.CpuPercent, _cpuEma, snapshot.MemoryBytes);
            }
        }
        else
        {
            _consecutiveSpikes = 0;
            _cpuEma = Alpha * snapshot.CpuPercent + (1 - Alpha) * _cpuEma;
        }

        return AnomalyResult.Normal;
    }
}
