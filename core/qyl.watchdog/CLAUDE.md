# qyl.watchdog

Lightweight system resource watchdog. Catches zombie processes before they catch you.

## identity

```yaml
name: qyl.watchdog
type: console-daemon
sdk: ANcpLua.NET.Sdk
role: local-dev-tool
distribution: dotnet-global-tool
command: qyl-watch
```

## purpose

Monitor system processes, detect anomalous CPU consumption, alert via native notifications. Designed for developer workstations where IDE backends and browser tabs go rogue.

## constraints

```yaml
performance:
  ram: "< 10 MB"
  cpu: "< 0.5%"
  startup: "< 500ms"
  binary: "< 5 MB"

design:
  - standalone (no qyl dependencies)
  - no ASP.NET Core (too heavy)
  - native notifications only (no dashboard)
  - optional OTLP export to collector
```

## architecture

```yaml
components:
  sampler:
    purpose: poll process stats
    interval: 5 seconds
    output: ProcessSnapshot[]

  analyzer:
    purpose: detect anomalies
    algorithm: EMA baseline + spike detection
    state: per-process baselines

  alerter:
    purpose: notify user
    method: macOS Notification Center
    cooldown: 5 minutes per process
```

## algorithm

```yaml
baseline:
  type: exponential-moving-average
  alpha: 0.1
  updates: only when not spiking

spike-detection:
  condition: cpu > max(baseline * 3.0, 50%)
  sustained: 6 consecutive samples (30 seconds)
  reason: filters transient spikes (builds, etc.)

alert-trigger:
  when: sustained spike detected
  cooldown: 300 seconds per PID
  action: native notification + optional OTLP
```

## files

```yaml
Platform/:
  - IProcessSampler.cs        # abstraction
  - MacOsProcessSampler.cs    # libproc / Process.GetProcesses
  - LinuxProcessSampler.cs    # /proc parsing
  - WindowsProcessSampler.cs  # WMI / Process class

Detection/:
  - ProcessBaseline.cs        # EMA + spike tracking
  - AnomalyAnalyzer.cs        # orchestrates baselines
  - AnomalyResult.cs          # result type

Alerting/:
  - INotificationSender.cs    # abstraction
  - MacOsNotificationSender.cs # osascript
  - OtlpExporter.cs           # optional http export

Root/:
  - Program.cs                # entry point + main loop
  - WatchdogOptions.cs        # config record
  - GlobalUsings.cs           # shared usings
```

## patterns

```yaml
main-loop:
  code: |
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.IntervalMs));
    while (await timer.WaitForNextTickAsync(ct))
    {
        var snapshots = sampler.Sample();
        foreach (var snapshot in snapshots)
        {
            var result = analyzer.Analyze(snapshot);
            if (result.IsAnomaly)
            {
                await alerter.AlertAsync(result, ct);
            }
        }
    }

notification-macos:
  code: |
    var script = $"""
        display notification "{body}" with title "{title}" sound name "Basso"
        """;
    Process.Start("osascript", $"-e '{script}'");

baseline-update:
  code: |
    // Only update baseline when NOT spiking (prevents baseline inflation)
    if (!isSpike)
    {
        _cpuEma = Alpha * snapshot.CpuPercent + (1 - Alpha) * _cpuEma;
    }
```

## config

```yaml
environment:
  QYL_WATCH_INTERVAL: 5000      # poll interval ms
  QYL_WATCH_THRESHOLD: 3.0      # spike multiplier
  QYL_WATCH_SUSTAINED: 6        # samples before alert
  QYL_WATCH_COOLDOWN: 300000    # alert cooldown ms
  QYL_WATCH_OTLP_ENDPOINT: ""   # optional collector URL
  QYL_WATCH_IGNORE: ""          # comma-separated process names

cli-args:
  --interval: override poll interval
  --threshold: override spike threshold
  --once: single scan then exit
  --daemon: background mode
  --verbose: detailed output
  --kill <pid>: kill a process
```

## dependencies

```yaml
project-references: []

packages:
  - none required (BCL only ideal)
  - System.CommandLine (optional, for rich CLI)

forbidden:
  - Microsoft.AspNetCore.*
  - qyl.protocol
  - qyl.collector
  - Grpc.*
  - DuckDB.*
```

## commands

```yaml
run: dotnet run --project src/qyl.watchdog
pack: dotnet pack src/qyl.watchdog -c Release
install-local: dotnet tool install --global --add-source ./artifacts qyl.watchdog
```

## testing

```yaml
unit:
  - ProcessBaseline spike detection
  - AnomalyAnalyzer state management
  - Cooldown logic

integration:
  - MacOsProcessSampler returns valid data
  - MacOsNotificationSender triggers notification

manual:
  - Start watchdog
  - Run: `yes > /dev/null &` (creates 100% CPU process)
  - Verify notification appears within 35 seconds
  - Kill the yes process
  - Verify no more alerts
```

## known-zombies

Processes known to go rogue on macOS dev machines:

```yaml
ide-backends:
  - Rider.Backend
  - WebStorm
  - clangd
  - fsnotifier

system:
  - duetexpertd
  - mds_stores
  - bird
  - cloudd

browsers:
  - "Google Chrome Helper"
  - "Microsoft Edge Helper"
  - "Safari Web Content"
```

## implementation-order

1. `ProcessSnapshot` record + `IProcessSampler` interface
2. `MacOsProcessSampler` using `Process.GetProcesses()`
3. `ProcessBaseline` with EMA + spike detection
4. `AnomalyAnalyzer` managing baselines dictionary
5. `MacOsNotificationSender` using osascript
6. `Program.cs` main loop with PeriodicTimer
7. CLI argument parsing
8. Optional OTLP export
