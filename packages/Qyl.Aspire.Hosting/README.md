# Qyl.Aspire.Hosting

Aspire hosting integration for the [qyl](https://github.com/ancplua/qyl) observability platform.

Mirrors the shape of `Aspire.Hosting.AgentFramework.DevUI` from the Microsoft Agent Framework:
one Aspire resource per dashboard instance, a fluent `.WithCollector(...)` to declare each
backend, and an in-process reverse proxy that fans the dashboard REST + SSE surface out across
the fleet by a `{backendPrefix}/{tail}` path convention.

## Install

```sh
dotnet add package Qyl.Aspire.Hosting
```

## Usage

```csharp
using Aspire.Hosting;
using Aspire.Hosting.Qyl;

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
| `GET  /health`              | `{ status: "healthy" }` — AppHost probe.                                |
| `GET  /api/v1/fleet`        | Fans out metadata across all registered collectors; tagged `_backend`.  |
| `*    /api/v1/{**path}`     | Routes by first path segment (e.g. `/api/v1/traces/dev/<id>` → `dev`). SSE inherits the same prefix rule; `Accept: text/event-stream` switches to streamed proxy. |

Writes are deterministic: every non-GET must carry a collector prefix as the first path segment
so the aggregator can target exactly one backend.

## Why no fan-out writes

Writing to multiple backends from one aggregator call silently doubles state mutations. The
Microsoft DevUI solves the same problem by making callers pass an explicit `entity_id` with a
backend prefix — qyl does the same via URL prefix.

## License

MIT © 2025-2026 ancplua
