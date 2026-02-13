namespace Qyl.Watchdog.Platform;

public readonly record struct ProcessSnapshot(
    int Pid,
    string Name,
    double CpuPercent,
    long MemoryBytes);
