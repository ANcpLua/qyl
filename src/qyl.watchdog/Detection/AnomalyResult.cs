namespace Qyl.Watchdog.Detection;

public readonly record struct AnomalyResult(
    bool IsAnomaly,
    int Pid,
    string ProcessName,
    double CpuPercent,
    double BaselineCpu,
    long MemoryBytes)
{
    public static AnomalyResult Normal => default;

    public static AnomalyResult Anomaly(
        int pid, string name, double cpuPercent, double baselineCpu, long memoryBytes) =>
        new(true, pid, name, cpuPercent, baselineCpu, memoryBytes);
}
