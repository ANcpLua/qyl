using Qyl.Run;

// The runnable qyl distributed-app launcher. From the repo root:
//     dotnet run --project packages/Qyl.Run.Host
//
// Starts the collector (REST API + OTLP ingest + DuckDB; it also serves the built dashboard as static
// files), then blocks with the live TUI. The runner's own read-only resource state is exposed at
// http://127.0.0.1:18888/runner/resources (+ /stream) for the qyl.run.console runner UI.
//
// Note: resources are launched via `dotnet run --project <path>`, so only .NET projects can be added as of 7th july 4pm.
var app = QylAppBuilder.Create(args);

app.AddCollector("collector", port: 5100, project: "services/qyl.collector");

await app.Build().RunAsync().ConfigureAwait(false);
