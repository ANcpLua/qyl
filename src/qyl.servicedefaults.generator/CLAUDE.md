# qyl.servicedefaults.generator - Auto-Instrumentation

Roslyn source generator for automatic OTel instrumentation via C# interceptors.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | netstandard2.0 |
| Pattern | C# 12 interceptors |

## Purpose

Zero-config compile-time telemetry: intercepts GenAI SDK calls, database calls, and `[Traced]` methods. Emits OTel spans with semantic conventions. No runtime reflection.

## Analyzers -> Emitters

| Analyzer | Emitter | Detects |
|----------|---------|---------|
| `GenAiCallSiteAnalyzer` | `GenAiInterceptorEmitter` | GenAI SDK calls |
| `DbCallSiteAnalyzer` | `DbInterceptorEmitter` | Database calls |
| `OTelTagAnalyzer` | `OTelTagEmitter` | OTel tag definitions |
| `MeterAnalyzer` | `MeterEmitter` | Meter classes |
| `TracedCallSiteAnalyzer` | `TracedInterceptorEmitter` | [Traced] methods |

## Provider Detection

Type-name pattern matching: `Anthropic.*` -> anthropic, `OpenAI.*` -> openai, `Azure.AI.OpenAI.*` -> azure

## Rules

- Target netstandard2.0 for analyzer compatibility
- Use `IIncrementalGenerator` pattern only
- Generated code must be AOT-compatible
