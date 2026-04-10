# Attribute Catalog — Complete Inventory Across All Repos

> 38 attributes across 5 repos. Every attribute triggers compile-time code generation — no runtime reflection.

---

## How Attributes Work in qyl

An attribute is a marker. You put `[Something]` on a class, method, parameter, or property. A Roslyn source generator picks it up at compile time and emits C# code into `obj/Generated/`. The emitted code does what you would otherwise write by hand — span creation, metric recording, DI wiring, dispatch routing, schema generation, DuckDB parameter binding. Zero runtime reflection. AOT-safe.

The pipeline is always:

```
[Attribute] on code → Analyzer extracts model → Emitter generates .g.cs
```

---

## 1. QYL Instrumentation — OTel Tracing (5 attributes)

### `[Traced]` — Auto-OTel Spans

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `ActivitySourceName` | `string` | (required) | Name of the `ActivitySource` to use |
| `SpanName` | `string?` | method name | Custom span name |
| `Kind` | `ActivityKind` | `Internal` | Span kind (Client, Server, Producer, Consumer, Internal) |
| `RootSpan` | `bool` | `false` | When true, creates an isolated root span (no parent) |

**Target:** `Class | Method`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/TracedAttribute.cs`

**What happens:**
- On a **class**: all public methods get traced. Use `[NoTrace]` to opt out specific methods.
- On a **method**: that method gets traced.
- The `TracedCallSiteAnalyzer` finds every call site where a `[Traced]` method is invoked.
- The `TracedInterceptorEmitter` generates C# interceptor methods that wrap each call with `ActivitySource.StartActivity()`, tag setters for `[TracedTag]` parameters and properties, `[TracedReturn]` capture, `code.*` OTel attributes, exception recording, and status setting.

**Example:**
```csharp
[Traced("MyApp.Orders")]
public class OrderService
{
    public async Task<Order> GetOrder([TracedTag] string id) { }

    [NoTrace]
    public void HelperMethod() { }

    [Traced(SpanName = "custom.operation", Kind = ActivityKind.Client)]
    public void CustomOperation() { }
}
```

**Generator:** `ServiceDefaultsSourceGenerator` (pipeline: `TracedCallSitesDiscovered`)
**Analyzer:** `TracedCallSiteAnalyzer`
**Emitter:** `TracedInterceptorEmitter`
**Model:** `TracedCallSite`
**Output file:** `TracedIntercepts.g.cs`

---

### `[NoTrace]` — Opt-Out from Class-Level Tracing

**Target:** `Method`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/NoTraceAttribute.cs`

No properties. Parameterless. Excludes a method from tracing when the containing class has `[Traced]`.

**Generator:** Not a generator trigger — consumed by `TracedCallSiteAnalyzer` to skip methods.

---

### `[TracedTag]` — Span Attribute from Parameter or Property

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string?` | parameter/property name | Span attribute key |
| `SkipIfNull` | `bool` | `true` | Skip tag when value is null |
| `SkipIfDefault` | `bool` | `false` | Skip tag when value equals `default(T)` |

**Target:** `Parameter | Property`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/TracedTagAttribute.cs`

**What happens:**
- On a **parameter** of a `[Traced]` method: the parameter value is recorded as a span tag.
- On a **property** of a class with `[Traced]` methods: the property value is recorded on every span from that class.
- The emitter generates null/default checks based on `SkipIfNull` and `SkipIfDefault`.

**Generator:** Consumed by `TracedCallSiteAnalyzer` → emitted by `TracedInterceptorEmitter`
**Model:** `TracedTagParameter` (parameters), `TracedTagProperty` (properties)

---

### `[TracedReturn]` — Span Attribute from Return Value

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `TagName` | `string` | (required) | Span attribute key |
| `Property` | `string?` | `null` (uses `ToString()`) | Dotted member path (e.g. `"Usage.InputTokens"`) |

**Target:** `ReturnValue`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/TracedReturnAttribute.cs`

**Usage:** `[return: TracedReturn("product.id", Property = "Id")]`

**Generator:** Consumed by `TracedCallSiteAnalyzer` → emitted by `TracedInterceptorEmitter`
**Model:** `TracedReturnInfo`

---

### `[OTel]` — Semantic Convention Key Override

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | OTel semconv attribute name (e.g. `"gen_ai.request.model"`) |
| `SkipIfNull` | `bool` | `true` | Skip when value is null |

**Target:** `Property | Parameter`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/OTelAttribute.cs`

Does not emit telemetry on its own. Acts as a naming override for `[TracedTag]` and `[Tag]`. The `TelemetryTagNameResolver` in the generator checks for `[OTel]` first, then falls back to `[TracedTag]`/`[Tag]` name, then parameter name.

---

## 2. QYL Instrumentation — OTel Metrics (6 attributes)

### `[Meter]` — Meter Container Class

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Meter name (e.g. `"MyApp"`) |
| `Version` | `string?` | `null` | Meter version |

**Target:** `Class`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/MeterAttribute.cs`

The class must be `partial`. The generator creates a `static readonly Meter _meter` field and instrument fields for each metric method.

**Generator:** `ServiceDefaultsSourceGenerator` (pipeline: `MeterDefinitionsDiscovered`)
**Analyzer:** `MeterAnalyzer`
**Emitter:** `MeterEmitter`
**Model:** `MeterDefinition`
**Output file:** `MeterImplementations.g.cs`

---

### `[Counter]` — Counter Metric

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Metric name (e.g. `"orders.created"`) |
| `Unit` | `string?` | `null` | Unit (e.g. `"{order}"`) |
| `Description` | `string?` | `null` | Metric description |

**Target:** `Method` (must be `partial`, inside a `[Meter]` class)
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/CounterAttribute.cs`

**What happens:**
- If the first non-`[Tag]` parameter is numeric, its value is passed to `Counter<long>.Add(value)`.
- If no non-tag parameter exists, the generator emits `Add(1)` for simple occurrence counters.
- `[Tag]` parameters become `KeyValuePair<string, object?>` dimensions.

**Emitter:** `MeterEmitter` (handles all metric kinds)

---

### `[Histogram]` — Histogram Metric

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Metric name |
| `Unit` | `string?` | `null` | Unit (e.g. `"ms"`, `"s"`) |
| `Description` | `string?` | `null` | Metric description |

**Target:** `Method` (must be `partial`, inside a `[Meter]` class)
**Source:** `src/qyl.instrumentation/Instrumentation/HistogramAttribute.cs`

First non-`[Tag]` parameter is the value to record. Default value type is `double`.

---

### `[UpDownCounter]` — Up-Down Counter Metric

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Metric name |
| `Unit` | `string?` | `null` | Unit |
| `Description` | `string?` | `null` | Metric description |

**Target:** `Method` (must be `partial`, inside a `[Meter]` class)
**Source:** `src/qyl.instrumentation/Instrumentation/UpDownCounterAttribute.cs`

First non-`[Tag]` parameter is the delta (positive = increment, negative = decrement). Default value type is `long`.

---

### `[Gauge]` — Observable Gauge Metric

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Metric name |
| `Unit` | `string?` | `null` | Unit |
| `Description` | `string?` | `null` | Metric description |

**Target:** `Method` (must be `partial`, inside a `[Meter]` class)
**Source:** `src/qyl.instrumentation/Instrumentation/GaugeAttribute.cs`

Creates an `ObservableGauge` with a stored value field. Calling the method updates the stored value; the gauge reports it on collection. Default value type is `long`.

---

### `[Tag]` — Metric Dimension Parameter

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string?` | parameter name or `[OTel]` name | Tag name for the metric dimension |

**Target:** `Parameter`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qyl.instrumentation/Instrumentation/TagAttribute.cs`

Used with `[Counter]`, `[Histogram]`, `[Gauge]`, or `[UpDownCounter]`. The generator falls back to `[OTel]` name if `[Tag]` name is null.

**Model:** `MetricTagParameter`

---

## 3. QYL Instrumentation — Agent Tracing (1 attribute)

### `[AgentTraced]` — Agent Invocation Tracing

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `AgentName` | `string?` | method name | Agent name recorded as `gen_ai.agent.name` |

**Target:** `Method`
**Namespace:** `Qyl.Instrumentation.Instrumentation`
**Source:** `src/qql.instrumentation/Instrumentation/AgentTracedAttribute.cs`

**What happens:**
- The `AgentCallSiteAnalyzer` detects `[AgentTraced]` methods (in addition to MAF `InvokeAsync` / builder registration calls).
- The `AgentInterceptorEmitter` generates interceptors that wrap calls with `gen_ai.agent.invoke` spans using the `"qyl.agent"` ActivitySource.
- Sets `gen_ai.operation.name`, `gen_ai.provider.name`, and `gen_ai.agent.name` span attributes.

**Generator:** `ServiceDefaultsSourceGenerator` (pipeline: `AgentCallSitesDiscovered`)
**Analyzer:** `AgentCallSiteAnalyzer`
**Emitter:** `AgentInterceptorEmitter`
**Model:** `AgentCallSite` (kind: `AgentTracedMethod`)
**Output file:** `AgentIntercepts.g.cs`

---

## 4. QYL Instrumentation — Assembly-Level Registration (3 attributes)

These are **emitted by generators**, not written by users. They mark assemblies so that `QylServiceDefaults` can auto-discover and register telemetry sources at startup.

### `[GeneratedActivitySource]`

**Target:** `Assembly` (AllowMultiple)
**Source:** `src/qyl.instrumentation/GeneratedTelemetryRegistrationAttributes.cs`
**Emitted by:** `TracedInterceptorEmitter` — one per distinct `ActivitySourceName` in `[Traced]` methods.

### `[GeneratedMeter]`

**Target:** `Assembly` (AllowMultiple)
**Source:** `src/qyl.instrumentation/GeneratedTelemetryRegistrationAttributes.cs`
**Emitted by:** `MeterEmitter` — one per distinct meter name in `[Meter]` classes.

### `[GeneratedCapability]`

| Property | Type | Purpose |
|----------|------|---------|
| `Kind` | `string` | Capability kind: `"agent"`, `"genai.provider"`, `"genai.model"`, `"genai.operation"` |
| `Value` | `string` | Capability value (agent name, provider ID, model name) |

**Target:** `Assembly` (AllowMultiple)
**Source:** `src/qyl.instrumentation/GeneratedTelemetryRegistrationAttributes.cs`
**Emitted by:** `CapabilityEmitter` — from GenAI and Agent call site analysis.

These feed into OTel Resource attributes for fleet-wide topology discovery (`qyl.capability.agents`, `qyl.capability.genai.providers`, etc.).

---

## 5. QYL Loom Workflow — Compiler Attributes (9 attributes)

### `[LoomTool]` — Workflow Tool Method

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Tool name |
| `Description` | `string` | `""` | Tool description |
| `Phase` | `LoomPhase` | `Detect` | Pipeline phase (Detect/Plan/Fix/Verify/Report/Close) |
| `UseOnlyWhen` | `string?` | `null` | Condition when tool should be used |
| `DoNotUseWhen` | `string?` | `null` | Condition when tool should NOT be used |

**Target:** `Method`
**Namespace:** `Qyl.Instrumentation.Instrumentation.Loom`
**Source:** `src/qyl.instrumentation/Instrumentation/Loom/LoomAttributes.cs`

**Generator:** `LoomSourceGenerator`
**Extractor:** `LoomToolExtractor`
**Output Generator:** `LoomToolOutputGenerator`
**What it generates:**
- `{MethodName}Descriptor` — `LoomToolDescriptor` with name, description, phase, parameters, capabilities, approval, side effects, invoker delegate
- `{MethodName}RuntimeMetadata` — `LoomRuntimeMetadataDescriptor` with parameter bindings, result descriptor, telemetry descriptor, policy descriptor
- Registry entries in `LoomGeneratedRegistry.Tools` and `LoomGeneratedRegistry.RuntimeMetadata`
- Telemetry manifest entries

---

### `[LoomContract]` — Workflow Data Contract

| Property | Type | Purpose |
|----------|------|---------|
| `Name` | `string` | Contract name |

**Target:** `Class | Struct`
**Extractor:** `LoomContractExtractor`
**Output Generator:** `LoomContractOutputGenerator`
**What it generates:**
- `Descriptor` — `LoomContractDescriptor` with properties
- `JsonSchema` — compile-time JSON Schema string for the contract type
- Registry entry in `LoomGeneratedRegistry.Contracts`
- Schema entry in `LoomGeneratedRegistry.ContractSchemas`

---

### `[LoomStep]` — Workflow Step

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Id` | `string` | (required) | Step identifier |
| `Phase` | `LoomPhase` | `Detect` | Pipeline phase |
| `Description` | `string?` | `null` | Step description |

**Target:** `Class`
**Extractor:** `LoomStepExtractor`
**Output Generator:** `LoomStepOutputGenerator`
**What it generates:**
- `Descriptor` — `LoomStepDescriptor`
- Registry entry in `LoomGeneratedRegistry.Steps`
- Interceptor manifest entry

---

### `[LoomWorkflow]` — Complete Workflow Definition

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Id` | `string` | (required) | Workflow identifier |
| `RunStateType` | `Type` | (required) | The run state type |
| `StepIds` | `string[]` | (required, params) | Ordered step IDs composing the workflow |
| `Description` | `string?` | `null` | Workflow description |

**Target:** `Class`
**Extractor:** `LoomWorkflowExtractor`
**Output Generator:** `LoomWorkflowOutputGenerator`
**What it generates:**
- `Descriptor` — `LoomWorkflowDescriptor` with step IDs and run state type
- Registry entry in `LoomGeneratedRegistry.Workflows`
- Interceptor manifest entry

---

### `[RequiresCapability]` — Capability Gate

| Property | Type | Purpose |
|----------|------|---------|
| `Capability` | `string` | Required capability name (e.g. `"github"`, `"code_search"`) |

**Target:** `Method | Class` (AllowMultiple)
**What it does:** Declares that a tool or step requires a specific capability. The Loom runtime checks capabilities before tool invocation. Appears in `LoomGeneratedRegistry.Capabilities`.

---

### `[RequiresApproval]` — Approval Gate

**Target:** `Method | Class`
No properties. Marks a tool or step as requiring human approval before execution. The `LoomPolicyDescriptor` records `RequiresApproval = true`.

---

### `[ToolSideEffect]` — Side Effect Declaration

| Property | Type | Purpose |
|----------|------|---------|
| `SideEffect` | `ToolSideEffect` enum | `None`, `ReadsExternalState`, `WritesExternalState`, `MutatesCode`, `Deploys`, `ClosesIssue` |

**Target:** `Method | Class`
Declares what external effects a tool has. Recorded in `LoomPolicyDescriptor` and `LoomTelemetryDescriptor`.

---

### `[EmitsStructuredOutput]` — Structured Output Type

| Property | Type | Purpose |
|----------|------|---------|
| `OutputType` | `Type` | The structured output type |

**Target:** `Method | Class`
Declares that a tool produces structured output of the given type.

---

### `[LoomBudget]` — Execution Budget

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `MaxAttempts` | `int` | `1` | Maximum retry attempts |
| `MaxToolCalls` | `int` | `8` | Maximum tool calls per execution |
| `MaxTokens` | `int` | `16000` | Maximum token budget |

**Target:** `Method | Class`
Sets execution limits for a tool. Recorded in `LoomPolicyDescriptor`.

---

## 6. QYL DuckDB Storage — Data Access (3 attributes)

### `[DuckDbTable]` — DuckDB Type Mapping

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `TableName` | `string` | (required) | DuckDB table name |
| `OnConflict` | `string?` | `null` | ON CONFLICT clause for upsert behavior |

**Target:** `Class | Struct` (must be `partial`)
**Namespace:** `Qyl.Collector.Storage`
**Source:** Emitted via `PostInitializationOutput` from `DuckDbAttributes.cs`

**Generator:** `DuckDbInsertGenerator` (`qyl.collector.storage.generators`)
**Emitter:** `DuckDbEmitter`
**What it generates (per type):**
- `TableName` constant
- `ColumnList` constant (comma-separated column names)
- `ColumnCount` constant
- `AddParameters(DuckDBCommand, T)` — type-safe parameter binding for INSERT
- `MapFromReader(DbDataReader)` — ordinal-based reader mapping
- `BuildMultiRowInsertSql(int)` — multi-row INSERT SQL builder

---

### `[DuckDbColumn]` — Column Mapping Override

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `ColumnName` | `string?` | snake_case of property name | Column name in DuckDB |
| `IsUBigInt` | `bool` | `false` | UBIGINT columns needing decimal conversion |
| `ExcludeFromInsert` | `bool` | `false` | Exclude from INSERT (auto-generated columns) |
| `Ordinal` | `int` | `-1` (auto-assigned) | Column ordinal for SELECT mapping |

**Target:** `Property`
**Source:** Emitted via `PostInitializationOutput`

---

### `[DuckDbIgnore]` — Exclude Property from Mapping

**Target:** `Property`
No properties. Excludes a property from DuckDB mapping entirely.

---

## 7. Netagents MCP — Server Authoring (4 attributes + 2 enums)

### `[McpServer]` — MCP Server Class

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string?` | class name, kebab-cased | Server name for MCP protocol |
| `Description` | `string?` | XML doc summary | Server description |
| `Version` | `string?` | assembly informational version | Server version |

**Target:** `Class` (must be `partial`, not static, not generic)
**Namespace:** `Qyl.Agents`
**Source:** `netagents/src/Qyl.Agents.Abstractions/McpServerAttribute.cs`

**Generator:** `McpServerGenerator` (`netagents/src/Qyl.Agents.Generator/`)
**Extractor:** `ServerExtractor`
**Output:** `OutputGenerator.GenerateOutput()` which calls all sub-emitters:
- `DispatchEmitter` — switch-expression dispatch routing
- `SchemaEmitter` — static `byte[]` JSON schemas
- `JsonContextEmitter` — `[JsonSourceGenerationOptions]` partial class
- `OTelEmitter` — ActivitySource + Histogram (has known bugs, see convergence-plan.md)
- `ResourceEmitter` — resource endpoints
- `PromptEmitter` — prompt templates
- `SkillEmitter` — SKILL.md content
- `LlmsTxtEmitter` — llms.txt content
- `MetadataEmitter` — server metadata

---

### `[Tool]` — AI-Callable Tool Method

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string?` | method name, kebab-cased | Tool name for MCP protocol |
| `Description` | `string?` | XML doc summary | Tool description |
| `ReadOnly` | `ToolHint` | `Unset` | Read-only hint |
| `Idempotent` | `ToolHint` | `Unset` | Idempotent hint |
| `Destructive` | `ToolHint` | `Unset` | Destructive hint |
| `OpenWorld` | `ToolHint` | `Unset` | Open-world hint |
| `TaskSupport` | `ToolTaskSupport` | `Unset` | Long-running task support |

**Target:** `Method` (must be inside `[McpServer]` class)
**Namespace:** `Qyl.Agents`
**Extractor:** `ToolExtractor`
**Diagnostic:** QA0004 if `[Tool]` is found outside `[McpServer]` class

---

### `[Prompt]` — MCP Prompt Template

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Name` | `string` | (required) | Prompt name |
| `Description` | `string?` | XML doc summary | Prompt description |

**Target:** `Method`
**Namespace:** `Qyl.Agents`
**Extractor:** `PromptExtractor`
**Emitter:** `PromptEmitter`

---

### `[Resource]` — MCP Resource Endpoint

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Uri` | `string` | (required) | Resource URI (e.g. `"config://agents.toml"`) |
| `Name` | `string?` | method name | Display name |
| `MimeType` | `string?` | `null` | Content MIME type |
| `Description` | `string?` | XML doc summary | Description |

**Target:** `Method`
**Namespace:** `Qyl.Agents`
**Extractor:** `ResourceExtractor`
**Emitter:** `ResourceEmitter`

---

### `ToolHint` enum

```
Unset = 0    // Not declared — unknown to agent
True  = 1    // Explicitly true
False = 2    // Explicitly false
```

### `ToolTaskSupport` enum

```
Unset     = 0    // Omitted from wire format
Forbidden = 1    // Task execution forbidden
Optional  = 2    // Task execution optional
Required  = 3    // Task execution required
```

---

## 8. QYL MCP — Tool Manifest (2 attributes, from official MCP C# SDK)

These are **not defined in qyl** — they come from the `ModelContextProtocol` NuGet package.

### `[McpServerToolType]` — Tool Type Class Marker

Discovered by `ToolManifestAnalyzer` in both `qyl.instrumentation.generators` and `qyl.mcp.generators`. Used to build compile-time `Type[]` arrays replacing hardcoded tool registries.

### `[McpServerTool]` — Tool Method Marker

Discovered by `ToolManifestAnalyzer` in `qyl.mcp.generators`. Extracts tool name, title, description, hints, and return type.

**Generator (qyl):** `ServiceDefaultsSourceGenerator` → `ToolManifestEmitter` → `QylToolManifest.g.cs`
**Generator (qyl.mcp):** `ToolManifestGenerator` → `ToolManifestEmitter` → `QylToolManifest.g.cs`

The qyl.mcp version is richer — it also emits `ToolDescriptors[]` (metadata-rich descriptors) and `CreateTools()` (AOT-safe factory using `AIFunctionFactory.Create(Delegate)`).

---

## 9. ANcpLua.Roslyn.Utilities — AOT Reflection (7 attributes + 1 generator attribute)

### `[AotReflection]` — Compile-Time Reflection Metadata

**Target:** `Class`
**Namespace:** `ANcpLua.Analyzers.AotReflection`
**Source:** `ANcpLua.Roslyn.Utilities/src/ANcpLua.AotReflection.Attributes/AotReflectionAttribute.cs`

**Generator:** `AotReflectionGenerator`
**What it generates:**
Instead of `typeof(T).GetProperties()` at runtime (which fails with AOT/trimming), generates a `{TypeName}Metadata` class with:
- `ClassMetadata` — type-level container
- `PropertyMetadata[]` — getter/setter delegate expressions
- `MethodMetadata[]` — invoker delegate expressions
- `FieldMetadata[]` — field access delegate expressions
- `ConstructorMetadata[]` — constructor invoker delegates
- `ParameterMetadata[]` — parameter details

All delegates are strongly-typed. Zero reflection. NativeAOT safe.

**Pipeline:**
```
[AotReflection] → TypeExtractor → TypeModel
                   PropertyExtractor → PropertyModel
                   MethodExtractor → MethodModel
                   FieldExtractor → FieldModel
                   ConstructorExtractor → ConstructorModel
                   ParameterExtractor → ParameterModel
                   DeclarationChainExtractor → TypeDeclarationModel
                ↓
                ClassMetadataGenerator → orchestrates output
                PropertyCodeGenerator → getter/setter delegates
                MethodCodeGenerator → invoker delegates
                FieldCodeGenerator → field access delegates
                OutputGenerator → final .g.cs
```

### AOT Testing Attributes (6)

From `ANcpLua.Roslyn.Utilities.Testing.Aot`:

| Attribute | Purpose |
|-----------|---------|
| `[AotTest]` | Mark test as AOT-specific |
| `[AotSafe]` | Mark code as AOT-safe |
| `[AotUnsafe]` | Mark code as AOT-unsafe |
| `[TrimSafe]` | Mark code as trim-safe |
| `[TrimTest]` | Mark test as trimming-specific |
| `[TrimUnsafe]` | Mark code as trim-unsafe |

These don't trigger generators — they're markers for test infrastructure and feature-switch verification.

---

## 10. QYL Contracts — Generated Semconv Constants (not attributes, but attribute-like)

From `src/qyl.contracts/Attributes/`:
- `DbAttributes` — OTel 1.40.0 Database semantic conventions
- `GenAiAttributes` — OTel 1.40.0 GenAI semantic conventions
- `McpAttributes` — OTel 1.40.0 MCP semantic conventions

These are generated by `eng/semconv/generate-semconv.ts` from upstream OTel semconv data, not by Roslyn generators. They provide string constants consumed by the emitters (e.g., `AgentInterceptorEmitter` uses `GenAiAttributes.OperationName`).

---

## Summary Table

| # | Attribute | Target | Repo | Has Generator | Generator/Emitter |
|---|-----------|--------|------|---------------|-------------------|
| 1 | `[Traced]` | Class/Method | qyl | Yes | TracedInterceptorEmitter |
| 2 | `[NoTrace]` | Method | qyl | No (filter) | — |
| 3 | `[TracedTag]` | Parameter/Property | qyl | Yes (consumed) | TracedInterceptorEmitter |
| 4 | `[TracedReturn]` | ReturnValue | qyl | Yes (consumed) | TracedInterceptorEmitter |
| 5 | `[OTel]` | Parameter/Property | qyl | No (naming only) | — |
| 6 | `[Meter]` | Class | qyl | Yes | MeterEmitter |
| 7 | `[Counter]` | Method | qyl | Yes | MeterEmitter |
| 8 | `[Histogram]` | Method | qyl | Yes | MeterEmitter |
| 9 | `[UpDownCounter]` | Method | qyl | Yes | MeterEmitter |
| 10 | `[Gauge]` | Method | qyl | Yes | MeterEmitter |
| 11 | `[Tag]` | Parameter | qyl | Yes (consumed) | MeterEmitter |
| 12 | `[AgentTraced]` | Method | qyl | Yes | AgentInterceptorEmitter |
| 13 | `[GeneratedActivitySource]` | Assembly | qyl | Emitted | TracedInterceptorEmitter |
| 14 | `[GeneratedMeter]` | Assembly | qyl | Emitted | MeterEmitter |
| 15 | `[GeneratedCapability]` | Assembly | qyl | Emitted | CapabilityEmitter |
| 16 | `[LoomTool]` | Method | qyl | Yes | LoomToolOutputGenerator |
| 17 | `[LoomContract]` | Class/Struct | qyl | Yes | LoomContractOutputGenerator |
| 18 | `[LoomStep]` | Class | qyl | Yes | LoomStepOutputGenerator |
| 19 | `[LoomWorkflow]` | Class | qyl | Yes | LoomWorkflowOutputGenerator |
| 20 | `[RequiresCapability]` | Method/Class | qyl | Yes (consumed) | LoomToolOutputGenerator |
| 21 | `[RequiresApproval]` | Method/Class | qyl | Yes (consumed) | LoomToolOutputGenerator |
| 22 | `[ToolSideEffect]` | Method/Class | qyl | Yes (consumed) | LoomToolOutputGenerator |
| 23 | `[EmitsStructuredOutput]` | Method/Class | qyl | Yes (consumed) | LoomToolOutputGenerator |
| 24 | `[LoomBudget]` | Method/Class | qyl | Yes (consumed) | LoomToolOutputGenerator |
| 25 | `[DuckDbTable]` | Class/Struct | qyl | Yes | DuckDbEmitter |
| 26 | `[DuckDbColumn]` | Property | qyl | Yes (consumed) | DuckDbEmitter |
| 27 | `[DuckDbIgnore]` | Property | qyl | No (filter) | — |
| 28 | `[McpServer]` | Class | netagents | Yes | OutputGenerator (all sub-emitters) |
| 29 | `[Tool]` | Method | netagents | Yes | DispatchEmitter + SchemaEmitter + OTelEmitter |
| 30 | `[Prompt]` | Method | netagents | Yes | PromptEmitter |
| 31 | `[Resource]` | Method | netagents | Yes | ResourceEmitter |
| 32 | `[McpServerToolType]` | Class | MCP SDK | Yes | ToolManifestEmitter (both repos) |
| 33 | `[McpServerTool]` | Method | MCP SDK | Yes | ToolManifestEmitter (qyl.mcp) |
| 34 | `[AotReflection]` | Class | Roslyn.Utilities | Yes | AotReflectionGenerator |
| 35 | `[AotTest]` | Method | Roslyn.Utilities | No | — |
| 36 | `[AotSafe]` | Class/Method | Roslyn.Utilities | No | — |
| 37 | `[AotUnsafe]` | Class/Method | Roslyn.Utilities | No | — |
| 38 | `[TrimSafe]` | Class/Method | Roslyn.Utilities | No | — |

---

## Generated Output Examples

### What `[Traced]` + `[TracedTag]` + `[TracedReturn]` produces

**Input:**
```csharp
[Traced("MyApp.Orders")]
public class OrderService
{
    [TracedTag("service.region")]
    public string Region { get; set; } = "eu-west-1";

    [return: TracedReturn("order.id", Property = "Id")]
    public async Task<Order> GetOrder(
        [TracedTag] string orderId,
        [TracedTag("customer.tier"), OTel("customer.tier")] string? tier = null) { ... }
}
```

**Generated (`TracedIntercepts.g.cs`):**
```csharp
// <auto-generated/>
#pragma warning disable

using System.Diagnostics;

[assembly: global::Qyl.Instrumentation.GeneratedActivitySourceAttribute("MyApp.Orders")]

namespace Qyl.Instrumentation.Generators
{
    file static class TracedActivitySources
    {
        internal static readonly ActivitySource MyApp_Orders = new("MyApp.Orders");
    }
}

namespace Qyl.Instrumentation.Generators
{
    file static class TracedInterceptors
    {
        // OrderService.cs:8
        [InterceptsLocation(1, "...")]
        public static async Task<Order> Intercept_Traced_0(
            this global::MyNamespace.OrderService @this, string orderId, string? tier)
        {
            using var activity = TracedActivitySources.MyApp_Orders.StartActivity(
                "GetOrder", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("code.filepath", "/src/OrderService.cs");
                activity.SetTag("code.function.name", "GetOrder");
                activity.SetTag("code.namespace", "MyNamespace");
                activity.SetTag("code.lineno", 8);
                activity.SetTag("orderId", orderId);
                if (tier is not null)
                    activity.SetTag("customer.tier", tier);
                activity.SetTag("service.region", @this.Region);
            }
            try
            {
                var result = await @this.GetOrder(orderId, tier);
                activity?.SetTag("order.id", result?.Id);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                ActivityExceptionTelemetry.Record(activity, ex);
                throw;
            }
        }
    }
}
```

---

### What `[Meter]` + `[Counter]` + `[Histogram]` + `[Gauge]` + `[Tag]` produces

**Input:**
```csharp
[Meter("MyApp.Orders")]
public static partial class OrderMetrics
{
    [Counter("orders.created", Unit = "{order}", Description = "Orders created")]
    public static partial void RecordOrderCreated([Tag("status")] string status);

    [Histogram("order.duration", Unit = "ms", Description = "Order processing time")]
    public static partial void RecordDuration(double value, [Tag("type")] string orderType);

    [Gauge("orders.pending", Unit = "{order}", Description = "Pending orders")]
    public static partial void SetPendingCount(long value);
}
```

**Generated (`MeterImplementations.g.cs`):**
```csharp
// <auto-generated/>
#nullable enable

using System.Collections.Generic;
using System.Diagnostics.Metrics;

[assembly: global::Qyl.Instrumentation.GeneratedMeterAttribute("MyApp.Orders")]

namespace MyNamespace
{
    partial class OrderMetrics
    {
        private static readonly Meter _meter = new Meter("MyApp.Orders");

        private static readonly Counter<long> _ordersCreated =
            _meter.CreateCounter<long>("orders.created", "{order}", "Orders created");
        private static readonly Histogram<double> _orderDuration =
            _meter.CreateHistogram<double>("order.duration", "ms", "Order processing time");
        private static long _currentOrdersPending;
        private static readonly ObservableGauge<long> _ordersPending =
            _meter.CreateObservableGauge("orders.pending", "{order}", "Pending orders",
                () => _currentOrdersPending);

        public static partial void RecordOrderCreated(string status)
        {
            _ordersCreated.Add(1, new KeyValuePair<string, object?>("status", status));
        }

        public static partial void RecordDuration(double value, string orderType)
        {
            _orderDuration.Record(value, new KeyValuePair<string, object?>("type", orderType));
        }

        public static partial void SetPendingCount(long value)
            => _currentOrdersPending = value;
    }
}
```

---

### What `[DuckDbTable]` + `[DuckDbColumn]` produces

**Input:**
```csharp
[DuckDbTable("spans", OnConflict = "ON CONFLICT (trace_id, span_id) DO UPDATE SET duration_ns = EXCLUDED.duration_ns")]
public partial record SpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    [DuckDbColumn("parent_span_id")]
    public string? ParentSpanId { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong StartTimeUnixNano { get; init; }
    public long? DurationNs { get; init; }
    [DuckDbIgnore]
    public string? ComputedField { get; init; }
}
```

**Generated (`SpanRecord.DuckDb.g.cs`):**
```csharp
// <auto-generated/>
#nullable enable

using System;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

partial record SpanRecord
{
    public const string TableName = "spans";

    public const string ColumnList = """
        "trace_id", "span_id", "parent_span_id",
        "start_time_unix_nano", "duration_ns"
        """;

    public const int ColumnCount = 5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddParameters(DuckDBCommand cmd, SpanRecord row)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = row.TraceId });
        cmd.Parameters.Add(new DuckDBParameter { Value = row.SpanId });
        cmd.Parameters.Add(new DuckDBParameter { Value = row.ParentSpanId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)row.StartTimeUnixNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = row.DurationNs ?? (object)DBNull.Value });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanRecord MapFromReader(DbDataReader reader)
    {
        return new SpanRecord
        {
            TraceId = reader.GetString(0),
            SpanId = reader.GetString(1),
            ParentSpanId = reader.Col(2).AsString,
            StartTimeUnixNano = reader.Col(3).GetUInt64(0),
            DurationNs = reader.Col(4).AsInt64
        };
    }

    public static string BuildMultiRowInsertSql(int rowCount) { /* ... */ }
}
```

---

### What `[McpServer]` + `[Tool]` produces (netagents)

**Input:**
```csharp
[McpServer("qyl-telemetry")]
public partial class QylTelemetryServer
{
    /// <summary>Search traces by query string.</summary>
    [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
    public async Task<string> SearchTraces(
        [Description("Search query")] string query,
        [Description("Max results (1-100)")] int limit = 25) { ... }
}
```

**Generated (single file with all sub-emitters):**
```csharp
// Dispatch (DispatchEmitter):
public async Task<string> DispatchToolCallAsync(
    string toolName, JsonElement arguments, CancellationToken ct = default)
    => toolName switch
    {
        "search-traces" => await ExecuteTool_SearchTracesAsync(arguments, ct),
        _ => throw new ArgumentException($"Unknown tool: {toolName}", nameof(toolName))
    };

// Schema (SchemaEmitter):
private static readonly byte[] s_schema_SearchTraces = Encoding.UTF8.GetBytes(
    @"{""type"":""object"",""properties"":{""query"":{""type"":""string"",""description"":""Search query""},""limit"":{""type"":""integer"",""description"":""Max results (1-100)""}},""required"":[""query""]}");

// OTel (OTelEmitter):
private static readonly ActivitySource s_activitySource = new("Qyl.Agents", s_instrumentationVersion);
private static readonly Histogram<double> s_requestDuration =
    s_meter.CreateHistogram<double>("gen_ai.client.operation.duration", "s", "Duration of tool execution");
private static readonly Histogram<double> s_mcpOperationDuration =
    s_meter.CreateHistogram<double>("mcp.server.operation.duration", "s", "Duration of MCP server operations");
```

---

### What `[McpServerToolType]` + `[McpServerTool]` produces (qyl.mcp)

**Generated (`QylToolManifest.g.cs`):**
```csharp
namespace Qyl.Generated
{
    internal static class QylToolManifest
    {
        public static readonly Type[] ToolTypes = { typeof(SearchTracesTool), ... };

        public static readonly GeneratedToolDescriptor[] ToolDescriptors =
        [
            new()
            {
                Name = "search_traces",
                MethodName = "SearchTracesAsync",
                DeclaringType = "qyl.mcp.Tools.Traces.SearchTracesTool",
                Title = "Search Traces",
                Description = "Search distributed traces by query...",
                ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
                ReturnType = "string",
            },
        ];

        public static List<AIFunction> CreateTools(IServiceProvider services, Func<Type, bool>? filter = null)
        {
            var tools = new List<AIFunction>();
            if (filter?.Invoke(typeof(SearchTracesTool)) != false)
            {
                var svc = services.GetRequiredService<SearchTracesTool>();
                tools.Add(AIFunctionFactory.Create(svc.SearchTracesAsync,
                    new AIFunctionFactoryOptions { Name = "search_traces" }));
            }
            return tools;
        }
    }
}
```

---

### What `[LoomTool]` + `[RequiresCapability]` + `[LoomBudget]` produces

**Input:**
```csharp
public partial class DiagnosticTools
{
    [LoomTool("analyze_stacktrace", Description = "Analyze a stack trace for root cause",
        Phase = LoomPhase.Detect)]
    [RequiresCapability("code_search")]
    [RequiresApproval]
    [LoomBudget(MaxAttempts = 3, MaxToolCalls = 5, MaxTokens = 8000)]
    public async Task<string> AnalyzeStackTrace(string stackTrace, string? context = null) { ... }
}
```

**Generated (`DiagnosticTools.LoomTools.g.cs`):**
```csharp
public static LoomToolDescriptor AnalyzeStackTraceDescriptor { get; } = new(
    "analyze_stacktrace",
    "Analyze a stack trace for root cause",
    LoomPhase.Detect,
    null, null,
    typeof(DiagnosticTools),
    "AnalyzeStackTrace",
    null,
    new LoomToolParameterDescriptor[]
    {
        new("stackTrace", typeof(string), false, false, null, null, Array.Empty<string>()),
        new("context", typeof(string), true, true, "null", null, Array.Empty<string>()),
    },
    new string[] { "code_search" },
    true,
    ToolSideEffect.None,
    static async (services, args, ct) =>
        (object?)await ((DiagnosticTools)services.GetService(typeof(DiagnosticTools))!)
            .AnalyzeStackTrace((string)args[0]!, (string?)args[1]!)
            .ConfigureAwait(false)
);
```

**Generated (`LoomGeneratedRegistry.g.cs`):**
```csharp
public static partial class LoomGeneratedRegistry
{
    public static IReadOnlyList<LoomToolDescriptor> Tools => new LoomToolDescriptor[]
    {
        DiagnosticTools.AnalyzeStackTraceDescriptor,
    };

    public static IReadOnlyList<LoomCapabilityDescriptor> Capabilities => new LoomCapabilityDescriptor[]
    {
        new("code_search", new string[] { "analyze_stacktrace" }, true,
            new ToolSideEffect[] { ToolSideEffect.None }),
    };
}
```
