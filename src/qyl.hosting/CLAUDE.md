# qyl.hosting

GenAI-native application orchestration - the better AppHost.

## What This Is

A standalone orchestration framework that manages distributed applications with built-in observability. No Aspire dependency - qyl IS the orchestration layer.

## API Design Philosophy

**Minimal, expressive, just works:**

```csharp
var app = Qyl.CreateApp(args);

var api = app.AddProject<Projects.MyApi>("api")
    .WithGenAI();  // GenAI instrumentation + cost tracking

var frontend = app.AddVite("web", "../frontend")
    .WithBrowserTelemetry()  // console.log → qyl
    .WaitFor(api);

app.Run();
```

## Key Types

- `Qyl` - Entry point, creates `QylAppBuilder`
- `QylAppBuilder` - Fluent builder for adding resources
- `QylRunner` - Orchestrates process lifecycle
- Resources:
  - `ProjectResource<T>` - .NET projects
  - `ViteResource` - Vite/React/Vue frontends
  - `NodeResource` - Node.js apps
  - `PythonResource` / `UvicornResource` - Python apps
  - `ContainerResource` - Docker containers
  - `PostgresResource` - PostgreSQL

## Extension Methods Pattern

All resources use fluent chaining:

```csharp
.WithGenAI()           // Enable GenAI instrumentation
.WithCostTracking()    // Track LLM costs
.WithHealthCheck()     // Configure health endpoint
.WithReference(other)  // Inject connection info
.WaitFor(dependency)   // Startup ordering
.WithEnvironment(k,v)  // Set env vars
```

## What Makes This Better Than Aspire

1. **GenAI-first** - Instrumentation is the default, not an addon
2. **Zero dependencies** - No Aspire.Hosting, no DCP
3. **Embedded qyl collector** - Dashboard comes free
4. **Simpler mental model** - Resources, dependencies, run
5. **Browser telemetry** - Frontend console → qyl dashboard
