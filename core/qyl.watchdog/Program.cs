using Qyl.Watchdog;
using Qyl.Watchdog.Alerting;
using Qyl.Watchdog.Detection;
using Qyl.Watchdog.Platform;

var options = WatchdogOptions.Parse(args);

// Command dispatch — early exits before starting the daemon loop
if (options.Install) { await LaunchdSetup.InstallAsync(); return; }
if (options.Uninstall) { await LaunchdSetup.UninstallAsync(); return; }
if (options.Status) { await LaunchdSetup.StatusAsync(); return; }

if (options.KillPid is { } killPid)
{
    try
    {
        Process.GetProcessById(killPid).Kill();
        Console.WriteLine($"Killed process {killPid}");
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Failed to kill PID {killPid}: {ex.Message}");
    }

    return;
}

var timeProvider = TimeProvider.System;
var sampler = new ProcessSampler(timeProvider, options.IgnoreProcesses);
var analyzer = new AnomalyAnalyzer(options.SpikeThreshold, options.SustainedCount);

INotificationSender sender = OperatingSystem.IsMacOS()
    ? new MacOsNotificationSender()
    : new ConsoleNotificationSender();

var alerter = new Alerter(sender, timeProvider, options.CooldownMs);

Console.WriteLine($"qyl-watch started (interval: {options.IntervalMs}ms, threshold: {options.SpikeThreshold}x, sustained: {options.SustainedCount} samples)");
if (options.IgnoreProcesses.Count > 0)
    Console.WriteLine($"  ignoring: {string.Join(", ", options.IgnoreProcesses)}");

if (options.Once)
{
    await ScanAsync(CancellationToken.None);
    return;
}

var cts = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
Console.CancelKeyPress += cancelHandler;

using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.IntervalMs));

try
{
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        await ScanAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nqyl-watch stopped");
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
    cts.Dispose();
}

async Task ScanAsync(CancellationToken ct)
{
    var snapshots = sampler.Sample();
    var activePids = new HashSet<int>(snapshots.Count);

    foreach (var snapshot in snapshots)
    {
        activePids.Add(snapshot.Pid);

        if (options.Verbose && snapshot.CpuPercent > 10)
        {
            Console.WriteLine(
                $"  {snapshot.Name,-30} PID:{snapshot.Pid,-7} CPU:{snapshot.CpuPercent,6:F1}%  MEM:{snapshot.MemoryBytes / 1024 / 1024,5}MB");
        }

        var result = analyzer.Analyze(snapshot);

        if (result.IsAnomaly)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(
                $"  ANOMALY: {result.ProcessName} (PID {result.Pid}) — {result.CpuPercent:F0}% CPU (baseline: {result.BaselineCpu:F0}%)");
            Console.ResetColor();

            await alerter.AlertAsync(result, ct);
        }
    }

    sampler.PruneExited(activePids);
    analyzer.PruneExited(activePids);
}
