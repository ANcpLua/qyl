# Qyl.Orchestration

Compose and supervise local .NET services and executables with the process supervisor of
[qyl](https://qyl.at), the one `qyl up` runs on. Targets .NET 10 and the ASP.NET Core shared
framework. It starts only the processes you add: no collector, telemetry exporter, control server or
console UI.

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

`AddDotnetService` takes a built assembly with its `runtimeconfig.json`; `AddExecutable` takes any
executable, with arguments passed directly, without a shell. Each service gets its own loopback
endpoint through `ASPNETCORE_URLS` and its name as `OTEL_SERVICE_NAME`; a reference arrives as
`services__<name>__http__0`. A successful `/health` response means ready, and
`WithHealthCheck("/health", requireProcessId: true)` also requires the body to equal the child's
process ID. `WithoutEnvironmentPrefix` removes inherited variables before the `WithEnvironment`
values apply.

`Build` checks for dependency cycles and reserves the ports. `StartAsync` waits until every process
is ready (60 seconds by default) and stops all children on failure or cancellation; an early exit
reports the child's recent output. After readiness, an unexpected exit restarts the process up to
`MaxRestarts` times, three by default. On shutdown each process stops before the services it waits
for, so its final exports still arrive. On Unix, stop, restart and disposal send `SIGTERM` and allow
ten seconds before the process tree is killed; on Windows the main window is asked to close where
there is one, otherwise the process is terminated. `DisposeAsync` stops the process trees and
releases the reservations, even when the application never started. A builder and its application
are one run.

The library manages processes. It does not build projects, provision databases or containers, or
configure telemetry beyond each process's service name.

## License

qyl is free, under the MIT license
([LICENSE](https://github.com/ANcpLua/qyl/blob/main/LICENSE)). Source:
https://github.com/ANcpLua/qyl.
