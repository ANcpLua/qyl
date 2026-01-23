# qyl.instrumentation

Runtime instrumentation library for qyl observability platform.

## identity

```yaml
name: qyl.instrumentation
type: nuget-library
sdk: ANcpLua.NET.Sdk
role: instrumentation-runtime
```

## purpose

GenAI instrumentation helpers following OTel 1.39 semantic conventions. For basic OTel setup (HTTP, runtime, health checks), use `ANcpSdk.AspNetCore.ServiceDefaults` from the SDK.

## what this library provides

| Feature | How | Why Needed |
|---------|-----|------------|
| GenAI/LLM instrumentation | `GenAiActivitySource.StartChat()` | No OTel standard library - every AI SDK is different |
| OTel 1.39 gen_ai.* attributes | Extension methods | Fluent API for setting attributes |
| Provider constants | `GenAiActivitySource.Providers.*` | Standardized provider names |

## supported providers

```yaml
providers:
  - openai
  - anthropic
  - google
  - azure_openai
  - aws_bedrock
  - cohere
  - mistral
  - meta
  - groq
  - together_ai
  - fireworks
  - local
```

### otel 1.39 gen_ai.* attributes

All attributes follow OTel 1.39 semantic conventions:

```yaml
request:
  - gen_ai.provider.name
  - gen_ai.operation.name
  - gen_ai.request.model
  - gen_ai.request.temperature
  - gen_ai.request.top_p
  - gen_ai.request.top_k
  - gen_ai.request.max_tokens
  - gen_ai.request.stop_sequences
  - gen_ai.request.frequency_penalty
  - gen_ai.request.presence_penalty
  - gen_ai.request.seed

response:
  - gen_ai.response.model
  - gen_ai.response.id
  - gen_ai.response.finish_reasons

usage:
  - gen_ai.usage.input_tokens
  - gen_ai.usage.output_tokens
  - gen_ai.usage.input_tokens.cached
  - gen_ai.usage.output_tokens.reasoning

tools:
  - gen_ai.tool.name
  - gen_ai.tool.call.id
  - gen_ai.conversation.id

agent:
  - gen_ai.agent.id
  - gen_ai.agent.name
  - gen_ai.agent.description

qyl-extensions:
  - qyl.cost.usd
  - qyl.session.id
```

## usage

```csharp
using qyl.instrumentation.GenAi;

// Instrument any GenAI call
using var activity = GenAiActivitySource.StartChat("openai", "gpt-4o");
activity?.SetRequestTemperature(0.7);

var response = await client.CompleteChatAsync(messages);

activity?.SetResponseModel(response.Model);
activity?.SetTokenUsage(response.Usage.InputTokens, response.Usage.OutputTokens);
activity?.SetFinishReason(response.FinishReason);
```

## dependencies

```yaml
packages:
  - OpenTelemetry.Api  # ActivitySource
```
