using Qyl.Host;

var app = QylAppBuilder.Create(args);

var collector = app.AddCollector("collector", "services/qyl.collector", port: 5100,
    selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", port: 5200));

if (args.Contains("--dev", StringComparer.Ordinal))
{
    // --host 127.0.0.1: Node resolves `localhost` to ::1 only on this stack, but the runner
    // supervises (and health-probes) IPv4 loopback — bind where the probe looks.
    app.AddCommand("dashboard-dev", "npm", port: 5173,
            arguments: ["run", "dev", "--", "--host", "127.0.0.1"],
            workingDirectory: "services/qyl.dashboard")
        .WaitFor(collector);
}

if (args.Contains("--demo", StringComparer.Ordinal))
{
    app.AddProject("workload", "packages/Qyl.Run.Workload")
        .WithOtlpExporter(collector);
}

await app.Build().RunAsync().ConfigureAwait(false);
