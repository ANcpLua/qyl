# qyl.instrumentation.generators - Source Generators

Roslyn source generators for telemetry instrumentation.

## Identity

| Property  | Value           |
|-----------|-----------------|
| SDK       | ANcpLua.NET.Sdk |
| Framework | netstandard2.0  |
| Output    | Analyzer DLL    |

## Purpose

Compile-time instrumentation: auto-generates OTel spans, captures method arguments as attributes, handles async methods,
supports interceptor patterns.

## Usage

```xml
<ProjectReference Include="...\qyl.instrumentation.generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Rules

- Target netstandard2.0
- IIncrementalGenerator only
- Compile-time only, no runtime emission
