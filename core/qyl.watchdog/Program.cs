// qyl.watchdog - System Resource Watchdog
// See DESIGN.md for full specification
// See CLAUDE.md for implementation guide

Console.WriteLine("qyl.watchdog - TODO: implement");
Console.WriteLine("See DESIGN.md and CLAUDE.md for specs");

// Implementation order:
// 1. ProcessSnapshot record + IProcessSampler interface
// 2. MacOsProcessSampler using Process.GetProcesses()
// 3. ProcessBaseline with EMA + spike detection
// 4. AnomalyAnalyzer managing baselines dictionary
// 5. MacOsNotificationSender using osascript
// 6. Main loop with PeriodicTimer
// 7. CLI argument parsing
// 8. Optional OTLP export
