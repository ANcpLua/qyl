# qyl.instrumentation.generators

Roslyn source generators for telemetry instrumentation.

## identity

```yaml
sdk: ANcpLua.NET.Sdk
role: compile-time-only
output: Analyzer (no runtime reference)
```

## generators

```yaml
# Planned/In-progress generators for automatic instrumentation
```

## usage

Referenced as analyzer in consuming projects:

```xml
<ProjectReference Include="..\qyl.instrumentation.generators\..."
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"/>
```

## dependencies

```yaml
packages:
  - Microsoft.CodeAnalysis.CSharp
  - ANcpLua.Roslyn.Utilities
```

## patterns

```csharp
[Generator]
public class MyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Incremental generator pattern
    }
}
```
