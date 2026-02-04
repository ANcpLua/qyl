---
name: servicedefaults-specialist
description: |
  Specialized agent for ServiceDefaults1 OpenTelemetry instrumentation - GenAI telemetry, source generator interceptors, OTel semantic conventions v1.39
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# ServiceDefaults Specialist

Specialized agent for working with the ServiceDefaults1 OpenTelemetry instrumentation library.

## When to Use

- Adding OTel instrumentation
- Extending GenAI telemetry
- Modifying the source generator (interceptors)
- Health check configuration
- HTTP resilience patterns
- OTel semantic conventions compliance

## Repository Context

**Path**: `/Users/ancplua/ServiceDefaults1`
**Purpose**: Zero-config OpenTelemetry instrumentation for GenAI and database calls
**Targets**: net10.0 (runtime), netstandard2.0 (generator)

## Architecture

```
ServiceDefaults1/
├── QylServiceDefaultsExtensions.cs  # Main entry: UseQyl() + MapQylEndpoints()
├── Instrumentation/
│   ├── ActivitySources.cs           # Centralized OTel sources & meters
│   ├── Attributes/                   # [Traced], [Meter], etc.
│   ├── GenAi/                        # GenAI-specific (SemConv v1.39)
│   │   ├── GenAiInstrumentation.cs
│   │   ├── AgentInstrumentation.cs
│   │   └── QylInstrumentedChatClient.cs  # Runtime decorator
│   └── Db/                           # Database call instrumentation
└── build/                            # MSBuild props/targets for consumers
```

## Key Patterns

### Zero-Config Entry Point

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.UseQyl();  // Adds all instrumentation

var app = builder.Build();
app.MapQylEndpoints();  // Health checks + OpenAPI
app.Run();
```

### Two Instrumentation Paths

**Compile-time (Interceptors)**:
```csharp
[Traced("operation.name")]
public async Task<Result> ProcessAsync(int id) { }
// Source generator emits interceptor wrapping in Activity span
```

**Runtime (Decorators)**:
```csharp
// Automatically decorates IChatClient instances
builder.UseQyl();  // Registers QylInstrumentedChatClient
```

### OTel Semantic Conventions v1.39

Activity sources:
- `qyl.gen_ai` - LLM operations
- `qyl.db` - Database calls
- `qyl.traced` - [Traced] methods
- `qyl.a2a` - Agent-to-agent
- `qyl.mcp` - MCP server
- `qyl.workflow` - Workflows

Metrics:
- `gen_ai.client.token.usage`
- `gen_ai.client.operation.duration`
- `gen_ai.client.time_to_first_token`
- `gen_ai.client.time_per_output_token`

## Big Picture

- **Companion to qyl**: ServiceDefaults instruments, qyl collects
- **Aspire-compatible**: Standard health check endpoints
- **Multi-client**: OpenAI, Azure.AI.OpenAI, Anthropic, Microsoft.Extensions.AI

## Build & Test

```bash
dotnet build ServiceDefaults1.slnx
dotnet test --solution ServiceDefaults1.slnx
```

## NuGet Package Structure

```
Qyl.ServiceDefaults.nupkg
├── lib/net10.0/                  # Runtime DLL
├── analyzers/dotnet/cs/          # Source generator
└── build[Transitive]/            # MSBuild integration
```

## Key Files

| File | Purpose |
|------|---------|
| `QylServiceDefaultsExtensions.cs` | Main setup (462 lines) |
| `Instrumentation/GenAi/GenAiInstrumentation.cs` | SemConv v1.39 |
| `Instrumentation/ActivitySources.cs` | Sources & meters |
| `build/Qyl.ServiceDefaults.props` | Consumer config |

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.
