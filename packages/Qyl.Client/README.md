# Qyl.Client

Generated .NET REST client for the [qyl](https://github.com/ancplua/qyl) observability platform.

The client surface is emitted by `@typespec/http-client-csharp` from the TypeSpec models under
`core/specs/` and lives in the `Qyl.Api` namespace (sub-clients: `TracesApi`, `MetricsApi`,
`ProfilesApi`, `SessionsApi`, `DeploymentsApi`, …).

```csharp
using Qyl.Api;

var client = new ApiClient(new Uri("https://collector.qyl.dev"), credential);
await foreach (var span in client.GetTracesApi().ListAsync(limit: 100))
{
    Console.WriteLine(span.Name);
}
```

Pair with `Qyl.OpenTelemetry.Extensions` for zero-boilerplate OTLP wiring:

```csharp
services.AddQylOpenTelemetry(o =>
{
    o.Endpoint = new Uri("https://collector.qyl.dev");
    o.ServiceName = "my-service";
    o.ApiKey = builder.Configuration["Qyl:ApiKey"];
});
```

## License

MIT © 2025-2026 ancplua
