# qyl.agents.telemetry

OpenTelemetry GenAI Semantic Conventions (v1.38.0) for qyl.

## Overview

This package provides:
- **GenAiSemanticConventions** - Constants for all GenAI semantic convention attributes
- **TracerProviderBuilderExtensions** - Helpers for configuring OpenTelemetry tracing

## Usage

### Configure OpenTelemetry

```csharp
using qyl.agents.telemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddQylAgentInstrumentation()  // Adds Microsoft.Agents.AI.*, Microsoft.Extensions.AI.*, qyl.agents.ai
    .AddOtlpExporter()
    .Build();
```

### Use with Microsoft Agent Framework

```csharp
using Microsoft.Agents.AI;

// Agent Framework provides built-in OpenTelemetry support
AIAgent agent = chatClient
    .CreateAIAgent(instructions: "...", name: "MyAgent")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "my-app")  // Framework built-in
    .Build();
```

### Access Semantic Convention Constants

```csharp
using qyl.agents.telemetry;

// Use constants for attribute names (collector-side parsing)
var operationName = GenAiSemanticConventions.Operation.Name;  // "gen_ai.operation.name"
var agentId = GenAiSemanticConventions.Agent.Id;              // "gen_ai.agent.id"
var inputTokens = GenAiSemanticConventions.Usage.InputTokens; // "gen_ai.usage.input_tokens"
```

## Why This Package?

This package provides **semantic convention constants only**. Agent instrumentation should use
Microsoft Agent Framework's built-in `UseOpenTelemetry()` extension method, which implements
the full [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/).

The constants in this package are useful for:
- Collector-side span attribute parsing
- Dashboard and query construction
- Analyzer rules (deprecated attribute detection)

## Semantic Conventions v1.38.0

Supported attribute groups:
- `gen_ai.operation.*` - Operation metadata
- `gen_ai.agent.*` - Agent identification
- `gen_ai.request.*` - Request parameters
- `gen_ai.response.*` - Response metadata
- `gen_ai.usage.*` - Token usage
- `gen_ai.tool.*` - Tool definitions and calls
- `error.*` - Error information

See [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) for details.
