# Qyl.Orchestration

Compose local .NET services and executables using the same process supervisor as the qyl CLI.
Targets .NET 10 and the ASP.NET Core shared framework. No collector, dashboard, control server,
telemetry exporter or console UI is started by this package.

```csharp
using Qyl.Orchestration;

var builder = new QylApplicationBuilder { StartupTimeoutSeconds = 30, MaxRestarts = 0 };
var payments = builder.AddDotnetService("payments-api", paymentsAssembly);
builder.AddDotnetService("orders-api", ordersAssembly)
    .WithReference(payments)
    .WaitFor(payments);

await using var app = builder.Build();
await app.StartAsync(cancellationToken);
using var orders = new HttpClient { BaseAddress = app.GetEndpoint("orders-api") };
```

Services receive distinct loopback endpoints through `ASPNETCORE_URLS`; references arrive as
`services__<name>__http__0`. Arguments go directly to the process, without a shell. By default a
successful `/health` response means ready. `WithHealthCheck("/health", requireProcessId: true)`
also requires the response body to equal that child's process ID. `WithoutEnvironmentPrefix`
removes inherited settings before `WithEnvironment` values are applied.

`Build` validates dependency cycles and reserves ports. `StartAsync` waits for the complete
application and stops all children on failure or cancellation; early process exit includes the
child's recent output. `DisposeAsync` stops process trees and releases reservations, including
when the application was never started. A builder and application each represent one run.
Unexpected exits after readiness use a bounded restart policy (three retries by default).
On application shutdown, each process finishes before its `WaitFor` dependencies stop,
so final exports can still reach their destination.
On Unix, disposal, stop and restart send SIGTERM and allow up to ten seconds for host
shutdown and exporter flushing before killing the process tree. Windows requests window
closure when supported; headless processes fall back to forced termination.

Use `Qyl.Telemetry.Hosting` in the services to export telemetry to a collector or test receiver.
This library manages processes; it does not build projects, provision databases or containers,
or configure application telemetry beyond each process's default service name.
