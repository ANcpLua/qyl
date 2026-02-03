; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
QYL001 | OpenTelemetry | Warning | Activity/Span missing semantic convention attributes
QYL002 | OpenTelemetry | Warning | Deprecated semantic convention
QYL003 | OpenTelemetry | Warning | ActivitySource should be registered with AddSource()
QYL004 | GenAI | Warning | GenAI span missing required attributes
QYL005 | GenAI | Warning | Use gen_ai.client.token.usage histogram for token metrics
QYL006 | GenAI | Warning | GenAI operation name should follow semantic conventions
QYL007 | Metrics | Warning | Meter should be registered with AddMeter()
QYL008 | Metrics | Warning | Metric instrument name should follow naming conventions
QYL009 | Configuration | Warning | ServiceDefaults configuration incomplete
QYL010 | Configuration | Warning | Collector endpoint should use OTLP protocol
QYL011 | Metrics | Error | [Meter] class must be partial static
QYL012 | Metrics | Error | Metric method must be partial
QYL013 | OpenTelemetry | Error | [Traced] attribute requires non-empty ActivitySourceName
QYL014 | GenAI | Warning | Deprecated GenAI semantic convention
QYL015 | Metrics | Warning | High-cardinality tag on metric
