# qyl.instrumentation.generators - Source Generators

Roslyn source generators for telemetry instrumentation.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | netstandard2.0 |
| Role | compile-time-only |
| Output | Analyzer DLL |

## Purpose

Provides compile-time instrumentation for consuming projects. Generators emit code that:

- Creates OTel spans automatically
- Captures method arguments as span attributes
- Handles async methods correctly
- Supports interceptor patterns

## Usage

Reference as analyzer in consuming projects:

```xml
<ProjectReference Include="..\qyl.instrumentation.generators\qyl.instrumentation.generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Generator Pattern

```csharp
[Generator]
public class MyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Create syntax provider
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, _) => IsCandidate(node),
            transform: (ctx, _) => GetModel(ctx));

        // 2. Combine with compilation
        var compilation = context.CompilationProvider.Combine(provider.Collect());

        // 3. Register output
        context.RegisterSourceOutput(compilation, (spc, source) =>
        {
            var (comp, models) = source;
            foreach (var model in models)
            {
                spc.AddSource($"{model.Name}.g.cs", GenerateCode(model));
            }
        });
    }
}
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.CodeAnalysis.CSharp` | Roslyn APIs |
| `ANcpLua.Roslyn.Utilities` | Generator utilities |

## Rules

- Target netstandard2.0 for analyzer compatibility
- Use incremental generators only (IIncrementalGenerator)
- Never emit code at runtime - compile-time only
- Test with ANcpLua.Roslyn.Utilities.Testing
