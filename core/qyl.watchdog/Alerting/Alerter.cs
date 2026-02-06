using Qyl.Watchdog.Detection;

namespace Qyl.Watchdog.Alerting;

public sealed class Alerter(INotificationSender sender, TimeProvider timeProvider, long cooldownMs)
{
    private readonly ConcurrentDictionary<int, long> _cooldowns = new();

    public async ValueTask AlertAsync(AnomalyResult anomaly, CancellationToken ct)
    {
        if (!ShouldAlert(anomaly.Pid))
            return;

        var title = $"High CPU: {anomaly.ProcessName}";
        var body = $"PID {anomaly.Pid} using {anomaly.CpuPercent:F0}% CPU (baseline: {anomaly.BaselineCpu:F0}%)";

        await sender.SendAsync(title, body, ct);
        _cooldowns[anomaly.Pid] = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }

    private bool ShouldAlert(int pid)
    {
        if (!_cooldowns.TryGetValue(pid, out var lastAlert))
            return true;
        return timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - lastAlert > cooldownMs;
    }
}
