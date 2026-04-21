# Qyl.Fleet

Distributed-app hosting for the [qyl](https://github.com/ancplua/qyl) observability platform.

One `QylDashboardResource` per dashboard instance, a fluent `.WithCollector(...)` to declare
each backend, and an in-process reverse proxy that routes the dashboard REST + SSE surface
across the collector fleet by a `{backendPrefix}/{tail}` path convention.

## Install

```sh
dotnet add package Qyl.Fleet
```

## Usage

```csharp
using Aspire.Hosting;
using Qyl.Fleet.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var collectorDev  = builder.AddProject<Projects.Qyl_Collector>("collector-dev");
var collectorProd = builder.AddProject<Projects.Qyl_Collector>("collector-prod");

builder.AddQylDashboard("dashboard", port: 5050)
       .WithCollector(collectorDev,  new QylCollectorInfo("dev",  "Local dev") { Environment = "dev" })
       .WithCollector(collectorProd, new QylCollectorInfo("prod", "Production") { Environment = "prod" })
       .WaitFor(collectorDev)
       .WaitFor(collectorProd);

builder.Build().Run();
```

## Routing contract

| Route                       | Behavior                                                                |
|-----------------------------|-------------------------------------------------------------------------|
| `GET  /health`              | `{ status: "healthy" }` — liveness probe.                               |
| `GET  /api/v1/fleet`        | Fans out metadata across all registered collectors; tagged `_backend`.  |
| `*    /api/v1/{**path}`     | Routes by first path segment (e.g. `/api/v1/traces/dev/<id>` → `dev`). SSE inherits the same prefix rule; `Accept: text/event-stream` switches to streamed proxy. |

Writes are deterministic: every non-GET must carry a collector prefix as the first path segment
so the aggregator can target exactly one backend.

## Why no fan-out writes

Writing to multiple backends from one aggregator call silently doubles state mutations. Callers
pass a collector prefix in the URL so the aggregator targets exactly one backend.

## License

MIT © 2025-2026 ancplua
