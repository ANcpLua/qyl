# qyl.hosting - App Orchestration

Process manager for the qyl OS. GenAI-native application orchestration — no Aspire dependency. Manages the lifecycle of
all resources (projects, containers, frontends) and wires them to the kernel (collector) automatically.

## Key Types

| Type                              | Purpose                              |
|-----------------------------------|--------------------------------------|
| `Qyl`                             | Entry point, creates `QylAppBuilder` |
| `QylAppBuilder`                   | Fluent builder for resources         |
| `QylRunner`                       | Process lifecycle orchestrator       |
| `ProjectResource<T>`              | .NET projects                        |
| `ViteResource`                    | Vite/React/Vue frontends             |
| `NodeResource` / `PythonResource` | Polyglot apps                        |
| `ContainerResource`               | Docker containers                    |
| `PostgresResource`                | PostgreSQL                           |

## Fluent API

```csharp
var app = Qyl.CreateApp(args);
app.AddProject<Projects.MyApi>("api").WithGenAI();
app.AddVite("web", "../frontend").WithBrowserTelemetry().WaitFor(api);
app.Run();
```

Extensions: `.WithGenAI()` | `.WithCostTracking()` | `.WithHealthCheck()` | `.WithReference()` | `.WaitFor()` |
`.WithEnvironment()`
