# Emitter Reference (25 emitters across 4 repos)

Emitters are the code generation units that transform extracted models into C# source code. Each emitter is a static class with an `Emit()` or similar entry point.

---

## ServiceDefaultsSourceGenerator Emitters

Location: `qyl/src/qyl.instrumentation.generators/Emitters/`

### TracedInterceptorEmitter

**Input:** `ImmutableArray<TracedCallSite>`
**Output:** `Intercepts.g.cs` — interceptor methods for `[Traced]` call sites

Generates:
- `TracedActivitySources` class with `ActivitySource` fields per unique source name
- `TracedInterceptors` class with `[InterceptsLocation]` methods
- Per-method interceptor with: span creation, tag setting from `[Tag]`/`[TracedTag]`, return capture from `[TracedReturn]`, error recording, status setting
- Three body patterns: sync, async (Task/ValueTask), async enumerable (IAsyncEnumerable)
- Assembly-level `[GeneratedActivitySourceAttribute]` for source registration

### AgentInterceptorEmitter

**Input:** `ImmutableArray<AgentCallSite>`
**Output:** `AgentIntercepts.g.cs` — interceptor methods for agent invocations

Generates:
- `AgentInterceptors` class with `[InterceptsLocation]` methods
- Per-invocation interceptor with: `ActivityKind.Client` span, `gen_ai.operation.name` (create_agent/invoke_agent), `gen_ai.provider.name`, `gen_ai.agent.name`
- Error capture with `ActivityExceptionTelemetry.Record()`

### DbInterceptorEmitter

**Input:** `ImmutableArray<DbCallSite>`
**Output:** `DbIntercepts.g.cs` — interceptor methods for ADO.NET calls

Generates:
- `DbInterceptors` class with `[InterceptsLocation]` methods
- Delegates to `DbInstrumentation.ExecuteReader[Async]`, `ExecuteNonQuery[Async]`, `ExecuteScalar[Async]`
- Supports concrete command types (e.g., `DuckDBCommand`) for type-safe interception

### MeterEmitter

**Input:** `ImmutableArray<MeterDefinition>`
**Output:** `Meters.g.cs` — meter implementations for `[Meter]` classes

Generates per `[Meter]` class:
- Static `Meter` field with name and version
- Per-method instrument field: `Counter<long>`, `Histogram<T>`, `UpDownCounter<T>`, `ObservableGauge<T>`
- Partial method implementations with tag support (0, 1, or N tags → different overloads)
- Gauge uses backing storage field with `ObservableGauge` callback
- Assembly-level `[GeneratedMeterAttribute]` for meter registration

### CapabilityEmitter

**Input:** `ImmutableArray<GenAiCallSite>`, `ImmutableArray<AgentCallSite>`
**Output:** `Capabilities.g.cs` — assembly-level capability declarations

Generates:
- `[GeneratedCapabilityAttribute("genai.provider", "...")]` for detected AI providers
- `[GeneratedCapabilityAttribute("genai.model", "...")]` for detected models
- `[GeneratedCapabilityAttribute("genai.operation", "...")]` for detected operations
- `[GeneratedCapabilityAttribute("agent", "...")]` for detected agent names

### ToolManifestEmitter (in qyl.instrumentation.generators)

**Input:** `ImmutableArray<ToolEntry>`
**Output:** `ToolManifest.g.cs` — GenAI tool catalog

### EmitterHelpers

Shared infrastructure:
- `AppendInterceptsLocationAttribute()` — emits the `[InterceptsLocation]` attribute definition
- `BuildParameterList()` — builds parameter list with `this T @this` for instance methods
- `BuildArgumentList()` — builds argument forwarding list

---

## LoomSourceGenerator Emitters

Location: `qyl/src/qyl.instrumentation.generators/Loom/Generation/`

### LoomToolOutputGenerator

**Input:** Tool models with declaration chain
**Output:** `{Namespace}.{Type}.LoomTools.g.cs`

Generates per `[LoomTool]` method:
- `{Method}Descriptor` — `LoomToolDescriptor` with name, description, phase, parameters, capabilities, approval, side effects, invoker
- `{Method}RuntimeMetadata` — `LoomRuntimeMetadataDescriptor` with parameter bindings, result descriptor, telemetry descriptor, policy descriptor

Sub-generators within:
- `AppendParameterArray` — `LoomToolParameterDescriptor[]` with type, nullability, defaults, enums
- `AppendRuntimeParameterBindingArray` — `LoomParameterBindingDescriptor[]` with schema visibility, infrastructure binding
- `AppendRuntimeResultDescriptor` — `LoomResultDescriptor` with output types, schema hints
- `AppendRuntimeTelemetryDescriptor` — `LoomTelemetryDescriptor` with phase, awaitable, return info
- `AppendRuntimePolicyDescriptor` — `LoomPolicyDescriptor` with budget, approval, side effects

### LoomContractOutputGenerator

**Input:** `LoomContractModel`
**Output:** `{Namespace}.{Type}.LoomContract.g.cs`

Generates:
- `Descriptor` static property — `LoomContractDescriptor` with name, type, properties array
- Per-property entries with name, type, nullability

### LoomStepOutputGenerator

**Input:** `LoomStepModel`
**Output:** `{Namespace}.{Type}.LoomStep.g.cs`

Generates:
- `Descriptor` static property — `LoomStepDescriptor` with ID, phase, executor type, description

### LoomWorkflowOutputGenerator

**Input:** `LoomWorkflowModel`
**Output:** `{Namespace}.{Type}.LoomWorkflow.g.cs`

Generates:
- `Descriptor` static property — `LoomWorkflowDescriptor` with ID, run state type, step IDs, description

### LoomRegistryOutputGenerator

**Input:** All tool models, contract models
**Output:** `LoomGeneratedRegistry.g.cs`

Generates:
- `Tools` — `IReadOnlyList<LoomToolDescriptor>` array of all tool descriptors
- `Capabilities` — `IReadOnlyList<LoomCapabilityDescriptor>` grouped by capability with tool names and side effects
- `ContractSchemas` — `IReadOnlyList<LoomContractSchemaDescriptor>` with type → property mappings

### LoomTelemetryManifestOutputGenerator

**Input:** All tool models, contract models, step models, workflow models
**Output:** `LoomGeneratedRegistry.TelemetryManifest.g.cs`

Generates:
- `ParameterBindings` — `IReadOnlyList<LoomParameterBindingDescriptor>` for all tool parameters
- `Results` — `IReadOnlyList<LoomResultDescriptor>` for all tool results
- `Telemetry` — `IReadOnlyList<LoomTelemetryDescriptor>` with phase, awaitable, side effects
- `Policies` — `IReadOnlyList<LoomPolicyDescriptor>` with budgets and approval requirements
- `Manifest` — `IReadOnlyList<LoomManifestEntry>` unified inventory of tools, contracts, steps, workflows
- `InterceptorManifest` — `LoomInterceptorManifest` with tool/step/workflow interceptor descriptors

### LoomGenerationHelpers

Shared Loom emit infrastructure:
- `StringLiteral()` — C# string literal with proper escaping
- `NullableStringLiteral()` — nullable string literal or `null`
- `TypeOf()` — `typeof(global::Fully.Qualified.Type)` expression
- `GetNamespace()` — extract namespace from fully qualified type name
- `AppendDeclarationChain()` — emit nested type declarations for partial types

---

## DuckDbInsertGenerator Emitters

Location: `qyl/src/qyl.collector.storage.generators/`

### DuckDbEmitter

**Input:** Table models with property mappings
**Output:** Per-table generated file

Generates:
- `InsertAsync(DuckDBConnection, T)` — single-row insert with parameterized SQL
- `InsertBatchAsync(DuckDBConnection, IEnumerable<T>)` — batch insert with appender
- Column name mapping (PascalCase → snake_case unless `[DuckDbColumn]` overrides)
- Parameter binding for all mapped property types
- ON CONFLICT DO UPDATE upsert logic for tables with primary keys
- Type mapping: C# types → DuckDB types (string→VARCHAR, DateTime→TIMESTAMP, etc.)

---

## McpServerGenerator Emitters

Location: `netagents/src/Qyl.Agents.Generator/Generation/`

### DispatchEmitter

Generates:
- `DispatchToolCallAsync(string, JsonElement, CancellationToken)` — switch expression routing by tool name
- Per-tool private methods with: OTel span (`execute_tool {name}`, ActivityKind.Internal), direct `JsonElement` accessors for primitives, `JsonSerializer` fallback for complex types, `Stopwatch` + `Histogram<double>` duration recording, error capture
- AOT-safe parameter deserialization with format-aware accessors (date-time, uuid, uri, enums)
- Return serialization: string direct, bool manual, numeric culture-invariant, complex via JsonSerializer

### SchemaEmitter

Generates:
- `s_schema_{MethodName}` — `static readonly byte[]` per tool with compile-time JSON Schema
- Properties with type, format, description, enum values, required array

### OTelEmitter

Generates:
- `s_activitySource` — `ActivitySource("Qyl.Agents", version)` from assembly version
- `s_meter` — `Meter("Qyl.Agents", version)`
- `s_requestDuration` — `Histogram<double>("gen_ai.client.operation.duration", "s")`

### MetadataEmitter

Generates:
- `GetServerInfo()` — `McpServerInfo` with name, description, version
- `GetToolInfos()` — `McpToolInfo[]` with name, description, schema bytes, hints (ReadOnly, Destructive, Idempotent, OpenWorld), task support
- `GetResourceInfos()` — `McpResourceInfo[]` with URI, name, description, MIME type
- `GetPromptInfos()` — `McpPromptInfo[]` with name, description, arguments

### PromptEmitter

Generates:
- `DispatchPromptAsync(string, JsonElement, CancellationToken)` — switch routing by prompt name
- Per-prompt methods with parameter deserialization (simplified, strings-only typical)
- Structured prompts pass `PromptResult` through; string prompts wrap in single `PromptMessage`

### ResourceEmitter

Generates:
- `DispatchResourceReadAsync(string, CancellationToken)` — switch routing by URI
- Per-resource methods with binary (base64) vs text handling
- Stub methods that throw for unknown URIs when no resources defined

### JsonContextEmitter

Generates (only when complex types exist):
- `[JsonSourceGenerationOptions]` partial class targeting complex parameter and return types
- `[JsonSerializable(typeof(T))]` entries for each complex type

### SkillEmitter

Generates:
- `s_skillMd` string constant with SKILL.md content
- YAML frontmatter with name + description
- Tool sections with parameters, annotation hints
- Resource and prompt sections

### LlmsTxtEmitter

Generates:
- `s_llmsTxt` string constant with llms.txt content
- Server name, description, tool listing

---

## ToolManifestGenerator Emitters (qyl.mcp)

Location: `qyl.mcp/src/qyl.mcp.generators/Emitters/`

### ToolManifestEmitter

**Input:** `ImmutableArray<ToolEntry>`
**Output:** `QylToolManifest.g.cs`

Generates:
- `QylToolManifest` static class
- `Type[]` array of all `[McpServerToolType]` classes
- Delegate factory for tool instantiation

### Note on divergence

This emitter uses raw `StringBuilder` with hardcoded indentation. The convergence plan recommends migrating to `IndentedStringBuilder` + `GeneratedCodeHelpers` from ANcpLua.Roslyn.Utilities.Sources, which the same project already references.

---

## AotReflectionGenerator Emitters

Location: `ANcpLua.Roslyn.Utilities/src/ANcpLua.AotReflection/`

### ClassMetadataGenerator

Orchestrates all sub-generators to produce a complete `ClassMetadata` instance.

### PropertyCodeGenerator

Generates getter/setter delegate expressions:
```csharp
new PropertyMetadata("Name", typeof(string),
    getter: (obj) => ((Foo)obj).Name,
    setter: (obj, val) => ((Foo)obj).Name = (string)val)
```

### MethodCodeGenerator

Generates invoker delegate expressions:
```csharp
new MethodMetadata("DoWork", typeof(void),
    invoker: (obj, args) => ((Foo)obj).DoWork((int)args[0]))
```

### FieldCodeGenerator

Generates field access delegate expressions:
```csharp
new FieldMetadata("_count", typeof(int),
    getter: (obj) => ((Foo)obj)._count,
    setter: (obj, val) => ((Foo)obj)._count = (int)val)
```

### OutputGenerator

Assembles the final `.g.cs` file with namespace, using directives, and the metadata class.

### GenerationHelpers

Shared emit logic: indentation, type name escaping, accessibility keywords.

---

## Metadata-Only Attributes (No Dedicated Emitter)

These attributes exist but have no dedicated emitter — they are consumed as metadata by parent emitters:

| Attribute | How It's Used |
|-----------|--------------|
| `[NoTrace]` | Filter in `TracedCallSiteAnalyzer` — skips methods |
| `[OTel]` | Naming override in `TelemetryTagNameResolver` |
| `[TracedTag]` | Consumed by `TracedInterceptorEmitter` as part of `TracedCallSite` |
| `[TracedReturn]` | Consumed by `TracedInterceptorEmitter` as part of `TracedCallSite` |
| `[Tag]` | Consumed by `MeterEmitter` as part of `MeterDefinition` |
| `[DuckDbColumn]` | Consumed by `DuckDbEmitter` as part of `DuckDbTableInfo` |
| `[DuckDbIgnore]` | Filter in `DuckDbInsertGenerator` — skips properties |
| `[RequiresCapability]` | Consumed by `LoomToolOutputGenerator` |
| `[RequiresApproval]` | Consumed by `LoomToolOutputGenerator` |
| `[ToolSideEffect]` | Consumed by `LoomToolOutputGenerator` |
| `[EmitsStructuredOutput]` | Consumed by `LoomToolOutputGenerator` |
| `[LoomBudget]` | Consumed by `LoomToolOutputGenerator` |
