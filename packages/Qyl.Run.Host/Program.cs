using Qyl.Run;

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
//
// Note: resources are launched via `dotnet run --project <path>`, so only .NET projects can be added as of 7th july 4pm.
var app = QylAppBuilder.Create(args);

app.AddCollector("collector", port: 5100, project: "services/qyl.collector", selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", port: 5200).RejectSelfReference());

await app.Build().RunAsync().ConfigureAwait(false);
