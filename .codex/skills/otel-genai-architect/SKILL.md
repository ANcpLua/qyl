---
name: otel-genai-architect
description: |
  OpenTelemetry instrumentation for AI/GenAI - semantic conventions, traces, metrics, logs.
---

## Source Metadata

```yaml
frontmatter:
  model: opus
  tools: ["*"]
```


# OTel GenAI Architect

Specialist for OpenTelemetry instrumentation of AI/LLM applications.

## When to Use

- Adding OTel tracing to LLM integrations
- Reviewing telemetry for semantic convention compliance
- Designing observability for AI systems
- Validating trace/metric/log configurations

## GenAI Semantic Conventions (v1.39)

### Span Attributes
```
gen_ai.provider.name = "openai" | "anthropic" | "azure"
gen_ai.request.model = "claude-3-opus"
gen_ai.request.max_tokens = 4096
gen_ai.response.model = "claude-3-opus-20240229"
gen_ai.usage.input_tokens = 150
gen_ai.usage.output_tokens = 500
```

### Metrics
| Metric | Unit | Description |
|--------|------|-------------|
| `gen_ai.client.token.usage` | tokens | Token consumption |
| `gen_ai.client.operation.duration` | seconds | Request latency |
| `gen_ai.client.time_to_first_token` | seconds | Streaming TTFT |

### Activity Source Naming
```csharp
private static readonly ActivitySource Source = new("myapp.gen_ai");
```

## Implementation Pattern

```csharp
using var activity = Source.StartActivity("chat", ActivityKind.Client);
activity?.SetTag("gen_ai.provider.name", "anthropic");
activity?.SetTag("gen_ai.request.model", "claude-3-opus");

var response = await client.SendAsync(request);

activity?.SetTag("gen_ai.usage.input_tokens", response.InputTokens);
activity?.SetTag("gen_ai.usage.output_tokens", response.OutputTokens);
```

## Review Checklist

- [ ] Correct attribute names (gen_ai.*, not custom)
- [ ] Proper span kinds (Client for outbound calls)
- [ ] Token metrics captured
- [ ] Error recording follows OTel conventions
