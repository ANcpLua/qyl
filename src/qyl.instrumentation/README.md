# qyl.instrumentation

Zero-code OpenTelemetry instrumentation for ASP.NET Core applications.

## How it works

This package uses a C# Source Generator to intercept calls to `WebApplicationBuilder.Build()` and automatically injects:

1.  OpenTelemetry tracing and metrics configuration (`AddServiceDefaults()`).
2.  Default health and readiness endpoints (`MapServiceDefaults()`).

## Usage

Simply add a reference to this project/package. The generator will automatically instrument your `Program.cs`.

```xml
<ItemGroup>
    <ProjectReference Include="..\qyl.instrumentation\qyl.instrumentation.csproj" />
</ItemGroup>
```

Your `Program.cs` remains clean:

```csharp
var builder = WebApplication.CreateBuilder(args);
// ... services ...
var app = builder.Build(); // <-- Intercepted!
// ... middleware ...
app.Run();
```

The generator rewrites the `Build()` call to:

```csharp
Interceptors.InterceptBuild(builder);
```

Which executes:

```csharp
builder.AddServiceDefaults();
var app = builder.Build();
app.MapServiceDefaults();
return app;
```

## Configuration

- `OTEL_EXPORTER_OTLP_ENDPOINT`: Set this environment variable to enable OTLP export.
- `OTEL_SERVICE_NAME`: (Optional) Override service name.
