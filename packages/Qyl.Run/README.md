# Qyl.Run

Distributed-app runner for the [qyl](https://github.com/ancplua/qyl) observability platform.
A full Aspire-AppHost replacement with **zero Aspire dependencies** — ships on the shared
`Microsoft.AspNetCore.App` framework plus Spectre.Console for the CLI.

## Surface

```csharp
using Qyl.Run;

var app = QylAppBuilder.Create(args);

var collectorDev  = app.AddCollector("collector-dev",  port: 5100, environment: "dev",
                                     project: "services/qyl.collector");
var collectorProd = app.AddCollector("collector-prod", environment: "prod",
                                     project: "services/qyl.collector");

app.AddDashboard("dashboard", port: 5050, project: "services/qyl.dashboard")
   .WithCollector(collectorDev)
   .WithCollector(collectorProd)
   .WaitFor(collectorDev, collectorProd);

app.AddMcp("mcp", project: "services/qyl.mcp")
   .WaitFor(collectorDev);

await app.Build().RunAsync();
```

## CLI

```
 __ _  _   _ | |
/ _` || | | || |
\__, ||_|_|_||_|
 __/ |
|___/
v0.1.0 — qyl distributed-app runner

╭─────────────────┬────────┬───────┬───────────────────────╮
│                 │ Status │ Port  │ Endpoint              │
├─────────────────┼────────┼───────┼───────────────────────┤
│ collector-dev   │   ●    │ 5100  │ http://127.0.0.1:5100 │
│ collector-prod  │   ●    │ 5101  │ http://127.0.0.1:5101 │
│ dashboard       │   ●    │ 5050  │ http://127.0.0.1:5050 │
│ mcp             │   ●    │ 18891 │ http://127.0.0.1:18891│
╰─────────────────┴────────┴───────┴───────────────────────╯
[S] Stop   [R] Restart   [B] Open browser   [H] Help   [Esc] Exit
```

`[B]` always opens the first declared `dashboard` resource.

## Weight

|                    | Aspire                                               | Qyl.Run                    |
|--------------------|------------------------------------------------------|----------------------------|
| Third-party NuGets | 11 (KubernetesClient, Grpc.AspNetCore, Humanizer, …) | **1** (Spectre.Console)    |
| Framework refs     | `Microsoft.AspNetCore.App`                           | `Microsoft.AspNetCore.App` |
| LoC                | ~80k                                                 | ~650                       |

## Configuration

Bind `QylAppOptions` via the standard options pattern:

```csharp
app.Host.Services
       .AddOptionsWithValidateOnStart<QylAppOptions>()
       .BindConfiguration(QylAppOptions.SectionName)
       .ValidateDataAnnotations();
```

The builder sets this up automatically on `QylAppBuilder.Create(args)` — you only need to
override it if you want custom validation.

## License

MIT © 2025-2026 ancplua
