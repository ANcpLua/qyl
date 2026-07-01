using Qyl.Run;

// The runnable qyl distributed-app launcher. From the repo root:
//     dotnet run --project packages/Qyl.Run.Host
//
// Starts the collector (REST API + OTLP ingest + DuckDB; it also serves the built dashboard as static
// files), then blocks with the live TUI. The runner's own read-only resource state is exposed at
// http://127.0.0.1:18888/runner/resources (+ /stream) for qyl.run.dashboard.
//
// Note: resources are launched via `dotnet run --project <path>`, so only .NET projects can be added
// today; launching the dashboard's Vite dev server (npm) is a separate, planned launcher capability.
var app = QylAppBuilder.Create(args);

app.AddCollector("collector", port: 5100, project: "services/qyl.collector");

await app.Build().RunAsync().ConfigureAwait(false);
