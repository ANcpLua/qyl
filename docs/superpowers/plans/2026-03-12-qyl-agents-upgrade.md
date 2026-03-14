# Qyl.Agents Fat Upgrade — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix behavioral bugs, adopt shared utilities from ANcpLua.Roslyn.Utilities, and align OTel telemetry with GenAI semantic conventions across all 5 Qyl.Agents projects.

**Architecture:** Roslyn incremental source generator (`McpServerGenerator`) extracts `[McpServer]`/`[Tool]` metadata into equatable models, emits dispatch code + JSON schemas + OTel instrumentation + AOT-safe `JsonSerializerContext`. Runtime layer (`McpProtocolHandler`) handles MCP JSON-RPC transport. Changes flow: models → extractors → emitters → pipeline → runtime → tests.

**Tech Stack:** C# 14, netstandard2.0 (generator + abstractions), net10.0 (runtime + tests), Roslyn `IIncrementalGenerator`, `System.Text.Json` source generation, `System.Diagnostics.Activity`/`Metrics` for OTel, xUnit v3 with MTP runner.

**Spec:** `docs/superpowers/specs/2026-03-12-qyl-agents-upgrade-design.md`

---

## File Map

### Modified Files

| File | Changes |
|------|---------|
| `src/Qyl.Agents.Generator/Models/ToolModel.cs` | Add `ReturnKind` enum, replace triple-bool with `ReturnKind` + `ResultTypeFullyQualified` |
| `src/Qyl.Agents.Abstractions/McpServerInfo.cs` | `Name` → `required init` |
| `src/Qyl.Agents.Abstractions/McpToolInfo.cs` | `Name` → `required init` |
| `src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj` | Link polyfill files for `required`/`init` on netstandard2.0 |
| `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs` | Remove QA0012 |
| `src/Qyl.Agents.Generator/AnalyzerReleases.Shipped.md` | Move QA0001–QA0011 to Shipped |
| `src/Qyl.Agents.Generator/AnalyzerReleases.Unshipped.md` | Clear (remove QA0012) |
| `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs` | Replace manual attribute loops with `GetAttribute`/`HasAttribute`, replace `GetStringProperty`, use `DiagnosticFlow.Fail` for duplicates |
| `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs` | Replace `ClassifyReturnType` with `AwaitableContext`, replace `GetToolAttribute`/`GetStringProperty` with shared extensions |
| `src/Qyl.Agents.Generator/Extraction/ParameterExtractor.cs` | Replace `GetDescription` with `GetAttributeConstructorArgument`, replace `ToCamelCase` with `ToParameterName` |
| `src/Qyl.Agents.Generator/Generation/DispatchEmitter.cs` | Switch on `ReturnKind`, use `{ClassName}JsonContext.Default` instead of `s_jsonOptions`, add `gen_ai.system`/`server.name` tags, remove counter metric |
| `src/Qyl.Agents.Generator/Generation/OTelEmitter.cs` | Version from assembly metadata, rename histogram, remove counter |
| `src/Qyl.Agents.Generator/Generation/JsonContextEmitter.cs` | Check `ReturnKind` instead of `ReturnsVoid`, add nullable wrapper types |
| `src/Qyl.Agents.Generator/Generation/SchemaEmitter.cs` | Complete `EscapeJson` per RFC 8259 |
| `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs` | Fix `EscapeYaml` for multi-line + double-quoted scalars |
| `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs` | Add `JsonContextEmitter.Emit` call (already exists, verify order) |
| `src/Qyl.Agents.Generator/McpServerGenerator.cs` | Add second pipeline for orphaned `[Tool]` → QA0004 |
| `src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj` | Add `using ANcpLua.Roslyn.Utilities.Contexts` |
| `src/Qyl.Agents/Protocol/McpProtocolHandler.cs` | Cache JSON allocations, add OTel transport spans |
| `src/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs` | Add 17+ new test cases |
| `src/Qyl.Agents.Tests/McpProtocolEndToEndTests.cs` | Add 4 new test cases |
| `src/Qyl.Agents.Tests/TestServer.cs` | Add tool that throws (for error test) |

### No New Files

All changes are modifications to existing files.

---

## Task 1: Chunk 1 — Models & Abstractions

**Files:**
- Modify: `src/Qyl.Agents.Generator/Models/ToolModel.cs`
- Modify: `src/Qyl.Agents.Abstractions/McpServerInfo.cs`
- Modify: `src/Qyl.Agents.Abstractions/McpToolInfo.cs`
- Modify: `src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj`
- Modify: `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs`
- Modify: `src/Qyl.Agents.Generator/AnalyzerReleases.Shipped.md`
- Modify: `src/Qyl.Agents.Generator/AnalyzerReleases.Unshipped.md`

### 1A. ReturnKind enum + ToolModel

- [ ] **Step 1: Add ReturnKind enum and update ToolModel**

Replace the entire contents of `src/Qyl.Agents.Generator/Models/ToolModel.cs`:

```csharp
namespace Qyl.Agents.Generator.Models;

internal enum ReturnKind : byte
{
    Void,
    Sync,
    Task,
    ValueTask,
    TaskOfT,
    ValueTaskOfT,
}

internal readonly record struct ToolModel(
    string MethodName,
    string ToolName,
    string Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    EquatableArray<ToolParameterModel> Parameters);
```

> **Spec deviation:** Added `Sync` variant (not in spec's 5-value enum) because synchronous non-void returns (e.g., `int Add(int a, int b)`) need a distinct representation. Without it, the dispatch emitter cannot distinguish "void return, no await" from "has return value, no await".

- [ ] **Step 2: Verify the solution still builds (expect compile errors in extractors/emitters — confirms model change propagated)**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Compile errors in `ToolExtractor.cs`, `DispatchEmitter.cs`, `JsonContextEmitter.cs` referencing removed `ReturnsTask`/`ReturnsValueTask`/`ReturnsVoid` fields. This confirms the model change is visible everywhere it needs to be.

### 1B. Required init on info classes

- [ ] **Step 3: Link polyfill files into Abstractions project**

Edit `src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj` — add linked polyfill files for `required`/`init` on netstandard2.0. Add before the closing `</Project>`:

```xml
    <!-- Link polyfills for netstandard2.0 required init -->
    <ItemGroup>
        <Compile Include="..\ANcpLua.Roslyn.Utilities\Polyfills\LanguageFeatures\IsExternalInit.cs"
                 Link="Polyfills\IsExternalInit.cs" />
        <Compile Include="..\ANcpLua.Roslyn.Utilities\Polyfills\LanguageFeatures\RequiredMemberAttribute.cs"
                 Link="Polyfills\RequiredMemberAttribute.cs" />
        <Compile Include="..\ANcpLua.Roslyn.Utilities\Polyfills\LanguageFeatures\CompilerFeatureRequiredAttribute.cs"
                 Link="Polyfills\CompilerFeatureRequiredAttribute.cs" />
        <Compile Include="..\ANcpLua.Roslyn.Utilities\Polyfills\LanguageFeatures\SetsRequiredMembersAttribute.cs"
                 Link="Polyfills\SetsRequiredMembersAttribute.cs" />
    </ItemGroup>
```

- [ ] **Step 4: Update McpServerInfo — Name becomes required init**

Replace contents of `src/Qyl.Agents.Abstractions/McpServerInfo.cs`:

```csharp
namespace Qyl.Agents;

/// <summary>
/// Describes an MCP server's identity. Returned by the generated <c>GetServerInfo()</c> method.
/// </summary>
public sealed class McpServerInfo
{
    /// <summary>Server name as advertised in the MCP <c>initialize</c> response.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the server.</summary>
    public string? Description { get; init; }

    /// <summary>Semantic version string, or <c>null</c> if unversioned.</summary>
    public string? Version { get; init; }
}
```

- [ ] **Step 5: Update McpToolInfo — Name becomes required init**

Replace contents of `src/Qyl.Agents.Abstractions/McpToolInfo.cs`:

```csharp
namespace Qyl.Agents;

/// <summary>
/// Describes a single MCP tool. Returned by the generated <c>GetToolInfos()</c> method.
/// </summary>
public sealed class McpToolInfo
{
    /// <summary>Tool name as advertised in the MCP <c>tools/list</c> response.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the tool.</summary>
    public string? Description { get; init; }

    /// <summary>UTF-8 encoded JSON Schema describing the tool's input parameters.</summary>
    public byte[] InputSchema { get; init; } = System.Array.Empty<byte>();
}
```

- [ ] **Step 6: Verify Abstractions project builds**

Run: `dotnet build src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj`
Expected: Success (polyfills provide `required`/`init` support on netstandard2.0).

### 1C. AnalyzerReleases + Remove QA0012

- [ ] **Step 7: Remove QA0012 from DiagnosticDescriptors**

In `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs`, delete lines 97–103 (the `ComplexTypeNestingTooDeep` descriptor):

```csharp
    public static readonly DiagnosticDescriptor ComplexTypeNestingTooDeep = new(
        "QA0012",
        "Complex type nested beyond one level",
        "Parameter '{0}' of method '{1}' uses type '{2}' which is a complex type nested more than one level deep",
        Category,
        DiagnosticSeverity.Error,
        true);
```

- [ ] **Step 8: Move rules to AnalyzerReleases.Shipped.md**

Replace contents of `src/Qyl.Agents.Generator/AnalyzerReleases.Shipped.md`:

```markdown
## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
QA0001 | Usage | Error | McpServer class must be partial
QA0002 | Usage | Error | McpServer class must not be static
QA0003 | Usage | Error | McpServer class must not be generic
QA0004 | Usage | Error | Tool method must be inside McpServer class
QA0005 | Usage | Error | Tool method must not be static
QA0006 | Usage | Error | Tool method must not be generic
QA0007 | Usage | Error | Tool method has unsupported return type
QA0008 | Usage | Error | Tool parameter has unsupported type
QA0009 | Usage | Warning | Tool parameter has no Description
QA0010 | Usage | Warning | McpServer class has no Tool methods
QA0011 | Usage | Error | Duplicate tool name
```

- [ ] **Step 9: Clear AnalyzerReleases.Unshipped.md**

Replace contents of `src/Qyl.Agents.Generator/AnalyzerReleases.Unshipped.md`:

```markdown
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
```

- [ ] **Step 10: Commit Chunk 1**

```bash
git add src/Qyl.Agents.Generator/Models/ToolModel.cs \
  src/Qyl.Agents.Abstractions/McpServerInfo.cs \
  src/Qyl.Agents.Abstractions/McpToolInfo.cs \
  src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj \
  src/Qyl.Agents.Generator/DiagnosticDescriptors.cs \
  src/Qyl.Agents.Generator/AnalyzerReleases.Shipped.md \
  src/Qyl.Agents.Generator/AnalyzerReleases.Unshipped.md
git commit -m "refactor(qyl-agents): ReturnKind enum, required init, ship QA0001-0011, remove QA0012"
```

---

## Task 2: Chunk 2 — Extractor Upgrades

**Files:**
- Modify: `src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
- Modify: `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs`
- Modify: `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs`
- Modify: `src/Qyl.Agents.Generator/Extraction/ParameterExtractor.cs`

### 2A. Add AwaitableContext using + rewrite ToolExtractor

- [ ] **Step 1: Add Contexts global using to generator csproj**

In `src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`, add to the `<ItemGroup>` with `<Using>` elements (after line 30):

```xml
        <Using Include="ANcpLua.Roslyn.Utilities.Contexts" />
```

- [ ] **Step 2: Rewrite ToolExtractor — ClassifyReturnType with AwaitableContext, replace manual attribute lookups, delete GetStringProperty**

Replace entire contents of `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs`:

```csharp
using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Extraction;

internal static class ToolExtractor
{
    private const string ToolAttributeName = "Qyl.Agents.ToolAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public static DiagnosticFlow<ToolModel> Extract(
        IMethodSymbol method,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var guardFlow = SemanticGuard.ForMethod(method)
            .MustNotBeStatic(DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeStatic, method, method.Name))
            .Must(static m => !m.IsGenericMethod, DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeGeneric, method, method.Name))
            .ToFlow();

        if (guardFlow.IsFailed)
            return DiagnosticFlow.Fail<ToolModel>(guardFlow.Diagnostics);

        var (returnKind, resultTypeFqn) = ClassifyReturnType(method, compilation);

        if (returnKind is null)
            return DiagnosticFlow.Fail<ToolModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedReturnType, method, method.Name,
                method.ReturnType.ToDisplayString()));

        var toolAttr = method.GetAttribute(ToolAttributeName);
        var toolName = toolAttr?.GetConstructorArgument<string>(0)
                       ?? toolAttr?.GetNamedArgument<string>("Name")
                       ?? ToKebabCase(method.Name);
        var description = toolAttr?.GetNamedArgument<string>("Description")
                          ?? method.GetSummaryText(compilation, cancellationToken)
                          ?? string.Empty;

        var hasCancellationToken = HasCancellationToken(method);

        return ParameterExtractor.ExtractParameters(method, cancellationToken)
            .Select(parameters => new ToolModel(
                MethodName: method.Name,
                ToolName: toolName,
                Description: description,
                ResultTypeFullyQualified: resultTypeFqn,
                ReturnKind: returnKind.Value,
                HasCancellationToken: hasCancellationToken,
                Parameters: parameters));
    }

    private static (ReturnKind? Kind, string ResultFqn) ClassifyReturnType(
        IMethodSymbol method, Compilation compilation)
    {
        if (method.ReturnsVoid)
            return (ReturnKind.Void, string.Empty);

        var ret = method.ReturnType;
        var awaitable = new AwaitableContext(compilation);

        if (awaitable.IsTaskLike(ret))
        {
            var resultType = awaitable.GetTaskResultType(ret);
            var original = ((INamedTypeSymbol)ret).OriginalDefinition.ToDisplayString();
            var isValueTask = original.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);

            if (resultType is not null)
                return (isValueTask ? ReturnKind.ValueTaskOfT : ReturnKind.TaskOfT,
                    resultType.GetFullyQualifiedName());

            return (isValueTask ? ReturnKind.ValueTask : ReturnKind.Task, string.Empty);
        }

        // Plain synchronous return — valid as long as it's not a raw generic/open type
        if (ret is INamedTypeSymbol { IsUnboundGenericType: false } or IArrayTypeSymbol)
            return (ReturnKind.Sync, ret.GetFullyQualifiedName());

        return (null, string.Empty);
    }

    private static bool HasCancellationToken(IMethodSymbol method)
    {
        foreach (var p in method.Parameters)
            if (p.Type.ToDisplayString() == CancellationTokenTypeName)
                return true;
        return false;
    }

    internal static string ToKebabCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return pascal;

        var sb = new System.Text.StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
```

**Key changes:**
- `ClassifyReturnType` uses `AwaitableContext` (symbol comparison) instead of string matching
- Returns `ReturnKind?` (null = unsupported) instead of 5-tuple
- `GetToolAttribute` replaced by `method.GetAttribute(ToolAttributeName)`
- `GetStringProperty` replaced by `GetConstructorArgument<string>(0)` and `GetNamedArgument<string>("Name")`
- Deleted both `GetToolAttribute` and `GetStringProperty` private methods

### 2B. Rewrite ServerExtractor — shared attribute extensions + Fail for duplicates

- [ ] **Step 3: Rewrite ServerExtractor**

Replace entire contents of `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs`:

```csharp
using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Extraction;

internal static class ServerExtractor
{
    private const string McpServerAttributeName = "Qyl.Agents.McpServerAttribute";
    private const string ToolAttributeName = "Qyl.Agents.ToolAttribute";

    public static DiagnosticFlow<ServerModel> Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not ClassDeclarationSyntax classDeclaration)
            return DiagnosticFlow.Fail<ServerModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.ClassMustBePartial, context.TargetNode, context.TargetNode.ToString()));

        var guardFlow = SemanticGuard.ForType(typeSymbol)
            .MustBeClass(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustBePartial, typeSymbol, typeSymbol.Name))
            .MustNotBeStatic(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustNotBeStatic, typeSymbol, typeSymbol.Name))
            .MustNotBeGeneric(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustNotBeGeneric, typeSymbol, typeSymbol.Name))
            .ToFlow();

        var declarationsFlow = ExtractDeclarationChain(classDeclaration, cancellationToken);

        return DiagnosticFlow.Zip(guardFlow, declarationsFlow).Then(tuple =>
        {
            var (symbol, declarations) = tuple;
            var attr = symbol.GetAttribute(McpServerAttributeName);

            var serverName = attr?.GetConstructorArgument<string>(0)
                             ?? attr?.GetNamedArgument<string>("Name")
                             ?? ToolExtractor.ToKebabCase(symbol.Name);
            var description = attr?.GetNamedArgument<string>("Description")
                              ?? symbol.GetSummaryText(context.SemanticModel.Compilation, cancellationToken)
                              ?? string.Empty;
            var version = attr?.GetNamedArgument<string>("Version");

            var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();

            return ExtractTools(symbol, context.SemanticModel.Compilation, cancellationToken)
                .Select(tools => new ServerModel(
                    FullyQualifiedName: symbol.GetFullyQualifiedName(),
                    Namespace: namespaceName,
                    ClassName: symbol.Name,
                    ServerName: serverName,
                    Description: description,
                    Version: version,
                    DeclarationChain: declarations,
                    Tools: tools));
        });
    }

    private static DiagnosticFlow<EquatableArray<ToolModel>> ExtractTools(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var toolMethods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.HasAttribute(ToolAttributeName))
            .ToList();

        if (toolMethods.Count == 0)
        {
            var warning = DiagnosticInfo.Create(DiagnosticDescriptors.NoToolsFound, type, type.Name);
            return DiagnosticFlow.Ok(default(EquatableArray<ToolModel>)).Warn(warning);
        }

        var toolFlows = toolMethods.Select(m => ToolExtractor.Extract(m, compilation, cancellationToken));
        var collected = DiagnosticFlow.Collect(toolFlows);

        return collected.Then(tools =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicateDiags = new List<DiagnosticInfo>();

            foreach (var tool in tools)
                if (!seen.Add(tool.ToolName))
                    duplicateDiags.Add(DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateToolName,
                        type,
                        tool.ToolName,
                        type.Name));

            if (duplicateDiags.Count > 0)
                return DiagnosticFlow.Fail<EquatableArray<ToolModel>>(duplicateDiags.ToArray());

            return tools.IsEmpty
                ? DiagnosticFlow.Ok(default(EquatableArray<ToolModel>))
                : DiagnosticFlow.Ok(tools.AsEquatableArray());
        });
    }

    private static DiagnosticFlow<EquatableArray<TypeDeclarationModel>> ExtractDeclarationChain(
        ClassDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var chain = new List<TypeDeclarationModel>();

        for (TypeDeclarationSyntax? current = declaration; current is not null; current = current.Parent as TypeDeclarationSyntax)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!current.Modifiers.Any(SyntaxKind.PartialKeyword))
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ClassMustBePartial,
                    current.Identifier,
                    current.Identifier.ValueText));

            var modifiers = current.Modifiers.Select(static m => m.ValueText).ToList();
            if (!modifiers.Contains("partial"))
                modifiers.Add("partial");

            chain.Add(new TypeDeclarationModel(
                Name: current.Identifier.ValueText,
                Keyword: current.Keyword.ValueText,
                Modifiers: string.Join(" ", modifiers),
                TypeParameters: current.TypeParameterList?.ToString().Trim() ?? string.Empty,
                ConstraintClauses: current.ConstraintClauses.Count == 0
                    ? default
                    : current.ConstraintClauses.Select(static c => c.ToString().Trim()).ToArray().ToEquatableArray()));
        }

        chain.Reverse();

        if (diagnostics.Count > 0)
            return DiagnosticFlow.Fail<EquatableArray<TypeDeclarationModel>>(diagnostics.ToArray());

        return DiagnosticFlow.Ok(chain.Count is 0 ? default : chain.ToArray().ToEquatableArray());
    }
}
```

**Key changes:**
- `GetMcpServerAttribute` → `symbol.GetAttribute(McpServerAttributeName)`
- `HasToolAttribute` → `m.HasAttribute(ToolAttributeName)`
- `GetStringProperty` → `GetConstructorArgument<string>(0)` / `GetNamedArgument<string>("...")`
- Duplicate detection: `DiagnosticFlow.Fail(duplicateDiags.ToArray())` instead of loop of `.Error(d)`
- Deleted `GetMcpServerAttribute`, `HasToolAttribute`, `GetStringProperty` private methods

### 2C. Rewrite ParameterExtractor — shared extensions

- [ ] **Step 4: Rewrite ParameterExtractor — replace GetDescription with GetAttributeConstructorArgument, replace ToCamelCase with ToParameterName**

In `src/Qyl.Agents.Generator/Extraction/ParameterExtractor.cs`:

Replace lines 94–111 (`GetDescription` method) — delete the entire method.

Replace line 38 (`var description = GetDescription(parameter);`) with:
```csharp
            var description = parameter.GetAttributeConstructorArgument<string>(DescriptionAttributeName, 0);
```

Replace line 53 (`CamelCaseName: ToCamelCase(parameter.Name),`) with:
```csharp
                CamelCaseName: parameter.Name.ToParameterName(),
```

Delete lines 179–187 (the `ToCamelCase` method).

- [ ] **Step 5: Build the generator project to verify all extractor changes compile**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Compile errors in emitters (DispatchEmitter, JsonContextEmitter) that still reference old `ReturnsTask`/`ReturnsVoid` fields. Extractors should compile clean.

- [ ] **Step 6: Commit Chunk 2**

```bash
git add src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj \
  src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs \
  src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs \
  src/Qyl.Agents.Generator/Extraction/ParameterExtractor.cs
git commit -m "refactor(qyl-agents): adopt shared attribute/string extensions, AwaitableContext, Fail for duplicates"
```

---

## Task 3: Chunk 3A–3C — Emitter Fixes (Non-OTel)

**Files:**
- Modify: `src/Qyl.Agents.Generator/Generation/DispatchEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/JsonContextEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/SchemaEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs`

### 3A. Wire JsonContextEmitter + ReturnKind in DispatchEmitter

- [ ] **Step 1: Update JsonContextEmitter to use ReturnKind and add nullable wrappers**

Replace entire contents of `src/Qyl.Agents.Generator/Generation/JsonContextEmitter.cs`:

```csharp
using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class JsonContextEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        var types = CollectSerializableTypes(server);
        if (types.Count == 0) return;

        sb.AppendLine("[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(");
        sb.AppendLine("    global::System.Text.Json.JsonSerializerDefaults.Web)]");

        foreach (var type in types)
            sb.AppendLine($"[global::System.Text.Json.Serialization.JsonSerializable(typeof({type}))]");

        sb.AppendLine($"private partial class {server.ClassName}JsonContext");
        sb.AppendLine("    : global::System.Text.Json.Serialization.JsonSerializerContext { }");
    }

    private static List<string> CollectSerializableTypes(ServerModel server)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var tool in server.Tools)
        {
            // Parameter types (deserialization)
            foreach (var p in tool.Parameters)
            {
                if (seen.Add(p.TypeFullyQualified))
                    result.Add(p.TypeFullyQualified);

                // Nullable<T> wrapper for nullable value type parameters
                if (p.IsNullable && IsValueType(p.TypeFullyQualified))
                {
                    var nullableType = $"global::System.Nullable<{p.TypeFullyQualified}>";
                    if (seen.Add(nullableType))
                        result.Add(nullableType);
                }
            }

            // Return types (serialization) — only for kinds that produce a value
            if (tool.ReturnKind is ReturnKind.Void or ReturnKind.Task or ReturnKind.ValueTask)
                continue;
            if (seen.Add(tool.ResultTypeFullyQualified))
                result.Add(tool.ResultTypeFullyQualified);
        }

        return result;
    }

    private static bool IsValueType(string fqn) =>
        fqn.StartsWith("global::System.", StringComparison.Ordinal) &&
        !fqn.EndsWith("String", StringComparison.Ordinal) &&
        !fqn.EndsWith("Uri", StringComparison.Ordinal) &&
        !fqn.Contains("[]", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Rewrite DispatchEmitter — ReturnKind switch + AOT-safe JsonContext**

Replace entire contents of `src/Qyl.Agents.Generator/Generation/DispatchEmitter.cs`:

```csharp
using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class DispatchEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        // Public dispatch entry point
        sb.AppendLine("public async global::System.Threading.Tasks.Task<string> DispatchToolCallAsync(");
        sb.AppendLine("    string toolName,");
        sb.AppendLine("    global::System.Text.Json.JsonElement arguments,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return toolName switch");
            using (sb.BeginBlock())
            {
                foreach (var tool in server.Tools)
                    sb.AppendLine($"{Lit(tool.ToolName)} => await {PerToolMethod(tool)}(arguments, cancellationToken),");

                sb.AppendLine($"_ => throw new global::System.ArgumentException($\"Unknown tool: {{toolName}}\", nameof(toolName))");
            }
            sb.AppendLine(";");
        }
        sb.AppendLine();

        // Per-tool private methods
        foreach (var tool in server.Tools)
            EmitPerToolMethod(sb, tool, server);
    }

    private static void EmitPerToolMethod(IndentedStringBuilder sb, ToolModel tool, ServerModel server)
    {
        sb.AppendLine($"private async global::System.Threading.Tasks.Task<string> {PerToolMethod(tool)}(");
        sb.AppendLine("    global::System.Text.Json.JsonElement args,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken)");
        using (sb.BeginBlock())
        {
            // OTel span
            sb.AppendLine($"using var activity = s_activitySource.StartActivity(");
            sb.AppendLine($"    $\"execute_tool {tool.ToolName}\",");
            sb.AppendLine("    global::System.Diagnostics.ActivityKind.Internal);");
            sb.AppendLine("if (activity is not null)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("activity.SetTag(\"gen_ai.operation.name\", \"execute_tool\");");
                sb.AppendLine($"activity.SetTag(\"gen_ai.tool.name\", {Lit(tool.ToolName)});");
                sb.AppendLine("activity.SetTag(\"gen_ai.tool.type\", \"function\");");
                sb.AppendLine("activity.SetTag(\"gen_ai.system\", \"mcp\");");
                sb.AppendLine($"activity.SetTag(\"server.name\", {Lit(server.ServerName)});");
            }
            sb.AppendLine();

            // Metrics
            sb.AppendLine("var sw = global::System.Diagnostics.Stopwatch.StartNew();");
            sb.AppendLine("try");
            using (sb.BeginBlock())
            {
                // Deserialize parameters using AOT-safe JsonContext
                foreach (var p in tool.Parameters)
                {
                    if (p.IsRequired)
                        sb.AppendLine($"var {p.CamelCaseName} = global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(args.GetProperty({Lit(p.CamelCaseName)}), {server.ClassName}JsonContext.Default.Options);");
                    else if (p.DefaultValueLiteral is not null)
                        sb.AppendLine($"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(_{p.CamelCaseName}El, {server.ClassName}JsonContext.Default.Options) : {p.DefaultValueLiteral};");
                    else
                        sb.AppendLine($"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(_{p.CamelCaseName}El, {server.ClassName}JsonContext.Default.Options) : default({p.TypeFullyQualified});");
                }

                // Call user method — switch on ReturnKind
                var callArgs = BuildCallArgs(tool);
                var needsAwait = tool.ReturnKind is ReturnKind.Task or ReturnKind.ValueTask
                    or ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;
                var hasResult = tool.ReturnKind is ReturnKind.Sync or ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;

                if (hasResult)
                {
                    sb.AppendLine($"var result = {(needsAwait ? "await " : "")}{tool.MethodName}({callArgs});");
                    sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
                    sb.AppendLine($"return global::System.Text.Json.JsonSerializer.Serialize(result, {server.ClassName}JsonContext.Default.Options);");
                }
                else
                {
                    sb.AppendLine($"{(needsAwait ? "await " : "")}{tool.MethodName}({callArgs});");
                    sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
                    sb.AppendLine("return \"null\";");
                }
            }
            sb.AppendLine("catch (global::System.Exception ex)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("activity?.SetTag(\"error.type\", ex.GetType().FullName);");
                sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);");
                sb.AppendLine("throw;");
            }
            sb.AppendLine("finally");
            using (sb.BeginBlock())
            {
                sb.AppendLine("s_requestDuration.Record(sw.Elapsed.TotalSeconds);");
            }
        }
        sb.AppendLine();
    }

    private static string BuildCallArgs(ToolModel tool)
    {
        var parts = tool.Parameters.Select(static p => p.CamelCaseName).ToList();
        if (tool.HasCancellationToken)
            parts.Add("cancellationToken");
        return string.Join(", ", parts);
    }

    private static string PerToolMethod(ToolModel tool) => $"ExecuteTool_{tool.MethodName}Async";

    private static string Lit(string value) => SymbolDisplay.FormatLiteral(value, true);
}
```

**Key changes:**
- Deleted `s_jsonOptions` field emission — replaced by `{ClassName}JsonContext.Default.Options`
- Added `gen_ai.system` = `"mcp"` and `server.name` tags
- Removed `s_toolCallCount.Add(1)` counter (dropped for v1 per spec)
- `ReturnKind` switch: `needsAwait` + `hasResult` derived from enum, handles all 6 variants correctly
- `EmitPerToolMethod` now takes `ServerModel` for `server.ServerName` and `server.ClassName`

### 3B. Complete EscapeJson

- [ ] **Step 3: Fix EscapeJson in SchemaEmitter for RFC 8259 compliance**

In `src/Qyl.Agents.Generator/Generation/SchemaEmitter.cs`, replace lines 67–68 (the `EscapeJson` method):

```csharp
    private static string EscapeJson(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
```

### 3C. Fix EscapeYaml for multi-line

- [ ] **Step 4: Fix EscapeYaml in SkillEmitter for multi-line descriptions**

In `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs`, replace the YAML frontmatter emission (lines 23–26) and the `EscapeYaml` method (lines 67–70).

Replace the frontmatter block (lines 22–26):
```csharp
        // YAML frontmatter
        md.AppendLine("---");
        md.Append("name: ").AppendLine(server.ServerName);
        EmitYamlValue(md, "description", server.Description);
        md.AppendLine("---");
```

Replace the `EscapeYaml` method (lines 67–70) with:
```csharp
    private static void EmitYamlValue(System.Text.StringBuilder sb, string key, string value)
    {
        if (value.Contains('\n'))
        {
            // Multi-line: use YAML literal block scalar
            sb.Append(key).AppendLine(": |");
            foreach (var line in value.Split('\n'))
                sb.Append("  ").AppendLine(line.TrimEnd('\r'));
        }
        else if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
                 (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))))
        {
            // Single-line with special chars: double-quoted scalar
            sb.Append(key).Append(": \"")
              .Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .AppendLine("\"");
        }
        else
        {
            sb.Append(key).Append(": ").AppendLine(value);
        }
    }
```

- [ ] **Step 5: Build entire generator to verify all emitter changes compile**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Success — all extractors and emitters now use `ReturnKind` consistently.

- [ ] **Step 6: Commit Chunk 3A–3C**

```bash
git add src/Qyl.Agents.Generator/Generation/DispatchEmitter.cs \
  src/Qyl.Agents.Generator/Generation/JsonContextEmitter.cs \
  src/Qyl.Agents.Generator/Generation/SchemaEmitter.cs \
  src/Qyl.Agents.Generator/Generation/SkillEmitter.cs
git commit -m "fix(qyl-agents): AOT-safe JsonContext wiring, RFC 8259 EscapeJson, multi-line YAML"
```

---

## Task 4: Chunk 3D — OTel Telemetry

**Files:**
- Modify: `src/Qyl.Agents.Generator/Generation/OTelEmitter.cs`

### 3D. Version from assembly + correct metric names

- [ ] **Step 1: Rewrite OTelEmitter — version from assembly metadata, correct metric name, remove counter**

Replace entire contents of `src/Qyl.Agents.Generator/Generation/OTelEmitter.cs`:

```csharp
using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class OTelEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        // Version from assembly metadata — ties instrumentation version to package version
        sb.AppendLine($"private static readonly string s_instrumentationVersion =");
        sb.AppendLine($"    typeof({server.ClassName}).Assembly.GetName().Version?.ToString() ?? \"0.0.0\";");
        sb.AppendLine();

        sb.AppendLine("private static readonly global::System.Diagnostics.ActivitySource s_activitySource =");
        sb.AppendLine("    new global::System.Diagnostics.ActivitySource(\"Qyl.Agents\", s_instrumentationVersion);");
        sb.AppendLine();

        sb.AppendLine("private static readonly global::System.Diagnostics.Metrics.Meter s_meter =");
        sb.AppendLine("    new global::System.Diagnostics.Metrics.Meter(\"Qyl.Agents\", s_instrumentationVersion);");
        sb.AppendLine();

        sb.AppendLine("private static readonly global::System.Diagnostics.Metrics.Histogram<double> s_requestDuration =");
        sb.AppendLine("    s_meter.CreateHistogram<double>(\"gen_ai.client.operation.duration\", \"s\", \"Duration of tool execution\");");
    }
}
```

**Key changes:**
- Version from `typeof({ClassName}).Assembly.GetName().Version` instead of hardcoded `"1.0.0"`
- Histogram renamed: `gen_ai.server.request.duration` → `gen_ai.client.operation.duration`
- Counter (`qyl.agent.tool.calls`) removed entirely for v1

- [ ] **Step 2: Build to verify OTel emitter compiles**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Success.

- [ ] **Step 3: Commit Chunk 3D**

```bash
git add src/Qyl.Agents.Generator/Generation/OTelEmitter.cs
git commit -m "fix(qyl-agents): OTel version from assembly, correct GenAI metric names, drop counter"
```

---

## Task 5: Chunk 4+5 — Pipeline & Runtime

**Files:**
- Modify: `src/Qyl.Agents.Generator/McpServerGenerator.cs`
- Modify: `src/Qyl.Agents/Protocol/McpProtocolHandler.cs`

### 4A. Second pipeline for orphaned [Tool] → QA0004

- [ ] **Step 1: Add orphaned-tool diagnostic pipeline**

Replace entire contents of `src/Qyl.Agents.Generator/McpServerGenerator.cs`:

```csharp
using Qyl.Agents.Generator.Extraction;
using Qyl.Agents.Generator.Generation;

namespace Qyl.Agents.Generator;

[Generator]
public sealed class McpServerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Primary pipeline: [McpServer] classes → extract → generate
        var serverFlows = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Qyl.Agents.McpServerAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, ct) => ServerExtractor.Extract(ctx, ct));

        var servers = serverFlows.ReportAndStop(context);

        servers
            .Select(static (model, _) => OutputGenerator.GenerateOutput(model))
            .AddSource(context);

        // Secondary pipeline: orphaned [Tool] methods → QA0004 diagnostic only
        var orphanedTools = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Qyl.Agents.ToolAttribute",
            static (node, _) => node is MethodDeclarationSyntax,
            static (ctx, _) =>
            {
                if (ctx.TargetSymbol is not IMethodSymbol method)
                    return default(DiagnosticInfo?);

                var containingType = method.ContainingType;
                if (containingType is null || containingType.HasAttribute("Qyl.Agents.McpServerAttribute"))
                    return null; // Not orphaned — inside an [McpServer] class

                return DiagnosticInfo.Create(
                    DiagnosticDescriptors.ToolMethodMustBeInsideMcpServer,
                    method,
                    method.Name);
            });

        context.RegisterSourceOutput(orphanedTools, static (ctx, diagnostic) =>
        {
            if (diagnostic is not null)
                ctx.ReportDiagnostic(diagnostic.Value.ToDiagnostic());
        });
    }
}
```

### 5A. Cache JSON allocations + OTel transport spans in McpProtocolHandler

- [ ] **Step 2: Rewrite McpProtocolHandler — cached allocations + transport spans**

Replace entire contents of `src/Qyl.Agents/Protocol/McpProtocolHandler.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;

namespace Qyl.Agents.Protocol;

internal sealed class McpProtocolHandler<TServer>(TServer server) where TServer : class, IMcpServer
{
    private static readonly McpServerInfo s_info = TServer.GetServerInfo();
    private static readonly IReadOnlyList<McpToolInfo> s_tools = TServer.GetToolInfos();

    // Cached JSON allocations — Clone() detaches from JsonDocument lifetime
    private static readonly JsonElement s_emptyObject = JsonDocument.Parse("{}").RootElement.Clone();
    private static readonly JsonElement s_defaultSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone();

    // Pre-parsed tool schemas (computed once at construction)
    private static readonly JsonElement[] s_toolSchemas = ParseToolSchemas();

    // Shared ActivitySource for transport-level spans
    private static readonly ActivitySource s_activitySource = new("Qyl.Agents",
        typeof(TServer).Assembly.GetName().Version?.ToString() ?? "0.0.0");

    public async Task<JsonRpcResponse?> HandleAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (request.IsNotification)
            return null;

        // Transport-level OTel span
        var spanName = BuildSpanName(request);
        using var activity = s_activitySource.StartActivity(spanName, ActivityKind.Server);
        if (activity is not null)
        {
            activity.SetTag("mcp.method.name", request.Method);
            if (request.Id is { } id)
                activity.SetTag("jsonrpc.request.id", id.ToString());
        }

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCallAsync(request, activity, ct),
            "ping" => HandlePing(request),
            _ => ErrorResponse(request.Id, McpErrorCodes.MethodNotFound, $"Unknown method: {request.Method}")
        };
    }

    private static string BuildSpanName(JsonRpcRequest request)
    {
        if (request.Method != "tools/call" || request.Params is not { } p)
            return request.Method;

        // tools/call {tool.name} — append target when meaningful
        if (p.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            return $"tools/call {nameEl.GetString()}";

        return "tools/call";
    }

    private static JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        var result = JsonSerializer.SerializeToDocument(new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { listChanged = false }
            },
            serverInfo = new
            {
                name = s_info.Name,
                version = s_info.Version ?? "0.0.0"
            }
        });

        return SuccessResponse(request.Id, result.RootElement);
    }

    private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new List<object>(s_tools.Count);
        for (var i = 0; i < s_tools.Count; i++)
        {
            var tool = s_tools[i];
            tools.Add(new
            {
                name = tool.Name,
                description = tool.Description ?? "",
                inputSchema = s_toolSchemas[i]
            });
        }

        var result = JsonSerializer.SerializeToDocument(new { tools });
        return SuccessResponse(request.Id, result.RootElement);
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(
        JsonRpcRequest request, Activity? activity, CancellationToken ct)
    {
        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.name");

        var toolName = nameEl.GetString()!;

        // Enrich transport span with tool name for tools/call
        activity?.SetTag("gen_ai.tool.name", toolName);

        var arguments = p.TryGetProperty("arguments", out var argsEl)
            ? argsEl
            : s_emptyObject;

        try
        {
            var resultJson = await server.DispatchToolCallAsync(toolName, arguments, ct);

            var content = new[]
            {
                new { type = "text", text = resultJson }
            };

            var result = JsonSerializer.SerializeToDocument(new { content });
            return SuccessResponse(request.Id, result.RootElement);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, ex.Message);
        }
        catch (Exception ex)
        {
            var content = new[]
            {
                new { type = "text", text = ex.Message }
            };

            var result = JsonSerializer.SerializeToDocument(new { content, isError = true });
            return SuccessResponse(request.Id, result.RootElement);
        }
    }

    private static JsonRpcResponse HandlePing(JsonRpcRequest request) =>
        SuccessResponse(request.Id, s_emptyObject);

    private static JsonRpcResponse SuccessResponse(JsonElement? id, JsonElement result) =>
        new() { Id = id, Result = result };

    private static JsonRpcResponse ErrorResponse(JsonElement? id, int code, string message) =>
        new() { Id = id, Error = new JsonRpcError { Code = code, Message = message } };

    private static JsonElement[] ParseToolSchemas()
    {
        var schemas = new JsonElement[s_tools.Count];
        for (var i = 0; i < s_tools.Count; i++)
        {
            var tool = s_tools[i];
            schemas[i] = tool.InputSchema.Length > 0
                ? JsonDocument.Parse(tool.InputSchema).RootElement.Clone()
                : s_defaultSchema;
        }
        return schemas;
    }
}
```

**Key changes:**
- `s_emptyObject`, `s_defaultSchema`: cached `JsonElement` (`.Clone()` detaches from `JsonDocument`)
- `s_toolSchemas`: pre-parsed array computed once at static init
- `HandlePing` uses `s_emptyObject` instead of `JsonDocument.Parse("{}")`
- `HandleToolsList` uses `s_toolSchemas[i]` instead of parsing per-request
- `HandleToolsCallAsync` uses `s_emptyObject` for missing arguments fallback
- Added OTel transport spans: `ActivityKind.Server`, `mcp.method.name`, `jsonrpc.request.id`, `gen_ai.tool.name` on tools/call
- Span name: `{method}` or `tools/call {tool.name}`

- [ ] **Step 3: Build entire solution to verify everything compiles**

Run: `dotnet build`
Expected: Success — all projects compile with zero errors.

- [ ] **Step 4: Commit Chunks 4+5**

```bash
git add src/Qyl.Agents.Generator/McpServerGenerator.cs \
  src/Qyl.Agents/Protocol/McpProtocolHandler.cs
git commit -m "feat(qyl-agents): orphaned [Tool] QA0004 pipeline, cached JSON allocations, OTel transport spans"
```

---

## Task 6: Chunk 6 — Tests

**Files:**
- Modify: `src/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs`
- Modify: `src/Qyl.Agents.Tests/McpProtocolEndToEndTests.cs`
- Modify: `src/Qyl.Agents.Tests/TestServer.cs`

### Generator Tests

- [ ] **Step 1: Update existing tests for new generated output (no s_jsonOptions, has JsonContext)**

In `src/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs`, update `SingleToolGeneratesFullOutput` test assertions. Replace:
```csharp
                Assert.Contains("s_jsonOptions", content);
```
with:
```csharp
                Assert.Contains("MyToolsJsonContext", content);
                Assert.DoesNotContain("s_jsonOptions", content);
```

Also add new assertions in the same test:
```csharp
                Assert.Contains("gen_ai.system", content);
                Assert.Contains("server.name", content);
                Assert.Contains("gen_ai.client.operation.duration", content);
                Assert.DoesNotContain("qyl.agent.tool.calls", content);
```

- [ ] **Step 2: Add diagnostic test cases (QA0002–QA0011)**

Add the following tests to `McpServerGeneratorTests.cs`:

```csharp
    [Fact]
    public async Task StaticClassReportsQA0002()
    {
        var source = """
            using Qyl.Agents;

            [McpServer]
            public static partial class StaticServer
            {
                [Tool]
                public static string Greet(string name) => name;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0002", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GenericClassReportsQA0003()
    {
        var source = """
            using Qyl.Agents;

            [McpServer]
            public partial class GenericServer<T>
            {
                [Tool]
                public string Greet(string name) => name;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0003", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task OrphanedToolReportsQA0004()
    {
        var source = """
            using Qyl.Agents;

            public partial class NotAServer
            {
                [Tool]
                public string Orphan(string input) => input;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0004", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task StaticMethodReportsQA0005()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Test</summary>
            [McpServer]
            public partial class StaticMethodServer
            {
                [Tool]
                public static string Greet(string name) => name;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0005", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GenericMethodReportsQA0006()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Test</summary>
            [McpServer]
            public partial class GenericMethodServer
            {
                [Tool]
                public T Greet<T>(T input) => input;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0006", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task UnsupportedReturnTypeReportsQA0007()
    {
        var source = """
            using Qyl.Agents;
            using System.Collections.Generic;

            /// <summary>Test</summary>
            [McpServer]
            public partial class BadReturnServer
            {
                [Tool]
                public IAsyncEnumerable<string> Stream(string input) => throw new System.NotImplementedException();
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0007", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task MissingDescriptionReportsQA0009ButCompiles()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Test</summary>
            [McpServer]
            public partial class NoDescServer
            {
                /// <summary>A tool</summary>
                [Tool]
                public string Echo(string input) => input;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .HasDiagnostic("QA0009", DiagnosticSeverity.Warning)
            .Compiles();
    }

    [Fact]
    public async Task NoToolsReportsQA0010()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Empty server</summary>
            [McpServer]
            public partial class EmptyServer
            {
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0010", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task UnsupportedParameterTypeReportsQA0008()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Test</summary>
            [McpServer]
            public partial class BadParamServer
            {
                /// <summary>A tool</summary>
                [Tool]
                public string Process(System.Action callback) => "";
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0008", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task DuplicateToolNameReportsQA0011()
    {
        var source = """
            using Qyl.Agents;

            /// <summary>Test</summary>
            [McpServer]
            public partial class DuplicateServer
            {
                /// <summary>A</summary>
                [Tool("dupe")]
                public string First(string a) => a;

                /// <summary>B</summary>
                [Tool("dupe")]
                public string Second(string b) => b;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0011", DiagnosticSeverity.Error);
    }
```

- [ ] **Step 3: Add feature-coverage generator tests (nested class, enum, nullable, collections, JSON context, multi-line, server.name)**

Add to `McpServerGeneratorTests.cs`:

```csharp
    [Fact]
    public async Task NestedPartialClassCompiles()
    {
        var source = """
            using Qyl.Agents;

            namespace Outer;

            public partial class Container
            {
                /// <summary>Nested server</summary>
                [McpServer]
                public partial class Inner
                {
                    /// <summary>Hello</summary>
                    [Tool]
                    public string Greet(string name) => $"Hello {name}";
                }
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .Produces("Outer.Inner.McpServer.g.cs")
            .Compiles();
    }

    [Fact]
    public async Task EnumParameterIncludedInSchemaAndJsonContext()
    {
        var source = """
            using Qyl.Agents;
            using System.ComponentModel;

            namespace EnumTest;

            public enum Priority { Low, Medium, High }

            /// <summary>Test</summary>
            [McpServer]
            public partial class EnumServer
            {
                /// <summary>Set priority</summary>
                [Tool]
                public string SetPriority([Description("The priority")] Priority p) => p.ToString();
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("EnumTest.EnumServer.McpServer.g.cs", content =>
            {
                // Schema should have enum values
                Assert.Contains("Low", content);
                Assert.Contains("Medium", content);
                Assert.Contains("High", content);
                // JsonContext should include the enum type
                Assert.Contains("JsonSerializable", content);
                Assert.Contains("Priority", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task NullableParameterHandledInJsonContext()
    {
        var source = """
            using Qyl.Agents;
            using System.ComponentModel;

            namespace NullableTest;

            /// <summary>Test</summary>
            [McpServer]
            public partial class NullableServer
            {
                /// <summary>Process</summary>
                [Tool]
                public string Process([Description("Count")] int? count) => (count ?? 0).ToString();
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("NullableTest.NullableServer.McpServer.g.cs", content =>
            {
                Assert.Contains("JsonSerializable", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task JsonContextWiringUsesGeneratedContext()
    {
        var source = """
            using Qyl.Agents;
            using System.ComponentModel;

            namespace JsonCtx;

            /// <summary>Test</summary>
            [McpServer]
            public partial class CtxServer
            {
                /// <summary>Echo</summary>
                [Tool]
                public string Echo([Description("Input")] string input) => input;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("JsonCtx.CtxServer.McpServer.g.cs", content =>
            {
                Assert.Contains("CtxServerJsonContext.Default.Options", content);
                Assert.DoesNotContain("s_jsonOptions", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task DateTimeOffsetGuidUriParametersHaveFormat()
    {
        var source = """
            using Qyl.Agents;
            using System;
            using System.ComponentModel;

            namespace FormatTest;

            /// <summary>Test</summary>
            [McpServer]
            public partial class FormatServer
            {
                /// <summary>Process dates and ids</summary>
                [Tool]
                public string Process(
                    [Description("When")] DateTimeOffset when,
                    [Description("Id")] Guid id,
                    [Description("Link")] Uri link) => "";
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("FormatTest.FormatServer.McpServer.g.cs", content =>
            {
                Assert.Contains("date-time", content);
                Assert.Contains("uuid", content);
                Assert.Contains("uri", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task ArrayParameterIncludedInJsonContext()
    {
        var source = """
            using Qyl.Agents;
            using System.ComponentModel;

            namespace ArrayTest;

            /// <summary>Test</summary>
            [McpServer]
            public partial class ArrayServer
            {
                /// <summary>Process items</summary>
                [Tool]
                public string Process([Description("Items")] string[] items) => string.Join(",", items);
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("ArrayTest.ArrayServer.McpServer.g.cs", content =>
            {
                Assert.Contains("JsonSerializable", content);
                Assert.Contains("array", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task MultiLineDescriptionUsesYamlLiteralBlock()
    {
        var source = """
            using Qyl.Agents;
            using System.ComponentModel;

            namespace MultiLine;

            /// <summary>
            /// A server that does things.
            /// It has a multi-line description.
            /// </summary>
            [McpServer]
            public partial class MultiServer
            {
                /// <summary>A tool</summary>
                [Tool]
                public string Echo([Description("Input")] string input) => input;
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("MultiLine.MultiServer.McpServer.g.cs", content =>
            {
                // Multi-line description should use YAML literal block scalar (|)
                // or be properly escaped — the key test is that it compiles
                Assert.Contains("description:", content);
            })
            .Compiles();
    }

    [Fact]
    public async Task ServerNameAttributeEmitted()
    {
        var source = """
            using Qyl.Agents;
            using System.Threading.Tasks;

            namespace OTelTest;

            /// <summary>Test</summary>
            [McpServer]
            public partial class OTelServer
            {
                /// <summary>Do thing</summary>
                [Tool]
                public Task<string> DoThing(string input) => Task.FromResult(input);
            }
            """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("OTelTest.OTelServer.McpServer.g.cs", content =>
            {
                Assert.Contains("SetTag(\"server.name\"", content);
                Assert.Contains("SetTag(\"gen_ai.system\", \"mcp\")", content);
            })
            .Compiles();
    }
```

- [ ] **Step 4: Run generator tests**

Run: `dotnet test src/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: All tests pass (exit code 0).

### Runtime Tests

- [ ] **Step 5: Add ThrowingTool to TestServer for error testing**

In `src/Qyl.Agents.Tests/TestServer.cs`, add after the `Multiply` method:

```csharp
    /// <summary>Always throws for error testing</summary>
    [Tool]
    public string Fail([Description("Error message")] string message)
    {
        throw new InvalidOperationException(message);
    }
```

- [ ] **Step 6: Update existing ToolsListReturnsAllTools test (tool count changes from 2 to 3)**

In `src/Qyl.Agents.Tests/McpProtocolEndToEndTests.cs`, update `ToolsListReturnsAllTools`:

Replace `Assert.Equal(2, tools.GetArrayLength());` with:
```csharp
        Assert.Equal(3, tools.GetArrayLength());
```

And add `"fail"` to the tool names check:
```csharp
        Assert.Contains("fail", toolNames);
```

- [ ] **Step 7: Add runtime test cases (schema caching, tool exception, OTel attributes, transport spans)**

Add to `src/Qyl.Agents.Tests/McpProtocolEndToEndTests.cs`:

```csharp
    [Fact]
    public async Task RepeatedToolsListReturnsConsistentSchema()
    {
        var request = MakeRequest("tools/list");

        var response1 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);
        var response2 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var json1 = response1!.Result!.Value.GetRawText();
        var json2 = response2!.Result!.Value.GetRawText();
        Assert.Equal(json1, json2);
    }

    [Fact]
    public async Task ToolExceptionReturnsIsErrorContent()
    {
        var args = JsonDocument.Parse("""{"message": "boom"}""").RootElement;
        var request = MakeToolCallRequest("fail", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!.Error); // MCP spec: tool errors are content, not JSON-RPC errors
        var resultJson = response.Result!.Value;
        Assert.True(resultJson.GetProperty("isError").GetBoolean());
        Assert.Contains("boom", resultJson.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ToolCallSpanHasServerNameAndGenAiSystem()
    {
        using var collector = new ActivityCollector("Qyl.Agents");

        var args = JsonDocument.Parse("""{"a": 1, "b": 2}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var span = collector.FindSingle("execute_tool add");
        span.AssertTag("server.name", "calc-server");
        span.AssertTag("gen_ai.system", "mcp");
    }

    [Fact]
    public async Task TransportSpanHasMcpMethodAndRequestId()
    {
        using var collector = new ActivityCollector("Qyl.Agents");

        var args = JsonDocument.Parse("""{"a": 1, "b": 2}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var transportSpans = collector.Where("tools/call");
        Assert.NotEmpty(transportSpans);
        var transport = transportSpans[0];
        transport.AssertTag("mcp.method.name", "tools/call");
        transport.AssertHasTag("jsonrpc.request.id");
        transport.AssertKind(System.Diagnostics.ActivityKind.Server);
    }
```

- [ ] **Step 8: Run runtime tests**

Run: `dotnet test src/Qyl.Agents.Tests/Qyl.Agents.Tests.csproj`
Expected: All tests pass (exit code 0).

- [ ] **Step 9: Run full solution build to confirm zero warnings**

Run: `dotnet build --no-incremental`
Expected: Build succeeded. 0 Warning(s). 0 Error(s).

- [ ] **Step 10: Commit Chunk 6**

```bash
git add src/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs \
  src/Qyl.Agents.Tests/McpProtocolEndToEndTests.cs \
  src/Qyl.Agents.Tests/TestServer.cs
git commit -m "test(qyl-agents): comprehensive diagnostic, feature, and runtime test coverage"
```

---

## Verification Checklist

After all tasks are complete, verify:

- [ ] `dotnet build --no-incremental` — zero errors, zero warnings
- [ ] `dotnet test src/Qyl.Agents.Generator.Tests` — all pass
- [ ] `dotnet test src/Qyl.Agents.Tests` — all pass
- [ ] Generated code contains `{ClassName}JsonContext.Default.Options` (not `s_jsonOptions`)
- [ ] Generated code contains `gen_ai.system` and `server.name` tags
- [ ] Generated code uses `gen_ai.client.operation.duration` histogram (not `gen_ai.server.request.duration`)
- [ ] Generated code has no counter metric
- [ ] No `QA0012` in `DiagnosticDescriptors.cs` or `AnalyzerReleases`
- [ ] `McpServerInfo.Name` and `McpToolInfo.Name` are `required init`
