# Qyl.Fleet

Dashboard fleet hosting for the [qyl](https://github.com/ancplua/qyl) observability platform.

Registers an in-process reverse proxy (`IHostedService`) that routes the dashboard REST + SSE
surface across one or more `qyl.collector` backends by a `{collectorId}/{tail}` path convention.

## Weight

**Zero third-party NuGets.** Ships on the shared `Microsoft.AspNetCore.App` framework reference
only — no Aspire, no Kubernetes client, no gRPC runtime, no Humanizer. Total package surface is
four C# files under 300 LoC.

## Install

```sh
dotnet add package Qyl.Fleet
```

## Usage

```csharp
using Qyl.Fleet.Hosting;

var host = Host.CreateApplicationBuilder(args);

host.Services.AddQylFleet(fleet =>
{
    fleet.Port = 5050;
    fleet.WithCollector("dev",  new Uri("http://localhost:5100"), description: "Local dev");
    fleet.WithCollector("prod", new Uri("https://collector.prod"),
                        description: "Production", environment: "prod");
});

host.Build().Run();
```

## Routing contract

| Route                       | Behavior                                                                |
|-----------------------------|-------------------------------------------------------------------------|
| `GET  /health`              | `{ status: "healthy" }` — liveness probe.                               |
| `GET  /api/v1/fleet`        | Lists every registered collector with its metadata and endpoint.        |
| `*    /api/v1/{**path}`     | Routes by first path segment (e.g. `/api/v1/traces/dev/<id>` → `dev`). `Accept: text/event-stream` switches to streamed proxy (SSE). |

Writes are deterministic: every non-GET must carry a collector id as the first path segment so
the aggregator targets exactly one backend. No silent fan-out.

## License

MIT © 2025-2026 ancplua
