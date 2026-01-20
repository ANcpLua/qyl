# qyl.instrumentation

Runtime instrumentation library for qyl observability platform.

## identity

```yaml
name: qyl.instrumentation
type: nuget-library
sdk: ANcpLua.NET.Sdk
role: instrumentation-runtime
```

## instrumentation coverage

### auto-instrumented (zero code changes)

These are instrumented automatically when you call `AddServiceDefaults()`:

| Domain | Source | Attributes |
|--------|--------|------------|
| HTTP Server | OpenTelemetry.Instrumentation.AspNetCore | http.request.method, url.path, http.response.status_code |
| HTTP Client | OpenTelemetry.Instrumentation.Http | http.request.method, url.full, server.address |
| Runtime | OpenTelemetry.Instrumentation.Runtime | process.runtime.dotnet.gc.*, threadpool.* |

### manual instrumentation (requires code)

These require explicit code because no standard auto-instrumentation exists:

| Domain | How to Instrument | Why Manual |
|--------|-------------------|------------|
| GenAI/LLM | `GenAiActivitySource.StartChat()` | No OTel standard library exists - every AI SDK is different |
| Custom Business | `ActivitySource.StartActivity()` | Business-specific, cannot be auto-detected |

## genai instrumentation

GenAI is the key differentiator - qyl provides helpers other tools don't:

```csharp
using qyl.instrumentation.GenAi;

// Wrap any AI SDK call
using var activity = GenAiActivitySource.StartChat("openai", "gpt-4o");
activity?.SetRequestTemperature(0.7);
activity?.SetRequestMaxTokens(1000);

var response = await openAiClient.CompleteChatAsync(messages);

activity?.SetResponseModel(response.Model);
activity?.SetResponseId(response.Id);
activity?.SetTokenUsage(response.Usage.PromptTokens, response.Usage.CompletionTokens);
activity?.SetFinishReason(response.FinishReason);
```

### supported providers

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
var builder = WebApplication.CreateSlimBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapServiceDefaults();
app.Run();
```

## what interceptors CAN and CANNOT do

### can intercept (compile-time)
- Static method calls in YOUR code
- Extension method calls in YOUR code
- Builder pattern methods in YOUR code

### cannot intercept
- Interface calls (`IHttpClient.SendAsync()`)
- Virtual dispatch (runtime polymorphism)
- Calls inside compiled NuGet packages
- Calls made by third-party libraries

### honest assessment

C# interceptors are NOT equivalent to Datadog's CLR Profiling API:
- Datadog uses runtime bytecode modification (like Java agents)
- .NET has no public CLR Profiling API for safe runtime modification
- Interceptors are compile-time only, limited to source code you compile

This is why GenAI manual instrumentation exists - there's no way to auto-instrument arbitrary AI SDK calls without runtime modification.

## dependencies

```yaml
packages:
  - OpenTelemetry.Api
  - OpenTelemetry.Extensions.Hosting
  - OpenTelemetry.Instrumentation.AspNetCore
  - OpenTelemetry.Instrumentation.Http
  - OpenTelemetry.Instrumentation.Runtime
  - OpenTelemetry.Exporter.OpenTelemetryProtocol
  - OpenTelemetry.Exporter.Console

project-references:
  - qyl.instrumentation.generators (Analyzer)
```
