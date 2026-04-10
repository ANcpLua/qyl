# Generator Catalog — Every Source Generator Across All Repos

> 5 Roslyn source generators across 4 repos. Every one is `IIncrementalGenerator` with `ForAttributeWithMetadataName` and value-equatable models.

---

## Architecture: How Generators Work

Every generator follows the same Roslyn incremental generator pattern:

```
ForAttributeWithMetadataName(attributeFQN)   ← fast registered lookup
    → Predicate(SyntaxNode)                   ← cheap syntactic filter
    → Transform(GeneratorAttributeSyntaxContext) ← semantic extraction → Model
    → Collect / Combine                       ← aggregate models
    → RegisterSourceOutput                    ← emit .g.cs
```

Key constraints:
- **Models must be value-equatable** — Roslyn skips re-generation when models haven't changed (incremental caching).
- **No ISymbol in models** — symbols are transient; storing them breaks caching.
- **netstandard2.0 target** — generators load into the compiler, which requires netstandard2.0.
- **ANcpLua.Roslyn.Utilities.Sources** — shared utilities embedded as source (generators can't load NuGet DLLs at runtime).

---

## 1. ServiceDefaultsSourceGenerator

**Location:** `qyl/src/qyl.instrumentation.generators/ServiceDefaultsSourceGenerator.cs`
**Trigger:** Multiple — intercepts `WebApplicationBuilder.Build()` and discovers call sites for traced methods, DB commands, agent calls, GenAI calls, meters, and MCP tool types.
**Repo:** qyl

### Pipelines (7 concurrent)

This is the "mega-generator" — it runs 7 independent incremental pipelines, each gated by MSBuild property toggles and runtime availability checks.

| Pipeline | Analyzer | Model | Emitter | Output File | MSBuild Toggle |
|----------|----------|-------|---------|-------------|----------------|
| **Builder Interception** | `CouldBeBuildInvocation` + `ExtractBuilderCallSite` | `BuilderCallSite` | inline `EmitBuilderInterceptors` | `Intercepts.g.cs` | always |
| **Traced Methods** | `TracedCallSiteAnalyzer` | `TracedCallSite` | `TracedInterceptorEmitter` | `TracedIntercepts.g.cs` | `QylTraced` |
| **Database Commands** | `DbCallSiteAnalyzer` | `DbCallSite` | `DbInterceptorEmitter` | `DbIntercepts.g.cs` | `QylDatabase` |
| **Agent Calls** | `AgentCallSiteAnalyzer` | `AgentCallSite` | `AgentInterceptorEmitter` | `AgentIntercepts.g.cs` | `QylAgent` |
| **Metrics** | `MeterAnalyzer` | `MeterDefinition` | `MeterEmitter` | `MeterImplementations.g.cs` | `QylMeter` |
| **Tool Manifest** | `ToolManifestAnalyzer` | `ToolTypeEntry` | `ToolManifestEmitter` | `QylToolManifest.g.cs` | always |
| **Capabilities** | (from GenAI + Agent pipelines) | `GenAiCallSite` + `AgentCallSite` | `CapabilityEmitter` | `QylCapabilities.g.cs` | always |

### Runtime Gate

All pipelines (except Tool Manifest and Capabilities) are gated by:
```csharp
compilation.GetTypeByMetadataName("Qyl.Instrumentation.QylServiceDefaults") is not null
```
If the `qyl.instrumentation` runtime package isn't referenced, no code is generated.

### Builder Interception Pipeline (detail)

This is the most unusual pipeline. It uses C# interceptors to wrap `builder.Build()`:

1. **Discover** `WebApplicationBuilder.Build()` call sites
2. **Collect** all ActivitySource names, Meter names, and capability attributes (from current assembly + referenced assemblies via `[GeneratedActivitySource]`/`[GeneratedMeter]`/`[GeneratedCapability]` assembly attributes)
3. **Generate** interceptor methods that inject `builder.TryUseQylConventions(options => { ... })` before `Build()` and `app.MapQylDefaultEndpoints()` after

This means adding `qyl.instrumentation` to a project auto-configures OTel — no manual setup code needed.

### Cross-Assembly Scanning

The generator scans **referenced assemblies** for `[GeneratedActivitySource]`, `[GeneratedMeter]`, and `[GeneratedCapability]` attributes. This enables a library to define meters and traces, and the consuming app automatically registers them at startup.

### Pipeline Tracking Names

Every pipeline has a `WithTrackingName()` for Roslyn diagnostic debugging:
- `QylRuntimeCheck`, `BuilderCallSitesDiscovered`, `TracedCallSitesDiscovered`, `DbCallSitesDiscovered`, `GenAiCallSitesDiscovered`, `AgentCallSitesDiscovered`, `MeterDefinitionsDiscovered`, `ToolTypesDiscovered`, `CapabilitiesCurrentDiscovered`, `ToggleCheck`, `GeneratedTelemetryCurrentDiscovered`, `GeneratedTelemetryReferencedDiscovered`, `GeneratedTelemetryCombined`

---

## 2. LoomSourceGenerator

**Location:** `qyl/src/qyl.instrumentation.generators/Loom/LoomSourceGenerator.cs`
**Trigger:** `[LoomTool]`, `[LoomContract]`, `[LoomStep]`, `[LoomWorkflow]`
**Repo:** qyl

### Architecture

Four parallel `ForAttributeWithMetadataName` providers, one per attribute type. All four are combined and processed in a single `RegisterSourceOutput` callback.

```
[LoomTool]     → LoomToolExtractor     → LoomToolModel
[LoomContract] → LoomContractExtractor  → LoomContractModel
[LoomStep]     → LoomStepExtractor      → LoomStepModel
[LoomWorkflow] → LoomWorkflowExtractor  → LoomWorkflowModel
                ↓
        CompilationProvider.Select (gate: LoomToolDescriptor type exists)
                ↓
        RegisterSourceOutput: emit per-type + registry + telemetry manifest
```

### Runtime Gate

```csharp
compilation.GetTypeByMetadataName("Qyl.Instrumentation.Instrumentation.Loom.LoomToolDescriptor") is not null
```

### Output Files

| Input | Output Generator | Output File Pattern |
|-------|-----------------|---------------------|
| Per tool group (by containing type) | `LoomToolOutputGenerator` | `{TypeFQN}.LoomTools.g.cs` |
| Per contract | `LoomContractOutputGenerator` | `{TypeFQN}.LoomContract.g.cs` |
| Per step | `LoomStepOutputGenerator` | `{TypeFQN}.LoomStep.g.cs` |
| Per workflow | `LoomWorkflowOutputGenerator` | `{TypeFQN}.LoomWorkflow.g.cs` |
| All combined | `LoomRegistryOutputGenerator` | `LoomGeneratedRegistry.g.cs` |
| All combined | `LoomTelemetryManifestOutputGenerator` | `LoomGeneratedRegistry.TelemetryManifest.g.cs` |

### What LoomRegistryOutputGenerator Emits

A `static partial class LoomGeneratedRegistry` with:
- `Tools` — `IReadOnlyList<LoomToolDescriptor>`
- `RuntimeMetadata` — `IReadOnlyList<LoomRuntimeMetadataDescriptor>`
- `Contracts` — `IReadOnlyList<LoomContractDescriptor>`
- `Steps` — `IReadOnlyList<LoomStepDescriptor>`
- `Workflows` — `IReadOnlyList<LoomWorkflowDescriptor>`
- `Capabilities` — `IReadOnlyList<LoomCapabilityDescriptor>` (grouped by capability name)
- `ContractSchemas` — `IReadOnlyDictionary<string, string>` (name → JSON schema)

### What LoomTelemetryManifestOutputGenerator Emits

Additional properties on `LoomGeneratedRegistry`:
- `ParameterBindings` — `IReadOnlyList<LoomParameterBindingDescriptor>` (all tool parameters with schema visibility and infrastructure binding)
- `Results` — `IReadOnlyList<LoomResultDescriptor>` (output types, structured output, schema hints)
- `Telemetry` — `IReadOnlyList<LoomTelemetryDescriptor>` (per-tool telemetry metadata)
- `Policies` — `IReadOnlyList<LoomPolicyDescriptor>` (approval, budget, side effects, capabilities)
- `Manifest` — `IReadOnlyList<LoomManifestEntry>` (unified manifest of all tools, contracts, steps, workflows)
- `InterceptorManifest` — `LoomInterceptorManifest` (tool/step/workflow interceptor descriptors with span names)

### Partial Validation

All four attribute types also register partial validation pipelines via `RegisterPartialValidation()`, which uses `LoomDeclarationChainExtractor.ExtractWithDiagnostics()` to validate that types with Loom attributes are declared `partial` through the entire nesting chain.

### Extractors

| Extractor | Source | Extracts |
|-----------|--------|----------|
| `LoomToolExtractor` | `[LoomTool]` methods | Name, description, phase, parameters, capabilities, approval, side effects, budget, structured output, invoker |
| `LoomContractExtractor` | `[LoomContract]` types | Name, properties (with enum values, nullability, required) |
| `LoomStepExtractor` | `[LoomStep]` classes | Id, phase, executor type, description |
| `LoomWorkflowExtractor` | `[LoomWorkflow]` classes | Id, run state type, step IDs, description |
| `LoomParameterExtractor` | Parameters of `[LoomTool]` methods | Name, type, nullable, default value, description, enum values, schema visibility, infrastructure binding |
| `LoomPolicyExtractor` | `[RequiresApproval]`, `[ToolSideEffect]`, `[LoomBudget]`, `[RequiresCapability]`, `[EmitsStructuredOutput]` | Policy metadata aggregated onto tool models |
| `LoomDeclarationChainExtractor` | Any type declaration | Nesting chain for partial type emission (validates all levels are partial) |
| `LoomLiteralFormatter` | Default values | Literal string formatting for code generation |

---

## 3. DuckDbInsertGenerator

**Location:** `qyl/src/qyl.collector.storage.generators/DuckDbInsertGenerator.cs`
**Trigger:** `[DuckDbTable]`
**Repo:** qyl

### Architecture

The simplest generator. Single `ForAttributeWithMetadataName` pipeline:

```
[DuckDbTable] on partial type
    → ExtractTableInfo (inline)
    → DuckDbEmitter.Emit
    → {TypeName}.DuckDb.g.cs
```

### PostInitializationOutput

Emits `DuckDbAttributes.g.cs` containing the `[DuckDbTable]`, `[DuckDbColumn]`, and `[DuckDbIgnore]` attribute definitions. This means consumers don't need a separate attributes package — the attributes are emitted by the generator itself.

### Column Extraction

For each `[DuckDbTable]` type, the generator:
1. Iterates all public properties with getters
2. Skips properties with `[DuckDbIgnore]`
3. Extracts `[DuckDbColumn]` metadata (column name, UBIGINT flag, exclude-from-insert, ordinal)
4. Auto-detects UBIGINT for `ulong`/`System.UInt64` types
5. Converts PascalCase property names to snake_case for default column names

### Emitter Output (per type)

```csharp
partial class SpanRecord
{
    public const string TableName = "spans";
    public const string ColumnList = """
        "trace_id", "span_id", "parent_span_id", ...
        """;
    public const int ColumnCount = 15;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddParameters(DuckDBCommand cmd, SpanRecord row) { ... }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanRecord MapFromReader(DbDataReader reader) { ... }

    public static string BuildMultiRowInsertSql(int rowCount) { ... }
}
```

### Type Handling

| C# Type | Reader Method | Parameter Handling |
|---------|--------------|-------------------|
| `string` | `GetString(ordinal)` | Direct |
| `long` | `GetInt64(ordinal)` | Direct |
| `int` | `GetInt32(ordinal)` | Direct |
| `double` | `GetDouble(ordinal)` | Direct |
| `byte` | `GetByte(ordinal)` | Direct |
| `ulong` | `Col(ordinal).GetUInt64(0)` | `(decimal)value` |
| `DateTimeOffset` | `Col(ordinal).AsDateTimeOffset` | Direct |
| Nullable any | `Col(ordinal).As{Type}` | `value ?? (object)DBNull.Value` |
| Nullable ulong | `Col(ordinal).AsUInt64` | Ternary: `HasValue ? (decimal)Value : DBNull.Value` |

---

## 4. McpServerGenerator (netagents)

**Location:** `netagents/src/Qyl.Agents.Generator/McpServerGenerator.cs`
**Trigger:** `[McpServer]`
**Repo:** netagents

### Architecture

```
Primary pipeline:
    [McpServer] on class → ServerExtractor.Extract → ServerModel
        → OutputGenerator.GenerateOutput → single .g.cs per server

Secondary pipeline (validation only):
    [Tool] on method → check if containing type has [McpServer]
        → if not: report QA0004 diagnostic
```

### ServerExtractor

Extracts the complete server model from a `[McpServer]`-decorated class:
- Server metadata (name, description, version)
- All `[Tool]` methods via `ToolExtractor` (name, description, hints, task support, parameters)
- All `[Prompt]` methods via `PromptExtractor` (name, description, arguments)
- All `[Resource]` methods via `ResourceExtractor` (URI, name, MIME type, description)
- Type declaration chain for partial nesting

### ToolExtractor

For each `[Tool]` method:
- Extracts name, description, tool hints (ReadOnly, Destructive, Idempotent, OpenWorld)
- Extracts task support mode
- Extracts parameters via `ParameterExtractor`:
  - Name, type, `[Description]` attribute, nullability, default value, enum values
  - Supports: primitives, `string`, `DateTime`, `DateTimeOffset`, `Guid`, `Uri`, `TimeSpan`, arrays, `IEnumerable<T>`, custom types
  - Auto-detects `CancellationToken` parameters (excluded from schema)
- Validates: not static, not generic, no duplicate names

### Output Structure (per server)

`OutputGenerator.GenerateOutput()` assembles all sub-emitter outputs into a single source file:

| Sub-Emitter | What It Generates |
|-------------|-------------------|
| `DispatchEmitter` | `DispatchToolCallAsync()` — switch expression routing per tool; per-tool private dispatch methods with `JsonElement` accessors; tool listing method |
| `SchemaEmitter` | Static `byte[]` JSON Schema per tool (compile-time, zero runtime cost) |
| `JsonContextEmitter` | `[JsonSourceGenerationOptions]` partial class when complex types are present |
| `OTelEmitter` | `ActivitySource("Qyl.Agents")`, `Histogram<double>("gen_ai.client.operation.duration")`, per-tool span creation |
| `ResourceEmitter` | Resource dispatch, listing, and content reading |
| `PromptEmitter` | Prompt dispatch, listing, and template rendering |
| `SkillEmitter` | `GetSkillMarkdown()` — SKILL.md content as string |
| `LlmsTxtEmitter` | `GetLlmsTxt()` — llms.txt content as string |
| `MetadataEmitter` | `GetServerInfo()` — static metadata for server discovery |

### Diagnostics

| ID | Severity | Message |
|----|----------|---------|
| QA0001 | Error | `[McpServer]` class must be partial |
| QA0002 | Error | `[McpServer]` class must not be static or generic |
| QA0003 | Warning | Tool has no hints set |
| QA0004 | Error | `[Tool]` method found outside `[McpServer]` class |
| QA0005 | Error | Duplicate tool name in server |
| QA0006 | Error | Duplicate resource URI in server |
| QA0007 | Error | Duplicate prompt name in server |
| QA0008 | Warning | Parameter missing `[Description]` attribute |
| QA0009 | Error | Tool method must not be static or generic |
| QA0010 | Error | Unsupported parameter type |

### Known OTel Bugs

From `convergence-plan.md` — all in `DispatchEmitter.cs` / `OTelEmitter.cs`:
- Emits deprecated `gen_ai.system` (should be removed)
- Emits non-semconv `server.name` (should be removed)
- Missing `gen_ai.tool.call.id`, `gen_ai.tool.description`
- Missing `mcp.protocol.version`, `mcp.session.id`, `jsonrpc.protocol.version`
- Missing `rpc.response.status_code` on errors
- Only emits `gen_ai.client.operation.duration`, should also emit `mcp.server.operation.duration`

---

## 5. ToolManifestGenerator (qyl.mcp)

**Location:** `qyl.mcp/src/qyl.mcp.generators/ToolManifestGenerator.cs`
**Trigger:** `[McpServerToolType]` (from official MCP C# SDK)
**Repo:** qyl.mcp

### Architecture

```
[McpServerToolType] on class → ToolManifestAnalyzer.ExtractToolType → ToolTypeEntry
    → ToolManifestEmitter.Emit → QylToolManifest.g.cs
```

### Analyzer: ToolManifestAnalyzer

Extracts `[McpServerToolType]` classes and discovers their `[McpServerTool]` methods:
- Tool name (from `Name` property on `[McpServerTool]`, or method name kebab-cased)
- Title, Description, ReadOnly, Destructive, Idempotent, OpenWorld
- Return type display name

### Emitter Output

```csharp
internal static class QylToolManifest
{
    // Backward compat: Type[] for existing registration
    public static readonly Type[] ToolTypes = { typeof(SearchTracesTools), ... };

    // Metadata-rich descriptors for host projection and discovery
    public static readonly GeneratedToolDescriptor[] ToolDescriptors = [ ... ];

    // AOT-safe factory: resolves services, creates AIFunction instances
    public static List<AIFunction> CreateTools(IServiceProvider services, Func<Type, bool>? filter = null)
    {
        var tools = new List<AIFunction>();
        if (filter?.Invoke(typeof(SearchTracesTools)) != false)
        {
            var svc = services.GetRequiredService<SearchTracesTools>();
            tools.Add(AIFunctionFactory.Create(svc.SearchTraces, new AIFunctionFactoryOptions { Name = "qyl_search_traces" }));
        }
        return tools;
    }
}
```

### Difference from qyl's ToolManifestEmitter

| Feature | qyl version | qyl.mcp version |
|---------|-------------|-----------------|
| `ToolTypes` array | Yes | Yes |
| `ToolDescriptors` | No | Yes (name, method, type, title, description, hints, return type) |
| `CreateTools()` factory | No | Yes (AOT-safe via `AIFunctionFactory.Create(Delegate)`) |
| Uses `IndentedStringBuilder` | Yes | No (raw `StringBuilder`) |

---

## 6. AotReflectionGenerator (ANcpLua.Roslyn.Utilities)

**Location:** `ANcpLua.Roslyn.Utilities/src/ANcpLua.AotReflection/AotReflectionGenerator.cs`
**Trigger:** `[AotReflection]`
**Repo:** ANcpLua.Roslyn.Utilities

### Architecture

```
[AotReflection] on class → TypeExtractor.ExtractTypeModel → DiagnosticFlow<TypeModel>
    → ReportAndStop (diagnostics) → OutputGenerator.GenerateOutput → FileWithName
    → CollectAsEquatableArray → AddSources
```

### Extractors

| Extractor | Output Model | What |
|-----------|-------------|------|
| `TypeExtractor` | `TypeModel` | Type metadata (name, namespace, accessibility, generics) |
| `PropertyExtractor` | `PropertyModel` | Properties with getter/setter info |
| `MethodExtractor` | `MethodModel` | Methods with parameters and return types |
| `FieldExtractor` | `FieldModel` | Fields with type and default values |
| `ConstructorExtractor` | `ConstructorModel` | Constructors with parameters |
| `ParameterExtractor` | `ParameterModel` | Parameter types, defaults, attributes |
| `DeclarationChainExtractor` | `TypeDeclarationModel` | Nesting chain for partial types |
| `LiteralFormatter` | (helper) | Default value literal formatting |

### Code Generators

| Generator | What |
|-----------|------|
| `ClassMetadataGenerator` | Orchestrates full output structure |
| `PropertyCodeGenerator` | Getter/setter delegate expressions |
| `MethodCodeGenerator` | Invoker delegate expressions |
| `FieldCodeGenerator` | Field access delegate expressions |
| `OutputGenerator` | Final `.g.cs` file assembly |
| `GenerationHelpers` | Shared emit logic (indent, escaping, types) |

### Key Design Point

Uses `DiagnosticFlow<T>` from the Roslyn.Utilities library — a railway-oriented pipeline that carries both the value and accumulated diagnostics. This is the pattern the qyl generators should use more broadly.

---

## Shared Infrastructure (ANcpLua.Roslyn.Utilities)

All generators consume these via the `ANcpLua.Roslyn.Utilities.Sources` package (embedded as source):

| Utility | Purpose |
|---------|---------|
| `EquatableArray<T>` | Value-equatable array wrapper for incremental caching |
| `IndentedStringBuilder` | Scoped indentation via `BeginBlock()`/`EndBlock()` |
| `GeneratedCodeHelpers` | Standard headers, attributes, pragmas |
| `DiagnosticFlow<T>` | Railway-oriented diagnostic pipeline |
| `IncrementalValuesProviderExtensions` | `AddSource()`, `AddSources()`, `GroupBy()`, `Distinct()`, `CollectAsEquatableArray()` |
| `SymbolExtensions` | `HasAttribute()`, `GetAttribute()`, `GetAttributes()` |
| `AttributeExtensions` | `GetConstructorArgument()`, `GetNamedArgument()` |
| `SemanticGuard` | Fluent validation accumulating DiagnosticFlow |
| `CodeStylePreferences` | EditorConfig-aware code generation |
| `TypeCache<TEnum>` | O(1) enum-indexed symbol lookup |
| `StringExtensions` | `EndsWithOrdinal()`, `StartsWithOrdinal()`, `ToGlobalTypeName()` |

---

## Generator Comparison Matrix

| Dimension | ServiceDefaultsSourceGenerator | LoomSourceGenerator | DuckDbInsertGenerator | McpServerGenerator | ToolManifestGenerator | AotReflectionGenerator |
|-----------|-------------------------------|--------------------|-----------------------|-------------------|----------------------|----------------------|
| **Repo** | qyl | qyl | qyl | netagents | qyl.mcp | Roslyn.Utilities |
| **Pipelines** | 7 concurrent | 4 + registry + telemetry | 1 | 2 (primary + validation) | 1 | 1 |
| **Trigger** | Multiple (call sites + attributes) | 4 Loom attributes | `[DuckDbTable]` | `[McpServer]` | `[McpServerToolType]` | `[AotReflection]` |
| **Uses interceptors** | Yes (C# interceptors) | No | No | No | No | No |
| **Cross-assembly** | Yes (scans referenced assemblies) | No | No | No | No | No |
| **MSBuild toggles** | Yes (4 toggles) | No | No | No | No | No |
| **Uses IndentedStringBuilder** | No (raw StringBuilder) | Yes | No (raw StringBuilder) | No (EmitHelpers) | No (raw StringBuilder) | Yes |
| **Uses DiagnosticFlow** | No | Partial (via LoomDeclarationChainExtractor) | No | Yes (via ReportAndStop) | No | Yes |
| **PostInitializationOutput** | No | No | Yes (attributes) | No | No | No |
| **Output files per run** | 7 max | 2 + N per type | 1 per type | 1 per server | 1 | 1 per type |
| **AOT story** | Auto-registers sources | Compile-time descriptors | Zero-reflection DB access | Zero-reflection dispatch + JSON | AOT factory via AIFunctionFactory | Replaces runtime reflection |

---

## Generator Targeting

| Generator | Target Framework | Assembly |
|-----------|-----------------|----------|
| ServiceDefaultsSourceGenerator | netstandard2.0 | qyl.instrumentation.generators |
| LoomSourceGenerator | netstandard2.0 | qyl.instrumentation.generators |
| DuckDbInsertGenerator | netstandard2.0 | qyl.collector.storage.generators |
| McpServerGenerator | netstandard2.0 | Qyl.Agents.Generator |
| AotReflectionGenerator | netstandard2.0 | ANcpLua.AotReflection |
| ToolManifestGenerator | netstandard2.0 | qyl.mcp.generators |

All generators target netstandard2.0 because the Roslyn compiler host requires it.
