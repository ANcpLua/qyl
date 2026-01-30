# qyl.servicedefaults.generator - Auto-Instrumentation Generator

Roslyn source generator for automatic OTel instrumentation using interceptors.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | netstandard2.0 |
| Role | compile-time-only |
| Pattern | C# 12 interceptors |

## Purpose

Provides zero-configuration telemetry instrumentation at compile time:

- Intercepts GenAI SDK calls (Anthropic, OpenAI, etc.)
- Intercepts database calls
- Auto-generates OTel spans with semantic conventions
- No runtime reflection required

## Analyzers

| Analyzer | Purpose |
|----------|---------|
| `GenAiCallSiteAnalyzer` | Detects GenAI SDK method calls |
| `DbCallSiteAnalyzer` | Detects database calls |
| `OTelTagAnalyzer` | Extracts OTel tag definitions |
| `MeterAnalyzer` | Processes meter class definitions |
| `TracedCallSiteAnalyzer` | Detects [Traced] attributed methods |

## Emitters

| Emitter | Output |
|---------|--------|
| `GenAiInterceptorEmitter` | GenAI span interceptors |
| `DbInterceptorEmitter` | Database span interceptors |
| `OTelTagEmitter` | Tag extension methods |
| `MeterEmitter` | Meter implementations |
| `TracedInterceptorEmitter` | Method tracing interceptors |

## Provider Registry

GenAI providers are detected by type name patterns:

```csharp
// Models.cs - ProviderRegistry
{ "Anthropic", new ProviderInfo("anthropic", TypeContains: "Anthropic") },
{ "OpenAI", new ProviderInfo("openai", TypeContains: "OpenAI") },
{ "Azure.AI.OpenAI", new ProviderInfo("azure", TypeContains: "Azure.AI.OpenAI") },
```

## Adding a New GenAI Provider

1. Add provider constant to `qyl.protocol/Attributes/GenAiAttributes.cs`
2. Add to `ProviderRegistry` in `Models/Models.cs` with `TypeContains` pattern
3. Add method patterns to `GenAiCallSiteAnalyzer.cs`
4. Add constant mapping in `GenAiInterceptorEmitter.cs`
5. Create manual wrapper if rich context needed

## Generated Attributes

Follows OTel 1.39 GenAI semantic conventions:

| Attribute | Source |
|-----------|--------|
| `gen_ai.system` | Provider registry |
| `gen_ai.operation.name` | Method name mapping |
| `gen_ai.request.model` | Parameter extraction |
| `gen_ai.usage.input_tokens` | Response extraction |
| `gen_ai.usage.output_tokens` | Response extraction |

## Interceptor Pattern

```csharp
// Generated interceptor
[InterceptsLocation("path/to/file.cs", line: 42, column: 15)]
public static async Task<Response> Intercept_CreateMessageAsync(
    this IAnthropicClient client,
    CreateMessageRequest request,
    CancellationToken ct = default)
{
    using var activity = ActivitySource.StartActivity("gen_ai.chat");
    activity?.SetTag("gen_ai.system", "anthropic");
    activity?.SetTag("gen_ai.request.model", request.Model);

    var response = await client.CreateMessageAsync_Original(request, ct);

    activity?.SetTag("gen_ai.usage.input_tokens", response.Usage.InputTokens);
    activity?.SetTag("gen_ai.usage.output_tokens", response.Usage.OutputTokens);

    return response;
}
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.CodeAnalysis.CSharp` | Roslyn APIs |
| `ANcpLua.Roslyn.Utilities` | Generator utilities |

## Rules

- Target netstandard2.0 for analyzer compatibility
- Use incremental generator pattern (`IIncrementalGenerator`)
- Provider detection is type-name based (no runtime reflection)
- Generated code must be AOT-compatible
