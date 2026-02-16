# qyl.watchdog

Lightweight process anomaly detection daemon for developer workstations.

## Identity

| Property | Value                               |
|----------|-------------------------------------|
| SDK      | ANcpLua.NET.Sdk                     |
| Type     | console-daemon / dotnet global tool |
| Command  | `qyl-watchdog`                      |

## Purpose

Monitors system processes, detects anomalous CPU via EMA baselines, alerts via native notifications. Standalone — no qyl
dependencies.

## Architecture

| Component                             | Purpose                          |
|---------------------------------------|----------------------------------|
| `Platform/ProcessSampler.cs`          | Poll process stats (5s interval) |
| `Detection/AnomalyAnalyzer.cs`        | EMA baseline + spike detection   |
| `Detection/ProcessBaseline.cs`        | Per-process EMA state            |
| `Alerting/Alerter.cs`                 | Notification dispatch + cooldown |
| `Alerting/MacOsNotificationSender.cs` | osascript notifications          |

## Algorithm

- EMA baseline (alpha=0.1), updated only when not spiking
- Spike: CPU > max(baseline * 3.0, 50%) for 6 consecutive samples (30s)
- Cooldown: 5 min per PID

## Config

| Variable                  | Default | Purpose                |
|---------------------------|---------|------------------------|
| `QYL_WATCH_INTERVAL`      | 5000    | Poll interval ms       |
| `QYL_WATCH_THRESHOLD`     | 3.0     | Spike multiplier       |
| `QYL_WATCH_SUSTAINED`     | 6       | Samples before alert   |
| `QYL_WATCH_COOLDOWN`      | 300000  | Alert cooldown ms      |
| `QYL_WATCH_OTLP_ENDPOINT` | (none)  | Optional collector URL |

## Constraints

- Standalone: no qyl.protocol, no ASP.NET Core, no DuckDB
- RAM < 10MB, CPU < 0.5%, binary < 5MB
- BCL-only preferred
