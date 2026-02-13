# qyl.watchdog - System Resource Watchdog

## Overview

Lightweight daemon that monitors system processes, detects anomalous resource consumption, and alerts via native notifications before your Mac becomes a space heater.

## Identity

```yaml
name: qyl.watchdog
type: console-daemon
sdk: ANcpLua.NET.Sdk
role: local-dev-tool
distribution: dotnet-global-tool
install: dotnet tool install -g qyl.watchdog
run: qyl-watchdogdog
```

## Problem Statement

Developer workstations accumulate zombie processes:
- Orphaned IDE backends (Rider.Backend, WebStorm, etc.)
- Runaway browser tabs with infinite loops
- System daemons stuck in loops (duetexpertd, mds_stores)
- Background indexers that never finish

Users only notice when:
- Laptop is physically hot
- Fans are screaming
- System becomes laggy
- Battery drains rapidly

By then, productivity is already impacted.

## Design Principles

1. **Opinionated defaults** - Works immediately, no config required
2. **Minimal footprint** - < 10MB RAM, < 0.5% CPU
3. **Native notifications** - macOS Notification Center, no dashboard
4. **Optional telemetry** - Can emit to qyl.collector for history
5. **Smart detection** - Baseline learning, not fixed thresholds

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      qyl.watchdog                           │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │   Sampler    │───▶│   Analyzer   │───▶│   Alerter    │  │
│  │  (5s poll)   │    │  (baseline)  │    │ (notify/otel)│  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
│         │                   │                    │          │
│         ▼                   ▼                    ▼          │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │ ProcessInfo  │    │  Baselines   │    │  Cooldowns   │  │
│  │   (ps/top)   │    │ (per-process)│    │(spam prevent)│  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ (optional)
                    ┌──────────────────┐
                    │  qyl.collector   │
                    │  (OTLP export)   │
                    └──────────────────┘
```

## Components

### 1. Sampler

Polls system process stats at configurable interval (default: 5 seconds).

```csharp
public sealed class ProcessSampler(TimeProvider timeProvider)
{
    public IReadOnlyList<ProcessSnapshot> Sample()
    {
        // macOS: use libproc via P/Invoke or Process.GetProcesses()
        // Returns: PID, Name, CPU%, Memory bytes, StartTime
    }
}

public readonly record struct ProcessSnapshot(
    int Pid,
    string Name,
    double CpuPercent,
    long MemoryBytes,
    long StartTimeUnixMs);
```

**Platform strategies:**
- macOS: `Process.GetProcesses()` + `/proc` style via `libproc`
- Linux: `/proc/[pid]/stat` parsing
- Windows: `Process.GetProcesses()` (built-in)

### 2. Analyzer

Maintains per-process baselines using exponential moving average (EMA).

```csharp
public sealed class AnomalyAnalyzer
{
    private readonly ConcurrentDictionary<int, ProcessBaseline> _baselines = new();

    public AnomalyResult Analyze(ProcessSnapshot snapshot)
    {
        var baseline = _baselines.GetOrAdd(snapshot.Pid,
            _ => new ProcessBaseline(snapshot.Name));

        return baseline.Update(snapshot);
    }
}

public sealed class ProcessBaseline(string name)
{
    private double _cpuEma = 0;
    private int _spikeCount = 0;

    private const double Alpha = 0.1;           // EMA smoothing factor
    private const double SpikeThreshold = 3.0;  // 3x baseline = spike
    private const int SustainedCount = 6;       // 30s at 5s intervals

    public AnomalyResult Update(ProcessSnapshot snapshot)
    {
        var isSpike = snapshot.CpuPercent > Math.Max(_cpuEma * SpikeThreshold, 50);

        if (isSpike)
        {
            _spikeCount++;
            if (_spikeCount >= SustainedCount)
            {
                return AnomalyResult.Anomaly(name, snapshot, _cpuEma);
            }
        }
        else
        {
            _spikeCount = 0;
            _cpuEma = Alpha * snapshot.CpuPercent + (1 - Alpha) * _cpuEma;
        }

        return AnomalyResult.Normal;
    }
}
```

**Detection algorithm:**
1. Maintain EMA of CPU% per process
2. Spike = current > max(baseline * 3, 50%)
3. Sustained = spike persists for 6 samples (30 seconds)
4. Only then trigger alert
5. Reset spike counter when process returns to normal

**Why this works:**
- EMA adapts to processes that legitimately use more CPU sometimes
- 50% minimum threshold prevents false positives on idle processes
- Sustained requirement filters transient spikes (compilation, etc.)
- Per-process tracking catches zombie processes specifically

### 3. Alerter

Sends native notifications and optionally emits telemetry.

```csharp
public sealed class Alerter(INotificationSender sender, IOtlpExporter? exporter)
{
    private readonly ConcurrentDictionary<int, long> _cooldowns = new();
    private const long CooldownMs = 300_000; // 5 minutes

    public async ValueTask AlertAsync(AnomalyResult anomaly, CancellationToken ct)
    {
        if (!ShouldAlert(anomaly.Pid)) return;

        await sender.SendAsync(
            title: $"High CPU: {anomaly.ProcessName}",
            body: $"PID {anomaly.Pid} using {anomaly.CpuPercent:F0}% CPU (baseline: {anomaly.BaselineCpu:F0}%)",
            ct);

        if (exporter is not null)
        {
            await exporter.ExportAsync(anomaly, ct);
        }

        _cooldowns[anomaly.Pid] = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();
    }

    private bool ShouldAlert(int pid)
    {
        if (!_cooldowns.TryGetValue(pid, out var lastAlert)) return true;
        var now = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();
        return now - lastAlert > CooldownMs;
    }
}
```

**Notification strategies:**

macOS (primary):
```csharp
public sealed class MacOsNotificationSender : INotificationSender
{
    public async ValueTask SendAsync(string title, string body, CancellationToken ct)
    {
        var script = $"""
            display notification "{body}" with title "{title}" sound name "Basso"
            """;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e '{script}'",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });

        await process!.WaitForExitAsync(ct);
    }
}
```

### 4. Optional OTLP Export

Emit anomalies as OTel spans to qyl.collector for historical analysis.

```csharp
public sealed class OtlpExporter(HttpClient http) : IOtlpExporter
{
    public async ValueTask ExportAsync(AnomalyResult anomaly, CancellationToken ct)
    {
        var span = new
        {
            name = "system.process.anomaly",
            kind = "INTERNAL",
            attributes = new Dictionary<string, object>
            {
                ["process.pid"] = anomaly.Pid,
                ["process.name"] = anomaly.ProcessName,
                ["process.cpu.percent"] = anomaly.CpuPercent,
                ["process.cpu.baseline"] = anomaly.BaselineCpu,
                ["host.name"] = Environment.MachineName
            }
        };

        await http.PostAsJsonAsync("http://localhost:5100/v1/traces", span, ct);
    }
}
```

## Configuration

Minimal config via environment variables (opinionated defaults):

```yaml
QYL_WATCH_INTERVAL: 5000        # Poll interval in ms (default: 5000)
QYL_WATCH_THRESHOLD: 3.0        # Spike multiplier (default: 3.0)
QYL_WATCH_SUSTAINED: 6          # Samples before alert (default: 6)
QYL_WATCH_COOLDOWN: 300000      # Alert cooldown ms (default: 300000)
QYL_WATCH_OTLP_ENDPOINT: ""     # Optional: http://localhost:5100
QYL_WATCH_IGNORE: "kernel_task,WindowServer"  # Comma-separated ignore list
```

## CLI Interface

```bash
# Start watching (foreground)
qyl-watchdog

# Start as background daemon
qyl-watchdog --daemon

# One-shot check (exit after first scan)
qyl-watchdog --once

# Verbose output
qyl-watchdog --verbose

# Custom config
qyl-watchdog --interval 3000 --threshold 2.5

# Kill a runaway process (interactive)
qyl-watchdog --kill 46616
```

## Project Structure

```
src/qyl.watchdog/
├── Platform/
│   ├── IProcessSampler.cs
│   ├── MacOsProcessSampler.cs
│   ├── LinuxProcessSampler.cs
│   └── WindowsProcessSampler.cs
├── Detection/
│   ├── ProcessBaseline.cs
│   ├── AnomalyAnalyzer.cs
│   └── AnomalyResult.cs
├── Alerting/
│   ├── INotificationSender.cs
│   ├── MacOsNotificationSender.cs
│   └── IOtlpExporter.cs
├── CLAUDE.md
├── GlobalUsings.cs
├── Program.cs
├── WatchdogOptions.cs
└── qyl.watchdog.csproj
```

## Dependencies

```yaml
project-references: []  # Standalone, no qyl dependencies

packages:
  - System.CommandLine  # CLI parsing (optional, could use args directly)

forbidden:
  - ASP.NET Core        # Too heavy
  - qyl.protocol        # Keep standalone
  - qyl.collector       # Communicate via HTTP only
```

## Constraints

| Metric | Target | Rationale |
|--------|--------|-----------|
| RAM | < 10 MB | Must be invisible |
| CPU | < 0.5% | Can't be the problem it detects |
| Startup | < 500ms | Instant feedback |
| Binary | < 5 MB | Small global tool |

## Future Enhancements (YAGNI for now)

- [ ] Menu bar icon (macOS)
- [ ] Auto-kill option for known zombie patterns
- [ ] Memory anomaly detection
- [ ] Disk I/O monitoring
- [ ] Network anomaly detection
- [ ] Integration with launchd for auto-start

## Success Criteria

1. Detects Rider.Backend zombie within 30 seconds
2. Sends native notification before laptop gets hot
3. Uses less resources than the problems it detects
4. Zero configuration required for basic use
