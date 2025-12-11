# C4: SDKs & Instrumentation

> Telemetry constants, provider integrations, and ASP.NET Core SDK

## Overview

The SDK components provide GenAI semantic convention constants, provider-specific integrations (Gemini), ASP.NET Core
middleware, and MCP server tools for AI assistant integration.

## Key Classes/Modules

### qyl.agents.telemetry

| Class                             | Purpose                        | Location                             |
|-----------------------------------|--------------------------------|--------------------------------------|
| `GenAiAttributes`                 | v1.38 attribute name constants | `GenAiAttributes.cs`                 |
| `QylAttributes`                   | qyl-specific extensions        | `QylAttributes.cs`                   |
| `TracerProviderBuilderExtensions` | OTel configuration             | `TracerProviderBuilderExtensions.cs` |

### qyl.providers.gemini

| Class                        | Purpose                    | Location                        |
|------------------------------|----------------------------|---------------------------------|
| `GeminiChatClientExtensions` | Gemini integration helpers | `GeminiChatClientExtensions.cs` |

### qyl.sdk.aspnetcore

| Class                     | Purpose                 | Location                     |
|---------------------------|-------------------------|------------------------------|
| `QylAspNetCoreExtensions` | Middleware registration | `QylAspNetCoreExtensions.cs` |

### qyl.mcp.server

| Class                  | Purpose                | Location                        |
|------------------------|------------------------|---------------------------------|
| `TelemetryTools`       | MCP tools for querying | `Tools/TelemetryTools.cs`       |
| `TelemetryJsonContext` | JSON serialization     | `Tools/TelemetryJsonContext.cs` |

## Dependencies

**Internal:** qyl.grpc (models)

**External:** OpenTelemetry SDK, Microsoft.Extensions.AI, ModelContextProtocol SDK

## Data Flow

```
Application Code
    ↓
qyl.sdk.aspnetcore middleware
    ↓
OpenTelemetry TracerProvider
    ↓
Uses GenAiAttributes constants
    ↓
Exports to qyl.collector
```

## Patterns Used

- **Extension Methods**: All SDKs use extension methods for fluent config
- **Static Constants**: GenAiAttributes provides compile-time attribute names
- **Builder Pattern**: TracerProviderBuilder configuration
