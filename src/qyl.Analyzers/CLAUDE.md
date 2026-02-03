# qyl.Analyzers

Roslyn diagnostic analyzers for OpenTelemetry, GenAI, and qyl instrumentation patterns.

## Diagnostic Rules

### OpenTelemetry (QYL001-003, QYL013)

| ID | Severity | Description |
|----|----------|-------------|
| QYL001 | Warning | Activity/Span missing semantic convention attributes |
| QYL002 | Warning | Deprecated semantic convention (http.method → http.request.method) |
| QYL003 | Warning | ActivitySource should be registered with AddSource() |
| QYL013 | Error | [Traced] attribute requires non-empty ActivitySourceName |

### GenAI (QYL004-006, QYL014)

| ID | Severity | Description |
|----|----------|-------------|
| QYL004 | Warning | GenAI span missing required attributes (gen_ai.system, gen_ai.request.model) |
| QYL005 | Warning | Use gen_ai.client.token.usage histogram for token metrics |
| QYL006 | Warning | GenAI operation name should be: chat, text_completion, or embeddings |
| QYL014 | Warning | Deprecated GenAI semantic convention attribute |

### Metrics (QYL007-008, QYL011-012, QYL015)

| ID | Severity | Description |
|----|----------|-------------|
| QYL007 | Warning | Meter should be registered with AddMeter() |
| QYL008 | Warning | Metric name should follow naming conventions (dot-separated) |
| QYL011 | Error | [Meter] class must be partial static |
| QYL012 | Error | [Counter]/[Histogram] method must be partial |
| QYL015 | Warning | High-cardinality tag on metric (user.id, request.id) |

### Configuration (QYL009-010)

| ID | Severity | Description |
|----|----------|-------------|
| QYL009 | Warning | ServiceDefaults configuration incomplete (missing tracing/metrics) |
| QYL010 | Warning | Collector endpoint should use OTLP protocol |

## Commands

```bash
# Build
dotnet build src/qyl.Analyzers/qyl.Analyzers.csproj

# Test
dotnet test --project tests/qyl.Analyzers.Tests/qyl.Analyzers.Tests.csproj

# Pack
dotnet pack src/qyl.Analyzers/qyl.Analyzers.csproj -c Release -o artifacts
```

## Project Structure

```
src/qyl.Analyzers/
├── Analyzers/           # DiagnosticAnalyzer implementations
│   └── Qyl0XX*.cs       # One file per diagnostic
├── Core/
│   └── QylAnalyzer.cs   # Base class, DiagnosticIds, Categories
├── Resources.resx       # Localized diagnostic strings
└── AnalyzerReleases/    # Release tracking

src/qyl.Analyzers.CodeFixes/
├── CodeFixes/           # CodeFixProvider implementations
│   └── Qyl0XX*.cs       # One file per code fix
└── CodeFixResources.resx
```

## Adding a New Analyzer

1. Add DiagnosticId to `Core/QylAnalyzer.cs`
2. Add resource strings to `Resources.resx` (Title, MessageFormat, Description)
3. Create analyzer in `Analyzers/QylXXXNameAnalyzer.cs`
4. Update `AnalyzerReleases/AnalyzerReleases.Unshipped.md`
5. Add tests in `tests/qyl.Analyzers.Tests/`

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.CodeAnalysis.CSharp | Roslyn APIs |
| ANcpLua.Roslyn.Utilities | Shared Roslyn helpers |

## Targets

- `net10.0` - Development/testing
- `netstandard2.0` - NuGet package (analyzer compatibility)
