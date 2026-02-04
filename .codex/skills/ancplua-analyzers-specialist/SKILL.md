---
name: ancplua-analyzers-specialist
description: |
  Specialized agent for ANcpLua.Analyzers Roslyn analyzer package - creating diagnostics (AL00XX), code fix providers, and optimizing analyzer performance
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# ANcpLua Analyzers Specialist

Specialized agent for working with the ANcpLua.Analyzers Roslyn analyzer package.

## When to Use

- Creating new diagnostics (AL00XX)
- Adding code fix providers
- Debugging analyzer behavior
- Testing analyzer output
- Optimizing analyzer performance

## Repository Context

**Path**: `/Users/ancplua/ANcpLua.Analyzers`
**Purpose**: 44 Roslyn diagnostics enforcing modern C# patterns
**Target**: netstandard2.0 (analyzer) + net10.0 (tests)

## Architecture

```
src/
├── ANcpLua.Analyzers/
│   ├── Analyzers/           # One file per AL00XX diagnostic
│   └── Core/
│       ├── ALAnalyzer.cs    # Base class (inherit from this)
│       ├── OperationHelper.cs # Shared IOperation utilities
│       └── WellKnownTypes.cs  # Type metadata constants
└── ANcpLua.Analyzers.CodeFixes/
    └── CodeFixes/           # One file per code fix
```

## Patterns to Follow

### Creating a New Analyzer

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AL00XXMyAnalyzer : AlAnalyzer
{
    public const string DiagnosticId = "AL00XX";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: Resources.AL00XX_Title,
        messageFormat: Resources.AL00XX_MessageFormat,
        category: "Category",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    protected override void RegisterActions(AnalysisContext context)
    {
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        // Analysis logic
    }
}
```

### Using OperationHelper

```csharp
// Get operand name from IOperation (unwraps conversions)
var name = OperationHelper.GetOperandName(operation);

// Unwrap implicit conversions
var unwrapped = OperationHelper.UnwrapConversions(operation);
```

## Big Picture

- **Source of Truth**: ANcpLua.Roslyn.Utilities provides shared helpers
- **Auto-injection**: ANcpLua.NET.Sdk injects this package into all projects
- **Dogfooding**: This repo's code is analyzed by its own analyzers

## Before Writing New Code

Check if a utility already exists in:
1. `Core/OperationHelper.cs` (this repo)
2. ANcpLua.Roslyn.Utilities (upstream source of truth)

## Build & Test

```bash
dotnet build ANcpLua.Analyzers.slnx
dotnet test --solution ANcpLua.Analyzers.slnx
```

## Diagnostic Categories

- AL0001-AL0006: Pattern Safety
- AL0007-AL0009: XML Serialization
- AL0010: Generator Support
- AL0011: Threading
- AL0012-AL0013: OpenTelemetry
- AL0014-AL0016: Code Style
- AL0017-AL0019: Version Management
- AL0020-AL0024: Form Binding
- AL0025-AL0027: Performance
- AL0028-AL0040: Roslyn Utilities Extensions
- AL0041-AL0044: AOT/Trim Testing

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.
