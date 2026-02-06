# qyl.hosting - App Orchestration

GenAI-native application orchestration framework. No Aspire dependency.

## Key Types

| Type | Purpose |
|------|---------|
| `Qyl` | Entry point, creates `QylAppBuilder` |
| `QylAppBuilder` | Fluent builder for resources |
| `QylRunner` | Process lifecycle orchestrator |
| `ProjectResource<T>` | .NET projects |
| `ViteResource` | Vite/React/Vue frontends |
| `NodeResource` / `PythonResource` | Polyglot apps |
| `ContainerResource` | Docker containers |
| `PostgresResource` | PostgreSQL |

## Fluent API

```csharp
var app = Qyl.CreateApp(args);
app.AddProject<Projects.MyApi>("api").WithGenAI();
app.AddVite("web", "../frontend").WithBrowserTelemetry().WaitFor(api);
app.Run();
```

Extensions: `.WithGenAI()` | `.WithCostTracking()` | `.WithHealthCheck()` | `.WithReference()` | `.WaitFor()` | `.WithEnvironment()`
