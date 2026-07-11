# Qyl.Host

Distributed-app runner for the [qyl](https://github.com/ancplua/qyl) observability platform.
A full Aspire-AppHost replacement with **zero Aspire dependencies** — ships on the shared
`Microsoft.AspNetCore.App` framework plus Spectre.Console for the CLI.

## Surface

```csharp
using Qyl.Host;

var app = QylAppBuilder.Create(args);

// Collector + one-way self-telemetry: the collector's own OTLP telemetry flows into a
// dedicated diagnostics collector (same project, second process) whose exporter stays
// disabled — no third collector, no feedback loop.
app.AddCollector("collector", port: 5100, project: "services/qyl.collector",
    selfTelemetry: static telemetry => telemetry
        .ExportToDedicatedCollector("diagnostics", port: 5200)
        .RejectSelfReference());

// Any other .NET project:
app.AddProject("worker", "services/my.worker");

// Any dev command as a resource — readiness is a successful GET on healthPath (default "/"):
app.AddCommand("dashboard-dev", "npm run dev", port: 5173, workingDirectory: "services/qyl.dashboard");

await app.Build().RunAsync();
```

`WaitFor(dependencies...)` holds a resource's launch until every dependency reports Ready; a
terminally failed dependency fails the dependent instead of hanging it, and unknown names or
cycles fail `Build()`. The self-telemetry pair wires it automatically — the diagnostics sink
comes up before the collector so the earliest self-telemetry has somewhere to land.

`AddCollector` pins the child's ports through `QYL_PORT` / `QYL_OTLP_PORT` / `QYL_GRPC_PORT`
(unique across the composition — the first collector keeps 4318/4317, later ones claim free
ports) and defaults loopback dev children to `QYL_OTLP_AUTH_MODE=Unsecured` unless the parent
environment already chose an auth mode. The dedicated diagnostics instance gets its own
`qyl.<name>.duckdb`, its own `OTEL_SERVICE_NAME` (plus the SDK's per-process
`service.instance.id`), and a force-blanked `OTEL_EXPORTER_OTLP_ENDPOINT`.
`ExportTo(existing)` targets an already-added collector instead.

Safety is automatic: self-reference, port-identity, and two-process-cycle validation always
runs at composition time (`RejectSelfReference()` is call-site documentation), and the
collector re-validates the resolved endpoint at startup — a loopback alias, the machine's own
host name, or any local-interface address on one of its own ports is fatal at boot.

The composition primitives `ExportToDedicatedCollector` builds on are public for custom
topologies: `WithEnvironment(name, value)`, `WithIsolatedStorage()`,
`DisableSelfTelemetryExport()`, and `GetEndpoint("api" | "otlp-http" | "otlp-grpc")`.

## CLI

```
 __ _  _   _ | |
/ _` || | | || |
\__, ||_|_|_||_|
 __/ |
|___/
v0.1.0 — qyl distributed-app runner

╭─────────────┬────────┬──────┬────────────────────────╮
│             │ Status │ Port │ Endpoint               │
├─────────────┼────────┼──────┼────────────────────────┤
│ collector   │   ●    │ 5100 │ http://127.0.0.1:5100/ │
│ diagnostics │   ●    │ 5200 │ http://127.0.0.1:5200/ │
╰─────────────┴────────┴──────┴────────────────────────╯
[S] Stop   [R] Restart   [B] Open browser   [H] Help
```

## Weight

|                    | Aspire                                               | Qyl.Host                    |
|--------------------|------------------------------------------------------|----------------------------|
| Third-party NuGets | 11 (KubernetesClient, Grpc.AspNetCore, Humanizer, …) | **1** (Spectre.Console)    |
| Framework refs     | `Microsoft.AspNetCore.App`                           | `Microsoft.AspNetCore.App` |
| LoC                | ~80k                                                 | ~1,700                     |

## Configuration

`QylAppOptions` (`Qyl:Host` section — `RunnerPort`, `RunnerHost`, `StartupTimeoutSeconds`)
is bound reflection-free in `Build()` via `QylAppOptions.FromConfiguration`, which
validates imperatively and fails fast — the trim/AOT-clean replacement for
`AddOptionsWithValidateOnStart` + DataAnnotations. Override values through any
configuration source added before `Build()` (env vars, appsettings, command line).

## License

MIT © 2025-2026 ancplua
