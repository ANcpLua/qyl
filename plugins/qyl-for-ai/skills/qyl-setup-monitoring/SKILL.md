---
name: qyl-setup-monitoring
description: Set up AI agent monitoring with OpenTelemetry and qyl. Use when the user wants to instrument their application, add tracing, set up GenAI monitoring, or configure OTLP export to qyl.
license: Apache-2.0
category: setup
parent: qyl-workflow
disable-model-invocation: true
---

> [All Skills](../../SKILL_TREE.md) > [Workflow](../qyl-workflow/SKILL.md) > Setup Monitoring

# Set Up AI Agent Monitoring

Instrument applications with OpenTelemetry to send telemetry to qyl.

## Invoke This Skill When

- User wants to add observability to their application
- User asks about setting up tracing, logging, or metrics
- User wants to monitor AI agent behavior (GenAI instrumentation)
- User asks how to connect their app to qyl

## Quick Start (Any Language)

Set the environment variable and your OTel SDK will export to qyl:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

## .NET Setup

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddQylServiceDefaults();
var app = builder.Build();
app.MapQylEndpoints();
app.Run();
```

## Python Setup

```python
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter

provider = TracerProvider()
provider.add_span_processor(BatchSpanProcessor(
    OTLPSpanExporter(endpoint="http://localhost:4318/v1/traces")
))
trace.set_tracer_provider(provider)
```

## GenAI Monitoring

qyl follows OTel Semantic Conventions 1.40 for GenAI:

| Attribute                    | Description                             |
|------------------------------|-----------------------------------------|
| `gen_ai.system`              | Provider (openai, anthropic, etc.)      |
| `gen_ai.request.model`       | Model name                              |
| `gen_ai.usage.input_tokens`  | Input token count                       |
| `gen_ai.usage.output_tokens` | Output token count                      |
| `gen_ai.operation.name`      | Operation (chat, completion, embedding) |

## Verification

After instrumenting, use `qyl.health_check` to verify the connection, then `search_traces` to confirm telemetry is
flowing.
