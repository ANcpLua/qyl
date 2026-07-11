using Qyl.Host;

// The runnable qyl distributed-app launcher. From the repo root:
//     dotnet run --project packages/Qyl.Run.Host
//
// Starts the collector (REST API + OTLP ingest + DuckDB; it also serves the built dashboard as static
// files), then blocks with the live TUI. The runner's own read-only resource state is exposed at
// http://127.0.0.1:18888/runner/resources (+ /stream) for the qyl.run.console runner UI.
//
// The collector's own self-telemetry goes one-way into a dedicated diagnostics collector — the same
// collector project started as a second process/resource (own API port, own OTLP receiver ports, own
// DuckDB, own service identity). The diagnostics instance's exporter stays disabled, so:
//
//     collector self-telemetry ──OTLP──> diagnostics collector
//     diagnostics self-telemetry ──X──> nowhere
//
// No third collector, no feedback loop; RejectSelfReference additionally refuses any resolved
// endpoint that would point back at the originating collector.
var app = QylAppBuilder.Create(args);

var collector = app.AddCollector("collector", port: 5100, project: "services/qyl.collector", selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", port: 5200).RejectSelfReference());

// `--dev`: also run the Vite dashboard (HMR) as a resource. It proxies /api + /v1 to the
// collector (vite.config.ts), so it waits for the collector; [B] opens it once Ready.
if (args.Contains("--dev", StringComparer.Ordinal))
{
    // --host 127.0.0.1: Node resolves `localhost` to ::1 only on this stack, but the runner
    // supervises (and health-probes) IPv4 loopback — bind where the probe looks.
    app.AddCommand("dashboard-dev", "npm run dev -- --host 127.0.0.1", port: 5173,
            workingDirectory: "services/qyl.dashboard")
        .WaitFor(collector);
}

// `--demo`: also run the synthetic workload (#510 ④⑤) — gen_ai/http/db traces through the
// SemConv generated surface, exported into the collector's OTLP receiver, so the dashboard is
// populated on first run. Registered after the collector, so [B] keeps opening the dashboard;
// OTEL_SERVICE_NAME=workload comes from the resource name, the OTLP endpoint from the
// collector's composed receiver port (same primitives the self-telemetry wiring uses).
if (args.Contains("--demo", StringComparer.Ordinal))
{
    app.AddProject("workload", "packages/Qyl.Run.Workload")
        .WithEnvironment(QylConstants.Env.OtelExporterOtlpEndpoint,
            collector.GetEndpoint(QylConstants.EndpointKinds.OtlpHttp).ToString().TrimEnd('/'))
        .WithEnvironment(QylConstants.Env.OtelExporterOtlpProtocol, QylConstants.Collector.OtlpHttpProtobuf)
        .WaitFor(collector);
}

await app.Build().RunAsync().ConfigureAwait(false);
