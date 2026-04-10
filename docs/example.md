Model Context Protocol 2026 Roadmap and Design‑Principles Mapping
Overview

The Model Context Protocol (MCP) roadmap for 2026 identifies four Priority Areas—transport evolution & scalability, agent communication, governance maturation and enterprise readiness—plus an On‑the‑Horizon list for community‑driven topics. The MCP design principles help guide these efforts by preferring convergence, composability, interoperability, stability, capability, demonstration, pragmatism and standardization when shaping the protocol.

Priority Areas and Design Principle Alignment
Priority Area	Purpose / Key Work	Sample Design Principles Behind It (phrases only)	Notable References
Transport evolution & scalability	Evolve Streamable HTTP to support stateless horizontal scaling (session handling, resumption protocols) and create a .well‑known Server Card to publish server capabilities. No new transports this cycle.	Convergence over choice (use a single transport and avoid fragmentation), Interoperability over optimization (make scaling work across environments), Stability over velocity (extend existing transport rather than churn)	Roadmap; blog; Server Card WG mission: define card format and discovery mechanisms.
Agent communication	Improve the Tasks primitive (SEP‑1686) with retry semantics and expiry policies to support call‑now/fetch‑later patterns.	Composability over specificity (use primitives like tasks instead of bespoke APIs), Demonstration over deliberation (iterate based on production feedback), Stability over velocity (ship experimental features and iterate gradually)	Roadmap; SEP‑1686 abstract describes tasks with task IDs for querying status/results.
Governance maturation	Formalize contributor ladder and delegation models to avoid bottleneck from requiring full Core Maintainer review.	Standardization over innovation (standardize governance), Pragmatism over purity (remove bottlenecks for efficiency)	Roadmap; SEP‑1302 defines working/interest groups and their roles; SEP‑2085 establishes succession and amendment procedures.
Enterprise readiness	Address enterprise needs like audit trails, SSO‑integrated auth (e.g., Cross‑App Access), gateway & proxy patterns and configuration portability.	Pragmatism over purity (focus on real‑world enterprise needs), Interoperability over optimization (ensure solutions work across enterprise systems), Capability over compensation (improve security rather than patch later)	Roadmap; Cross‑App Access uses identity assertion to allow a calling app to authorize third‑party tools
modelcontextprotocol.io
.
On‑the‑Horizon Topics
Topic	Summary / Goals	Key Docs & Citations
Triggers & event‑driven updates	Standardize a callback mechanism (webhooks or similar) so servers can proactively notify clients when data is available; define subscription lifecycle and ordering guarantees across transports. The Triggers and Events WG charter emphasises server‑initiated notifications with defined ordering and coordination with transport and agent working groups.	Roadmap; charter.
Result‑type improvements	Explore streamed results (incremental output for interactive scenarios) and reference‑based results (clients fetch large payloads when needed), touching both transport and schema.	Roadmap.
Security & authorization	Enhance least‑privilege scopes and guidance to avoid OAuth mix‑up attacks. Current sponsored extensions include SEP‑1932 (DPoP) and SEP‑1933 (Workload Identity Federation) that strengthen token security. SEP‑1932 binds access tokens to a client‑held key pair to prevent token replay, while SEP‑1933 standardizes use of platform‑provided credentials and workload‑issued JWTs to streamline identity federation.	Roadmap; SEP‑1932 summary; SEP‑1933 summary.
Extensions ecosystem	Mature the experimental extension tracks (e.g., ext‑auth and ext‑apps) and investigate a Skills primitive for composed capabilities; strengthen registry support so that experiments can transition into standards.	Roadmap.
How Design Principles Support the Roadmap
Convergence over choice: The roadmap explicitly states that no new official transports will be added in 2026, focusing on improving Streamable HTTP and server cards. This convergent approach reduces fragmentation.
Composability over specificity: Agent communication improvements build on the tasks primitive rather than adding bespoke mechanisms; result‑type work explores streamed and reference‑based outputs as composable patterns.
Interoperability over optimization: Transport evolution emphasises stateless scaling and discovery so that servers can work with load balancers and proxies, while identity federation and DPoP aim for secure cross‑service interoperability.
Stability over velocity: The governance and enterprise areas aim to provide durable structures (contributor ladders, delegation models, succession procedures) and to avoid “experimental churn” by moving carefully.
Capability over compensation: Security extensions (DPoP and workload identity federation) add robust authorization mechanisms to avoid future vulnerabilities.
Demonstration over deliberation: Tasks improvements come after real‑world deployments and feedback; the extension ecosystem encourages experimentation before standardization.
Pragmatism over purity: Enterprise readiness recognizes real deployment challenges like audit trails and SSO integration rather than idealized designs.
Standardization over innovation: The roadmap channels new ideas through working groups, SEPs, and extensions before adopting them into the core spec, ensuring proposals are well‑tested and widely supported.
Getting Involved









ANcpLua.Roslyn.Utilities interesting for qyl

## 1. Attributes

### Full Ecosystem Attribute Inventory (38 total across 5 repos)

#### QYL Instrumentation — Marker Attributes (12)

| Attribute | Namespace | Zweck |
|-----------|-----------|-------|
| `[Traced]` | `Qyl.Instrumentation.Instrumentation` | Klasse/Methode für OTel-Tracing markieren |
| `[NoTrace]` | `Qyl.Instrumentation.Instrumentation` | Methode von Class-Level-Tracing ausschließen |
| `[AgentTraced]` | `Qyl.Instrumentation.Instrumentation` | Agent-Invocation-Tracing |
| `[Counter]` | `Qyl.Instrumentation.Instrumentation` | Counter-Metrik |
| `[Histogram]` | `Qyl.Instrumentation.Instrumentation` | Histogram-Metrik |
| `[UpDownCounter]` | `Qyl.Instrumentation.Instrumentation` | Up-Down-Counter-Metrik |
| `[Gauge]` | `Qyl.Instrumentation.Instrumentation` | Gauge-Metrik |
| `[Meter]` | `Qyl.Instrumentation.Instrumentation` | Meter-Klasse markieren |
| `[Tag]` | `Qyl.Instrumentation.Instrumentation` | Parameter als Span-Tag |
| `[TracedTag]` | `Qyl.Instrumentation.Instrumentation` | Expliziter Span-Tag |
| `[TracedReturn]` | `Qyl.Instrumentation.Instrumentation` | Return-Value als Span-Attribut |
| `[OTel]` | `Qyl.Instrumentation.Instrumentation` | Semconv-Key-Supplier |

Generator: `qyl/src/qyl.instrumentation.generators/`

#### QYL Loom Workflow — Marker Attributes (9)

| Attribute | Namespace | Zweck |
|-----------|-----------|-------|
| `[LoomTool]` | `Qyl.Instrumentation.Instrumentation.Loom` | Methode als Workflow-Tool |
| `[LoomContract]` | `Qyl.Instrumentation.Instrumentation.Loom` | Typ als Workflow-Contract |
| `[LoomStep]` | `Qyl.Instrumentation.Instrumentation.Loom` | Klasse als Workflow-Step |
| `[LoomWorkflow]` | `Qyl.Instrumentation.Instrumentation.Loom` | Klasse als kompletter Workflow |
| `[RequiresCapability]` | `Qyl.Instrumentation.Instrumentation.Loom` | Benötigte Capabilities deklarieren |
| `[RequiresApproval]` | `Qyl.Instrumentation.Instrumentation.Loom` | Approval-Gate |
| `[ToolSideEffect]` | `Qyl.Instrumentation.Instrumentation.Loom` | Side-Effects deklarieren |
| `[EmitsStructuredOutput]` | `Qyl.Instrumentation.Instrumentation.Loom` | Structured-Output-Typ |
| `[LoomBudget]` | `Qyl.Instrumentation.Instrumentation.Loom` | Execution-Budget setzen |

Source: `qyl/src/qyl.instrumentation/Instrumentation/Loom/LoomAttributes.cs`

#### QYL DuckDB Storage — Marker Attributes (3)

| Attribute | Namespace | Zweck |
|-----------|-----------|-------|
| `[DuckDbTable]` | `Qyl.Collector.Storage` | Typ für DuckDB-Helpers generieren |
| `[DuckDbColumn]` | `Qyl.Collector.Storage` | Property → DuckDB-Column-Mapping |
| `[DuckDbIgnore]` | `Qyl.Collector.Storage` | Property von Mapping ausschließen |

Generator: `qyl/src/qyl.collector.storage.generators/DuckDbInsertGenerator.cs`
Attributes emitted via PostInitializationOutput from `DuckDbAttributes.cs`

#### Netagents MCP — Marker Attributes (4)

| Attribute | Namespace | Zweck |
|-----------|-----------|-------|
| `[McpServer]` | `Qyl.Agents` | Partial Class als MCP-Server |
| `[Tool]` | `Qyl.Agents` | Methode als AI-callable Tool |
| `[Prompt]` | `Qyl.Agents` | Methode als MCP-Prompt |
| `[Resource]` | `Qyl.Agents` | Methode als MCP-Resource |

Generator: `netagents/src/Qyl.Agents.Generator/McpServerGenerator.cs`

#### QYL Contracts — Generated Semconv Output (3)

| Klasse | Namespace | Semconv |
|--------|-----------|---------|
| `DbAttributes` | `qyl.contracts.Attributes` | OTel 1.40.0 Database |
| `GenAiAttributes` | `qyl.contracts.Attributes` | OTel 1.40.0 GenAI |
| `McpAttributes` | `qyl.contracts.Attributes` | OTel 1.40.0 MCP |

Source: `qyl/src/qyl.contracts/Attributes/`

#### ANcpLua.Helpers.Utilities — AotReflection (7)

| Attribute / Typ | Rolle | Zweck |
|-----------------|-------|-------|
| `[AotReflection]` | Marker | Triggert den Generator |
| `ClassMetadata` | Generated | Klassen-Reflection-Daten |
| `MethodMetadata` | Generated | Methoden-Reflection-Daten |
| `PropertyMetadata` | Generated | Property-Reflection-Daten |
| `FieldMetadata` | Generated | Field-Reflection-Daten |
| `ConstructorMetadata` | Generated | Konstruktor-Reflection-Daten |
| `ParameterMetadata` | Generated | Parameter-Reflection-Daten |

Generator: `ANcpLua.Helpers.Utilities/src/ANcpLua.AotReflection/AotReflectionGenerator.cs`

### Other attributes in this repo

**Custom attribute definitions:**
- `src/ANcpLua.AotReflection.Attributes/AotReflectionAttribute.cs` — marks types for AOT metadata generation
- `src/ANcpLua.Roslyn.Utilities.Testing.Aot/` — `AotTestAttribute`, `AotSafeAttribute`, `AotUnsafeAttribute`, `TrimSafeAttribute`, `TrimTestAttribute`, `TrimUnsafeAttribute`
- `src/ANcpLua.Roslyn.Utilities.Testing.AgentTesting/BitNetAttribute.cs` — marks tests needing BitNet fixture

**Attribute analysis/extraction:**
- `src/ANcpLua.Roslyn.Utilities/AttributeExtensions.cs` — GetConstructorArgument, GetNamedArgument from AttributeData
- `src/ANcpLua.Roslyn.Utilities/SymbolExtensions.cs` — HasAttribute, GetAttribute, GetAttributes

**Metadata models (runtime reflection):**
- `src/ANcpLua.AotReflection.Attributes/` — `ClassMetadata`, `PropertyMetadata`, `MethodMetadata`, `FieldMetadata`, `ConstructorMetadata`, `ParameterMetadata`

**Polyfills (20+ netstandard2.0 backports):**
- `Polyfills/TrimAttributes/` — DynamicallyAccessedMembers, RequiresUnreferencedCode, RequiresDynamicCode
- `Polyfills/LanguageFeatures/` — RequiredMember, CallerArgumentExpression, CompilerFeatureRequired, ParamCollection
- `Polyfills/DiagnosticAttributes/` — NullableAttributes, MemberNotNull, StackTraceHidden, ExperimentalAttribute

## 2. OpenTelemetry

**Core analysis infrastructure:**
- `src/ANcpLua.Roslyn.Utilities/Contexts/OTelContext.cs` — cached type symbols for Activity, ActivitySource, Meter, Counter, Histogram, TracerProvider, MeterProvider, etc.
- `src/ANcpLua.Roslyn.Utilities/Contexts/DeprecatedOtelAttributes.cs` — 50+ deprecated semconv attribute mappings (v1.40.0)
- `src/ANcpLua.Roslyn.Utilities/Models/OTelEnums.cs` — SpanKind, SpanStatusCode matching OTel protobuf spec

**Instrumentation helpers (in Testing):**
- `Testing/Instrumentation/ActivityInstrumentation.cs` — ActivitySourceFactory, 100+ semconv constants, ScopedActivity, GenAI attributes
- `Testing/Instrumentation/MetricsInstrumentation.cs` — MeterFactory, MetricUnits, DurationRecorder, ActiveTracker
- `Testing/Instrumentation/LoggingConventions.cs` — standardized EventId ranges, 100+ LogTags constants
- `Testing/Instrumentation/LogEnricherInfrastructure.cs` — TraceContextEnricher, GenAiContextEnricher
- `Testing/Instrumentation/DataClassificationHelpers.cs` — PII/Secret redaction framework
- `src/ANcpLua.Roslyn.Utilities/Telemetry/AssemblyVersionExtensions.cs` — version extraction for telemetry

**No qyl code lives in this repo.** The `Qyl.Agents` references in `workflows/nuget-publish.yml` and `ActivityCollector` docs point to an external repo.

## 3. MCP

**No MCP implementation code.** Only `.mcp.json` configuring client connections (JetBrains, NuGet MCP servers). No ModelContextProtocol library references in any csproj.

## 4. Agents

**Full agent testing library** (`src/ANcpLua.Roslyn.Utilities.Testing.AgentTesting/`):
- `AGUITestServer.cs` — in-memory test server with AG-UI endpoint
- `FakeAgentBase.cs`, `FakeEchoAgent.cs`, `FakeTextStreamingAgent.cs`, `FakeMultiMessageAgent.cs`, `FakeReplayAgent.cs`, `DelegatingTestAgent.cs` — test doubles for Microsoft.Agents.AI
- `FakeChatClient.cs` — IChatClient test double with scripted responses
- `FakeHttpMessageHandler.cs` — URL-pattern matching HTTP handler with SSE support
- `SseResponseParser.cs` — SSE response parsing
- `ActivityCollector.cs` — captures Activity instances for telemetry assertions
- `BitNetFixture.cs` / `BitNetTestGroup.cs` — llama.cpp/BitNet LLM test infrastructure
- `ChatMessageExtensions.cs`, `AsyncEnumerableExtensions.cs` — message builders and async helpers
- `TestOutputAdapter.cs`, `LogRecord.cs`, `RecordedRequest.cs` — test output/logging infrastructure

**Dependencies:** Microsoft.Agents.AI, Microsoft.Agents.AI.Hosting.AGUI.AspNetCore, Microsoft.Extensions.AI, OpenAI, xunit.v3

## AotReflection — Generator-Attributes Architecture

**Roslyn source generator that replaces runtime reflection with compile-time generated metadata.** The whole point is AOT/trimming compatibility: `typeof(T).GetProperties()` and friends get trimmed away or fail in NativeAOT, so this generator does the work at compile time instead.

### Generator (`src/ANcpLua.AotReflection/`)

- `AotReflectionGenerator.cs` — the `[Generator]` entry point (IIncrementalGenerator)
- `DiagnosticDescriptors.cs` — analyzer diagnostics for the generator

**Extractors** (compile-time symbol analysis → immutable models):

| Extractor | Output Model | What it extracts |
|-----------|-------------|-----------------|
| `TypeExtractor` | `TypeModel` | Type metadata (name, namespace, accessibility, generics) |
| `PropertyExtractor` | `PropertyModel` | Properties with getter/setter info |
| `MethodExtractor` | `MethodModel` | Methods with parameters and return types |
| `FieldExtractor` | `FieldModel` | Fields with type and default values |
| `ConstructorExtractor` | `ConstructorModel` | Constructors with parameters |
| `ParameterExtractor` | `ParameterModel` | Parameter types, defaults, attributes |
| `DeclarationChainExtractor` | `TypeDeclarationModel` | Nesting chain for partial types |
| `LiteralFormatter` | (helper) | Default value literal formatting |

**Code Generators** (models → emitted C# source):

| Generator | Emits |
|-----------|-------|
| `ClassMetadataGenerator` | Orchestrates full output, top-level structure |
| `PropertyCodeGenerator` | Getter/setter delegate expressions |
| `MethodCodeGenerator` | Invoker delegate expressions |
| `FieldCodeGenerator` | Field access delegate expressions |
| `OutputGenerator` | Final `.g.cs` file assembly |
| `GenerationHelpers` | Shared emit logic (indent, escaping, types) |

**Models** (value-equatable for incremental caching):
- `TypeModel`, `PropertyModel`, `MethodModel`, `FieldModel`, `ConstructorModel`, `ParameterModel`
- `TypeDeclarationModel` — nesting chain
- `AotReflectionOptions` — controls what gets included (properties, methods, fields, constructors, inherited, private)
- `ReturnKind` — void/value/task/valuetask classification
- `AccessibilityExtensions` — maps Roslyn accessibility to C# keywords

### Attributes Contract (`src/ANcpLua.AotReflection.Attributes/`)

Runtime metadata types that the generator populates. Shipped as a separate package so consumers only depend on the thin contracts, not the generator.

| Type | Purpose |
|------|---------|
| `AotReflectionAttribute` | Marks types for metadata generation |
| `ClassMetadata` | Generated type-level metadata container |
| `PropertyMetadata` | Property name, type, getter/setter delegates |
| `MethodMetadata` | Method name, return type, invoker delegate |
| `FieldMetadata` | Field name, type, access delegates |
| `ConstructorMetadata` | Constructor parameters, invoker delegate |
| `ParameterMetadata` | Parameter name, type, default value, attributes |

### Pipeline

```
[AotReflection] attribute on a class
        ↓
Extractors (compile-time symbol analysis)
  TypeExtractor → TypeModel
  PropertyExtractor → PropertyModel
  MethodExtractor → MethodModel
  FieldExtractor → FieldModel
  ConstructorExtractor → ConstructorModel
  ParameterExtractor → ParameterModel
  DeclarationChainExtractor → TypeDeclarationModel (nesting)
  LiteralFormatter → default value literals
        ↓
Code Generators (emit C# source)
  ClassMetadataGenerator → orchestrates output
  PropertyCodeGenerator → getter/setter delegates
  MethodCodeGenerator → invoker delegates
  FieldCodeGenerator → field access delegates
  OutputGenerator → final .g.cs file
  GenerationHelpers → shared emit logic
        ↓
Runtime metadata (in Attributes package)
  ClassMetadata, PropertyMetadata, MethodMetadata,
  FieldMetadata, ConstructorMetadata, ParameterMetadata
```

So instead of `typeof(Foo).GetMethod("Bar").Invoke(...)` at runtime, you get a generated `FooMetadata` class with strongly-typed delegates — no reflection, no trimming warnings, NativeAOT safe.

### Testing.Aot (`src/ANcpLua.Roslyn.Utilities.Testing.Aot/`)

Companion package — attributes and helpers to verify that your code actually survives `PublishAot=true` and `PublishTrimmed=true` builds. Includes `AotTestAttribute`, `AotSafeAttribute`, `AotUnsafeAttribute`, `TrimSafeAttribute`, `TrimTestAttribute`, `TrimUnsafeAttribute`, and feature switch infrastructure.

## For qyl.instrumentation.generators & Qyl.Agents.Generator

**Matching DSL** (`Matching/SymbolMatch.cs`, `InvocationMatch.cs`) — fluent matchers like `Match.Method().Named("Foo").OnType("Bar").ReturnsTask()` and `Invoke.Method().Named("X").WithArguments(...)`. Replaces stringly-typed symbol comparisons in analyzers. This is the right way to detect patterns like `[Traced]` or `[AgentTraced]` methods.

**DiagnosticFlow<T>** (`DiagnosticFlow.cs`) — railway-oriented pipeline: carries value + accumulated diagnostics through `Then()`/`Select()`/`Warn()`/`Error()` chains. Value-equatable so incremental caching works. Your generators should be using this instead of manual if/else diagnostic reporting.

**IncrementalValuesProviderExtensions** — `AddSource()`, `AddSources()`, `GroupBy()`, `Distinct()`, `Take()`, `Skip()`, plus DiagnosticFlow integration. Eliminates the boilerplate in every `Initialize()` method.

**Sources package** (`ANcpLua.Roslyn.Utilities.Sources`) — embeds all utilities as **internal source** into generator assemblies. Generators can't load NuGet DLLs at runtime — this is how they consume the library.

**GeneratorTestEngine** + **GeneratorCachingReport** — fluent test builder with caching validation (compares two runs to detect cache hits/misses). Your generators should be testing caching behavior.

**CodeGeneration.cs** — `IndentedStringBuilder` with `BeginBlock()`/`EndBlock()` scopes, plus `GeneratedCodeHelpers` for standard headers, attributes, pragmas.

**DataFlowAnalyzer<T>** (`FlowAnalysis/`) — abstract forward dataflow over `ControlFlowGraph`. Worklist algorithm with lattice operations. Could power disposal analysis or taint tracking in qyl.instrumentation.

## For qyl.loom & Qyl.Agents (runtime)

**Result<T>** (`Result.cs`) — general-purpose Ok/Fail with `Match()`, `Select()`, `Then()`, `Where()`. Unlike DiagnosticFlow, this has no Roslyn dependency — fits agent/app domain code.

**AsyncSequenceExtensions** (`Async/`) — `ToReadOnlyListAsync()`, `Tap()`, `WhereNotNull()`, chunking for `IAsyncEnumerable<T>`. Directly useful for streaming agent responses in qyl.loom.

**ValueStringBuilder** — ref struct using `Span<char>` → `ArrayPool<char>`. Zero-alloc for small strings. Good for hot paths in loom's HTTP layer.

**Boxes** (`Boxes.cs`) — cached boxed values (bool, int[-1..10], char[0..127]). Eliminates boxing allocations for common values in tag lists / attribute dictionaries.

## For all downstream

**OverloadFinder** — finds method overloads by parameter types. Your CancellationToken analyzer uses this to suggest "use the async overload".

**SemanticGuard** — fluent validation that accumulates all violations into DiagnosticFlow. Replaces nested if-chains in generator validation.

**CodeStylePreferences** — reads EditorConfig settings so generated code respects the project's style (expression bodies, file-scoped namespaces, etc.).

**TypeCache<TEnum>** — O(1) enum-indexed symbol lookup. Your Context classes (OTelContext, etc.) are built on this.

**Polyfills package** — source-only `Index`/`Range`, `init`, `required`, trim attributes for netstandard2.0. Consumed by every generator targeting netstandard2.0.

## 5. Downstream consumers already using this repo

### qyl.mcp.generators (compile-time)

`qyl.mcp.generators` references `ANcpLua.Roslyn.Utilities.Sources` (source-embedded) and already uses:
- **EquatableArray<T>** in `ToolManifestGenerator.cs` and `ToolManifestModels.cs` for incremental caching
- **ToEquatableArray()** in `ToolManifestAnalyzer.cs`

`ToolManifestEmitter.cs` uses raw `StringBuilder` with hardcoded indentation — should be using **IndentedStringBuilder** + **GeneratedCodeHelpers**.

### qyl.mcp runtime

Has `ANcpLua.Roslyn.Utilities` as a PackageReference + global using but doesn't actually use it in runtime code yet. 77+ tool files use plain `StringBuilder` where `IndentedStringBuilder` would help.

### Qyl.Agents.Generator (netagents)

Consumes `ANcpLua.Roslyn.Utilities.Sources` for the same generator infrastructure. The convergence plan targets unifying netagents' `[McpServer]`/`[Tool]` generator with qyl.mcp's `ToolManifestGenerator` — the Matching DSL, DiagnosticFlow, and GeneratorTestEngine become critical when that merge happens.

### qyl.loom

Tight compile-time MCP surface (`LoomGodAnalyzerServer.cs`, 3 tools, 1 prompt). Good fit for the current utilities. `Result<T>` and `AsyncSequenceExtensions` are directly useful for its runtime code.

## 6. Where ANcpLua.Roslyn.Utilities fits in the MCP landscape

This repo provides **compile-time server authoring** infrastructure. That's the gap in the broader ecosystem:

| Concern | Who owns it | ANcpLua.Roslyn.Utilities role |
|---------|-------------|-------------------------------|
| Compile-time tool discovery & codegen | netagents (`Qyl.Agents.Generator`) + qyl.mcp (`ToolManifestGenerator`) | **Primary foundation** — EquatableArray, DiagnosticFlow, Matching DSL, IndentedStringBuilder, Sources package, GeneratorTestEngine |
| Transport (Streamable HTTP, stdio) | Official C# SDK + qyl.mcp hosting | None — runtime concern |
| Runtime orchestration & approval flows | MAF (`DefaultMcpToolHandler`, `InvokeMcpToolExecutor`) | None — MAF's strength |
| Azure Functions MCP hosting | MAF (`BuiltInFunctions.cs`) | None — hosting concern |
| OpenAI/Foundry protocol bridging | MAF (`mcp_list_tools`, `mcp_call` items) | None — protocol concern |
| OTel for MCP spans | qyl.instrumentation + qyl.instrumentation.generators | **Direct** — OTelContext, DeprecatedOtelAttributes (v1.40.0), semconv constants |
| Agent test doubles | This repo (`Testing.AgentTesting`) | **Primary** — FakeAgent*, FakeChatClient, AGUITestServer, ActivityCollector |
| AOT/trim safety | This repo (`AotReflection` + `Testing.Aot`) | **Primary** — compile-time metadata generation, publish verification |

MAF's MCP code is strongest at orchestration, transport, hosted execution, and protocol bridging. It is not strongest at compile-time server authoring or large-scale tool inventory management. That's exactly where this repo's generator infrastructure fills the gap.

**Key constraint:** MAF's declarative bridge (`McpServerToolExtensions.cs`) only supports `AnonymousConnection`. For authenticated MCP product surfaces (qyl.mcp with JWT/Keycloak/OAuth2), that's a real limitation — the compile-time authoring path through netagents + official SDK is the only viable route for production auth.# Attribute Catalog — Complete Inventory Across All Repos

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
# Convergence Plan: AOT MCP Server for Claude

**Date:** 2026-04-08
**Scope:** Unify netagents (Qyl.Agents) and qyl.mcp generator stacks into a single compile-time MCP server framework that is Claude-native, AOT-first, and OTel-correct.

---

## Current State Map

### Three stacks, two generators, zero shared code

| Dimension | netagents (Qyl.Agents) | qyl.mcp (ToolManifestGenerator) | Official C# SDK (v1.2.0) |
|-----------|------------------------|--------------------------------|--------------------------|
| **Trigger** | `[McpServer]` + `[Tool]` (custom) | `[McpServerToolType]` + `[McpServerTool]` (official SDK) | `[McpServerToolType]` + `[McpServerTool]` |
| **Code gen** | Full: dispatch, schema, OTel, JSON context, SKILL.md, llms.txt | Minimal: type array + delegate factory | None (runtime reflection) |
| **Transport** | stdio only | stdio + Streamable HTTP | stdio + Streamable HTTP |
| **Auth** | None | JWT, Keycloak, OAuth2 | OAuth 2.1 + DCR |
| **OTel** | Per-tool spans + metrics (bugs: deprecated `gen_ai.system`, invalid `server.name`) | Host-level message/request filters | None built-in |
| **AOT** | Strong (primitives direct, complex type warnings) | Partial (AIFunctionFactory uses reflection) | Supported (`PublishAot=true`) |
| **DI** | None (instance-based) | Full (IServiceProvider) | Full (IServiceProvider) |
| **Resources** | Yes (`[Resource]`) | No | Yes |
| **Prompts** | Yes (`[Prompt]`) | No | Yes |
| **Tasks** | Yes (`IMcpTaskStore`, MCPEXP001) | No | Experimental |
| **Validation** | Comprehensive (types, nesting, duplicates, diagnostics) | Minimal (syntax only) | Runtime |
| **Consumers** | qyl.loom (1 server, 3 tools) | qyl.mcp (77 tool files) | MAF samples, ecosystem |

### What Datzi sees

Two systems. Zero shared code. netagents has the better compiler. qyl.mcp has the better host. Neither alone is Claude-ready.

---

## Anthropic Constraints (non-negotiable)

These come from official Anthropic docs, the MCP spec (2025-11-25), and the C# SDK (v1.2.0).

### Transport

| Claude client | stdio | Streamable HTTP | SSE (deprecated) |
|---------------|-------|-----------------|-------------------|
| Claude Code | Yes | Yes | Yes (legacy) |
| Claude Desktop | Yes (local) | Yes (Connectors) | Yes (legacy) |
| Claude Web UI | **No** | **Yes (required)** | Yes (legacy) |
| Messages API (MCP Connector) | **No** | **Yes (required)** | Yes (legacy) |
| Claude iOS/Android | **No** | **Yes (inherited)** | Yes (legacy) |

**Implication:** stdio-only servers (current netagents) cannot reach Claude Web UI, the Messages API, or mobile. Streamable HTTP is mandatory for any production MCP server.

### Authentication

- OAuth 2.1 with Dynamic Client Registration (DCR) is the standard
- OAuth metadata discovery: RFC 9728 (`.well-known/oauth-protected-resource`) first, RFC 8414 fallback
- Bearer tokens via `authorization_token` field in server config
- OAuth callback: `https://claude.ai/api/mcp/auth_callback`
- All remote connections originate from Anthropic's cloud infrastructure

### Tool Design (Claude-native UX)

| Rule | Detail |
|------|--------|
| **Names** | `^[a-zA-Z0-9_-]{1,64}$}`. Prefix with domain (e.g. `qyl_search_traces`). MCP surfaces as `mcp__<server>__<tool>` |
| **Descriptions** | 3-4+ sentences. What it does, when to use it, when NOT to use it, parameter meanings, caveats. **Single most important factor for Claude tool selection quality** |
| **Schemas** | JSON Schema with `description` on every property. Use `enum` for constrained values. Mark required vs optional explicitly |
| **Input examples** | `input_examples` field (array) for complex/nested tools |
| **Strict mode** | `strict: true` guarantees Claude's calls always match schema |
| **Structured output** | `outputSchema` (JSON Schema) + `structuredContent` field. Must still include `text` for backward compat |
| **Errors** | Return `is_error: true` with actionable message. Never hang. Never crash |
| **Responses** | High-signal only. Stable identifiers (slugs, UUIDs). Only fields Claude needs for next reasoning step |

### API Connector Limitations

The Messages API MCP Connector (beta header `mcp-client-2025-11-20`) **only supports tools**. No prompts. No resources. Servers targeting the API connector must design around tools only.

### SSE Deprecation

C# SDK v1.2.0 disabled legacy SSE endpoints by default. `MapMcp()` no longer maps `/sse` and `/message`. Opt-in via `EnableLegacySse = true` (marked `[Obsolete]`).

Sources:
- [Build custom connectors](https://support.claude.com/en/articles/11503834-build-custom-connectors-via-remote-mcp-servers)
- [MCP connector API docs](https://platform.claude.com/docs/en/agents-and-tools/mcp-connector)
- [Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)
- [C# SDK v1.2.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v1.2.0)

---

## OTel Semantic Conventions (non-negotiable, no shims)

All conventions below are current as of OTel semconv 1.40+. The MCP-specific conventions were added in early 2026 (PR #2083). No shims, no custom attributes where a convention exists.

### MCP Server Span (transport level)

Span kind: `SERVER`. Span name: `{mcp.method.name} {target}` (e.g. `tools/call get_weather`).

| Attribute | Type | Req | Stability |
|-----------|------|-----|-----------|
| `mcp.method.name` | string | Required | Development |
| `error.type` | string | Cond. Required (on error) | Stable |
| `gen_ai.tool.name` | string | Cond. Required | Development |
| `gen_ai.prompt.name` | string | Cond. Required | Development |
| `jsonrpc.request.id` | string | Cond. Required | Development |
| `mcp.resource.uri` | string | Cond. Required | Development |
| `rpc.response.status_code` | string | Cond. Required | Release Candidate |
| `gen_ai.operation.name` | string | Recommended | Development |
| `mcp.protocol.version` | string | Recommended | Development |
| `mcp.session.id` | string | Recommended | Development |
| `jsonrpc.protocol.version` | string | Recommended | Development |
| `network.protocol.name` | string | Recommended | Stable |
| `network.transport` | string | Recommended | Stable |
| `client.address` | string | Recommended | Stable |
| `gen_ai.tool.call.arguments` | any | Opt-In | Development |
| `gen_ai.tool.call.result` | any | Opt-In | Development |

### GenAI Tool Execution Span (business logic level)

Span kind: `INTERNAL`. Span name: `execute_tool {gen_ai.tool.name}`.

| Attribute | Type | Req | Stability |
|-----------|------|-----|-----------|
| `gen_ai.operation.name` | string | Required (`execute_tool`) | Development |
| `error.type` | string | Cond. Required (on error) | Stable |
| `gen_ai.tool.name` | string | Recommended | Development |
| `gen_ai.tool.type` | string | Recommended (`function`) | Development |
| `gen_ai.tool.call.id` | string | Recommended | Development |
| `gen_ai.tool.description` | string | Recommended | Development |

### Metrics

| Metric | Instrument | Unit | Level |
|--------|-----------|------|-------|
| `mcp.server.operation.duration` | Histogram | `s` | Transport |
| `mcp.server.session.duration` | Histogram | `s` | Transport |
| `gen_ai.client.operation.duration` | Histogram | `s` | Tool execution |

MCP bucket boundaries: `[0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300]`
GenAI bucket boundaries: `[0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12, 10.24, 20.48, 40.96, 81.92]`

### Recommended Span Hierarchy

```
HTTP SERVER span (auto, ASP.NET Core Kestrel — do NOT emit manually)
  └─ MCP SERVER span: "tools/call get_weather"
       ├─ mcp.method.name = "tools/call"
       ├─ gen_ai.tool.name = "get_weather"
       ├─ gen_ai.operation.name = "execute_tool"
       ├─ mcp.protocol.version = "2025-06-18"
       ├─ jsonrpc.request.id = "42"
       ├─ jsonrpc.protocol.version = "2.0"
       └─ INTERNAL span: "execute_tool get_weather"
            ├─ gen_ai.operation.name = "execute_tool"
            ├─ gen_ai.tool.name = "get_weather"
            ├─ gen_ai.tool.type = "function"
            ├─ gen_ai.tool.call.id = "call_abc123"
            └─ error.type (only on failure)
```

### Context Propagation

Inject/extract W3C Trace Context (`traceparent`, `tracestate`) and Baggage via MCP `params._meta` property bag.

### Bugs in Current netagents OTel

| File | Line | Issue | Fix |
|------|------|-------|-----|
| `DispatchEmitter.cs` | 70 | Emits `gen_ai.system` — **deprecated** | Remove entirely. `gen_ai.provider.name` is not applicable to execute_tool spans |
| `DispatchEmitter.cs` | 71 | Emits `server.name` — **not a semconv attribute** | Remove entirely |
| `DispatchEmitter.cs` | — | Missing `gen_ai.tool.call.id` | Add (Recommended) |
| `DispatchEmitter.cs` | — | Missing `gen_ai.tool.description` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `mcp.protocol.version` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `mcp.session.id` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `jsonrpc.protocol.version` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `rpc.response.status_code` on errors | Add (Cond. Required) |
| `OTelEmitter.cs` | — | Only emits `gen_ai.client.operation.duration` | Also emit `mcp.server.operation.duration` |

Sources:
- [MCP Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/)
- [MCP Registry Attributes](https://opentelemetry.io/docs/specs/semconv/registry/attributes/mcp/)
- [GenAI Spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/)
- [MCP Conventions PR #2083](https://github.com/open-telemetry/semantic-conventions/pull/2083)

---

## Generator Comparison: What Each Does Uniquely

### netagents does (not in qyl.mcp)

1. **Source-generated dispatch** — `DispatchToolCallAsync()` with switch expression routing by tool name, per-tool private methods, direct `JsonElement` accessors for primitives
2. **Source-generated JSON schemas** — static `byte[]` arrays, compile-time construction, zero runtime cost
3. **Source-generated JSON contexts** — `[JsonSourceGenerationOptions]` partial class, only when complex types exist
4. **Per-tool OTel spans** — ActivitySource + Histogram emitted in generated code (bugs noted above)
5. **Resource endpoints** — `[Resource]` methods for content reads
6. **Prompt templates** — `[Prompt]` methods with parameter extraction
7. **SKILL.md + llms.txt generation** — agent platform metadata
8. **Comprehensive validation** — 10+ diagnostics (QA0003–QA0010), parameter type checking, duplicate detection, nesting validation
9. **Task support** — `IMcpTaskStore` for long-running operations (MCPEXP001)
10. **Tool hints** — `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` as `ToolHint` enum with Unset/True/False semantics

### qyl.mcp does (not in netagents)

1. **Streamable HTTP transport** — full ASP.NET Core hosting with `MapMcp()`
2. **Authentication** — JWT bearer, Keycloak, OAuth2 middleware
3. **Message filtering** — incoming/outgoing JSON-RPC middleware pipeline
4. **Request filtering** — per-tool access control, scope injection, rate limiting
5. **Health checks** — `/healthz` endpoint
6. **Landing page** — HTML + JSON server discovery endpoint
7. **Tool filtering** — `Func<Type, bool>?` predicate in `CreateTools()`
8. **Full DI** — `IServiceProvider.GetRequiredService<T>()` per tool type
9. **Manifest endpoints** — `/mcp.json` and `/llms.txt` via HTTP routes
10. **Host-level OTel** — transport-layer instrumentation via SDK message/request filters

### Where they overlap

- Both use `IIncrementalGenerator` + `ForAttributeWithMetadataName`
- Both produce value-equatable models for incremental caching
- Both emit a single generated file per compilation
- Both avoid runtime type discovery for tool registration
- Both target .NET 10 / C# 14

---

## Architecture: Unified AOT MCP Server

### Design Principles

1. **Compile-time everything** — schemas, dispatch, OTel, JSON contexts generated at build. Zero runtime reflection.
2. **Claude-native first** — tool descriptions enforced, structured output supported, Anthropic transport requirements met.
3. **OTel-correct** — MCP semconv 2026, no deprecated attributes, no custom attributes where conventions exist.
4. **Official SDK as substrate** — use `ModelContextProtocol` NuGet for transport/hosting. Generate the tool surface, don't replace the host.
5. **AOT by default** — `PublishAot=true` must work without warnings. Source-generated JSON for all serialization paths.

### The Key Insight

netagents should **not** replace the official SDK's hosting. It should **generate the tool implementations** that plug into the official SDK's host.

```
                    ┌──────────────────────────────────────┐
                    │   Official C# MCP SDK (v1.2.0)       │
                    │   ─ Streamable HTTP transport         │
                    │   ─ stdio transport                   │
                    │   ─ OAuth 2.1 + DCR                   │
                    │   ─ Session management                │
                    │   ─ MapMcp() ASP.NET Core             │
                    └───────────────┬──────────────────────┘
                                    │ consumes
                    ┌───────────────▼──────────────────────┐
                    │   Qyl.Agents.Generator (compile-time) │
                    │   ─ [McpServer] + [Tool] attributes   │
                    │   ─ Source-generated McpServerTool[]   │
                    │   ─ Source-generated JSON schemas      │
                    │   ─ Source-generated dispatch          │
                    │   ─ Source-generated OTel (MCP semconv)│
                    │   ─ Source-generated JSON contexts     │
                    │   ─ Validation diagnostics             │
                    └───────────────┬──────────────────────┘
                                    │ implements
                    ┌───────────────▼──────────────────────┐
                    │   User's MCP Server Class              │
                    │                                        │
                    │   [McpServer("qyl-telemetry")]          │
                    │   partial class QylTelemetryServer      │
                    │   {                                     │
                    │     [Tool] SearchTraces(...)             │
                    │     [Tool] GetSpan(...)                  │
                    │     [Resource] GetDashboard(...)         │
                    │   }                                     │
                    └────────────────────────────────────────┘
```

### Generated Output: What Changes

**Current** (netagents generates its own `IMcpServer` interface + `McpHost` runtime):
```csharp
// Generated: implements custom IMcpServer
partial class MyServer : IMcpServer
{
    public Task<string> DispatchToolCallAsync(string toolName, JsonElement args, CancellationToken ct)
        => toolName switch { "my_tool" => ExecuteTool_MyToolAsync(args, ct), _ => throw ... };
}

// Runtime: custom McpHost reads stdin, writes stdout
await McpHost.RunStdioAsync(new MyServer());
```

**Target** (netagents generates `McpServerTool[]` that plug into official SDK):
```csharp
// Generated: source-generated McpServerTool instances (no reflection)
partial class MyServer
{
    public static McpServerTool[] CreateMcpTools(IServiceProvider services)
    {
        var instance = services.GetRequiredService<MyServer>();
        return
        [
            McpServerTool.Create(
                name: "my_tool",
                description: "Does the thing. Use when you need X. Do NOT use for Y.",
                inputSchema: s_schema_MyTool, // static byte[], compile-time
                handler: (args, ct) => instance.DispatchTool_MyToolAsync(args, ct),
                inputSchemaStrict: true,
                readOnly: true,
                idempotent: true),
        ];
    }

    // Generated: AOT-safe dispatch with direct JsonElement accessors
    private async Task<McpToolResponse> DispatchTool_MyToolAsync(
        JsonElement args, CancellationToken ct)
    {
        using var activity = s_activitySource.StartActivity(
            "execute_tool my_tool", ActivityKind.Internal);
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", "my_tool");
        activity?.SetTag("gen_ai.tool.type", "function");

        var param1 = args.GetProperty("query").GetString()!;
        var result = await MyTool(param1, ct);
        return McpToolResponse.Success(result);
    }
}

// Hosting: official SDK does transport
var builder = Host.CreateApplicationBuilder();
builder.Services.AddSingleton<MyServer>();
builder.Services.AddMcpServer()
    .WithStreamableHttpTransport()
    .WithTools(MyServer.CreateMcpTools);
await builder.Build().RunAsync();
```

### What the Generator Emits (per server class)

| Output | Purpose | AOT-safe |
|--------|---------|----------|
| `CreateMcpTools(IServiceProvider)` | Static factory returning `McpServerTool[]` | Yes — no reflection |
| `DispatchTool_{Name}Async(JsonElement, CancellationToken)` | Per-tool dispatch with direct `JsonElement` accessors | Yes — primitives direct, complex via source-gen JSON |
| `s_schema_{Name}` | Static `byte[]` JSON Schema per tool | Yes — compile-time |
| `{ClassName}JsonContext` | `[JsonSourceGenerationOptions]` partial context | Yes — source-gen JSON |
| `s_activitySource` | `ActivitySource("Qyl.Agents")` | Yes |
| `s_mcpDuration` | `Histogram<double>("mcp.server.operation.duration")` | Yes |
| `s_toolDuration` | `Histogram<double>("gen_ai.client.operation.duration")` | Yes |
| `GetServerInfo()` | Static metadata for server discovery | Yes |
| `GetSkillMarkdown()` | SKILL.md content as string | Yes |
| `GetLlmsTxt()` | llms.txt content as string | Yes |

### OTel Emission (corrected)

**Transport-level span** (emitted in protocol handler, not per-tool):
```csharp
using var activity = s_activitySource.StartActivity(
    $"{mcpMethod} {target}", ActivityKind.Server);
activity?.SetTag("mcp.method.name", mcpMethod);           // Required
activity?.SetTag("mcp.protocol.version", "2025-06-18");    // Recommended
activity?.SetTag("mcp.session.id", sessionId);             // Recommended
activity?.SetTag("jsonrpc.request.id", requestId);         // Cond. Required
activity?.SetTag("jsonrpc.protocol.version", "2.0");       // Recommended
// On tools/call:
activity?.SetTag("gen_ai.tool.name", toolName);            // Cond. Required
activity?.SetTag("gen_ai.operation.name", "execute_tool");  // Recommended
// On error:
activity?.SetTag("error.type", exception.GetType().Name);  // Cond. Required
activity?.SetTag("rpc.response.status_code", errorCode);   // Cond. Required
```

**Tool-level span** (emitted in generated dispatch):
```csharp
using var activity = s_activitySource.StartActivity(
    $"execute_tool {toolName}", ActivityKind.Internal);
activity?.SetTag("gen_ai.operation.name", "execute_tool");  // Required
activity?.SetTag("gen_ai.tool.name", toolName);             // Recommended
activity?.SetTag("gen_ai.tool.type", "function");           // Recommended
activity?.SetTag("gen_ai.tool.call.id", callId);            // Recommended
activity?.SetTag("gen_ai.tool.description", description);   // Recommended
// NO gen_ai.system (deprecated)
// NO gen_ai.provider.name (not applicable to tool execution)
// NO server.name (not a semconv attribute)
```

### Validation Diagnostics

| ID | Severity | Message |
|----|----------|---------|
| QA0001 | Error | `[McpServer]` class must be partial |
| QA0002 | Error | `[McpServer]` class must not be static or generic |
| QA0003 | Warning | Tool has no hints set (ReadOnly, Destructive, Idempotent, OpenWorld) |
| QA0004 | Error | `[Tool]` method found outside `[McpServer]` class |
| QA0005 | Error | Duplicate tool name in server |
| QA0006 | Error | Duplicate resource URI in server |
| QA0007 | Error | Duplicate prompt name in server |
| QA0008 | Warning | Parameter missing `[Description]` attribute |
| QA0009 | Error | Tool method must not be static or generic |
| QA0010 | Error | Unsupported parameter type |
| **QA0011** | **Warning** | **Tool description is fewer than 50 characters** (new — Claude quality) |
| **QA0012** | **Warning** | **Parameter description missing or fewer than 10 characters** (new — Claude quality) |
| **QA0013** | **Info** | **Consider adding `input_examples` for complex tool schemas** (new — Claude quality) |

---

## What This Means for Each Project

### For qyl.mcp (77 tool files)

**No immediate migration required.** qyl.mcp's `ToolManifestGenerator` + official SDK hosting works. The path is:

1. **Phase 1:** Fix OTel in qyl.mcp's request filters to use MCP semconv attributes (add `mcp.method.name`, `mcp.protocol.version`, etc.)
2. **Phase 2:** When netagents v0.3.0 ships with the unified generator, qyl.mcp can optionally migrate tool classes from `[McpServerToolType]`/`[McpServerTool]` to `[McpServer]`/`[Tool]` to gain compile-time schemas, per-tool OTel, and AOT-safe dispatch
3. **Phase 3:** qyl.mcp's custom `ToolManifestGenerator` becomes unnecessary — netagents generates the same output but with more features

The migration is **incremental**. One tool class at a time. Both generators can coexist during transition.

### For qyl.loom (1 server, 3 tools)

**Already on netagents.** Benefits from all fixes immediately:

1. OTel attributes corrected (remove deprecated `gen_ai.system`, add MCP semconv)
2. Streamable HTTP transport (currently stdio-only via `McpHost`)
3. OAuth 2.1 support for remote access from Claude Web UI

### For netagents itself

**This is the work:**

1. Fix OTel bugs in `DispatchEmitter.cs` and `OTelEmitter.cs`
2. Generate `McpServerTool[]` factory method instead of (or alongside) custom `IMcpServer`
3. Add HTTP hosting integration (`McpEndpoints.MapMcpServer<T>()` for ASP.NET Core)
4. Add Claude-quality diagnostics (QA0011, QA0012, QA0013)
5. Add structured output schema support (`outputSchema` + `structuredContent`)
6. Keep `McpHost.RunStdioAsync()` for backward compat and local dev

---

## Ecosystem Fit

### Where netagents sits vs MAF vs official SDK

From the MAF MCP HITS analysis ([src.md](src.md), [sample.md](sample.md)):

- **MAF** is strongest at orchestration, transport, hosted execution, and protocol bridging. It is not strongest at compile-time server authoring.
- **Official C# SDK** is the canonical transport/server substrate. Stable, AOT-compatible, maintained by Microsoft.
- **netagents** is the compile-time authoring layer. It generates what the official SDK consumes.

These are complementary, not competing:

```
MAF (orchestration) ──consumes──▶ MCP servers
Official SDK (transport) ──hosts──▶ MCP servers
netagents (authoring) ──generates──▶ MCP server tools
```

### Anthropic ecosystem alignment

| Anthropic priority | netagents alignment |
|-------------------|---------------------|
| Streamable HTTP | Phase 2 (via official SDK integration) |
| OAuth 2.1 + DCR | Phase 2 (via ASP.NET Core middleware) |
| Tool descriptions | Phase 1 (QA0011 diagnostic enforces 50+ char descriptions) |
| Structured output | Phase 2 (generate `outputSchema` from return type) |
| AOT | Already strong, gets stronger with generated `McpServerTool[]` factory |
| OTel | Phase 1 (fix deprecated attributes, add MCP semconv) |

### What "Claude-native" means in practice

Not "works with Claude." Every MCP server works with Claude. "Claude-native" means:

1. **Tool descriptions read like documentation** — Claude selects tools based on descriptions, not names. 3-4 sentences minimum. Enforce at compile time.
2. **Structured output schemas** — Claude can return structured data validated against server-declared schemas. Generate these from C# return types.
3. **Error responses are actionable** — `is_error: true` with "what went wrong, what to do next." Generate error wrapping in dispatch.
4. **Streaming where it matters** — long-running tools use Tasks (MCPEXP001) so Claude doesn't time out waiting.
5. **Transport negotiation** — Streamable HTTP for remote, stdio for local. Same server binary, different host configuration.
6. **OAuth 2.1** — Claude Web UI and API connector authenticate via DCR. Not optional for remote servers.

---

## Summary

| What | Status | Action |
|------|--------|--------|
| netagents generator | Strong foundation, bugs in OTel | Fix OTel, add `McpServerTool[]` factory, add Claude diagnostics |
| netagents runtime | stdio-only, custom protocol | Keep for local dev. Add official SDK integration for HTTP |
| qyl.mcp generator | Works but minimal | Keep until netagents can replace it. Then migrate incrementally |
| qyl.mcp hosting | Production-ready | No change. Official SDK + ASP.NET Core stays |
| qyl.loom | Already on netagents | Benefits from all fixes. Add HTTP transport for Claude Web UI |
| OTel | Bugs: deprecated attributes, missing MCP semconv | Fix in Phase 1. No shims, no custom attributes |
| AOT | Strong for primitives | Full AOT with source-generated JSON contexts |
| Claude UX | Not enforced | Phase 1: diagnostics. Phase 2: structured output |

One generator. Official SDK for transport. OTel-correct. Claude-native. AOT by default. No shims.
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
````markdown
# v1.2.0

This release improves stateless HTTP transport defaults and documentation, but it also includes a **breaking behavioral
change** that is being treated as a server reliability fix rather than a major-version bump.

The two most important changes are:

1. Legacy SSE endpoints are now disabled by default.
2. The 2-argument `RequestContext` constructor is now obsolete.

## Breaking Changes

Refer to the [C# SDK Versioning](https://csharp.sdk.modelcontextprotocol.io/versioning.html) documentation for details
on versioning and breaking change policies.

### 1. Disable legacy SSE by default

`MapMcp()` no longer maps `/sse` and `/message` endpoints by default. Servers whose clients connect via SSE will find
those endpoints removed unless legacy SSE is explicitly re-enabled.

#### What changed

If your clients connect to a `/sse` endpoint such as:

```text
https://my-server.example.com/sse
```

then they were using the legacy SSE transport, assuming the server was not running in `Stateless` mode.

The `/sse` and `/message` endpoints are now disabled by default:

- `EnableLegacySse` defaults to `false`
- `EnableLegacySse` is marked `[Obsolete]`
- the diagnostic is `MCP9004`

That means upgrading the server SDK without updating clients can break existing SSE connections.

#### Client-side migration

Change the client `Endpoint` from the `/sse` path to the root MCP endpoint, which is the same URL your server passes to
`MapMcp()`.

```csharp
// Before (legacy SSE):
Endpoint = new Uri("https://my-server.example.com/sse")

// After (Streamable HTTP):
Endpoint = new Uri("https://my-server.example.com/")
```

With the default `HttpTransportMode.AutoDetect`, the client automatically tries Streamable HTTP first. If you already
know the server supports it, you can explicitly set:

```csharp
TransportMode = HttpTransportMode.StreamableHttp
```

#### Server-side migration

If you previously relied on `/sse` being mapped automatically, you now need:

```csharp
EnableLegacySse = true
```

That suppresses the `MCP9004` warning and keeps the SSE endpoints available.

The recommended path is:

1. migrate all clients to Streamable HTTP
2. remove `EnableLegacySse`

#### Transition period

If some clients still require SSE while others have already moved to Streamable HTTP, you can temporarily support both
transports by using:

```csharp
EnableLegacySse = true
Stateless = false
```

In that configuration, `MapMcp()` serves both transports at the same time:

- Streamable HTTP on the root MCP endpoint
- SSE on `/sse`
- POST messages on `/message`

Once all clients have migrated, remove `EnableLegacySse` and optionally switch to `Stateless = true`.

#### Why SSE is disabled by default

Legacy SSE is opt-in only because it does not provide built-in HTTP-level backpressure.

The legacy SSE transport uses two separate channels:

- clients POST JSON-RPC messages to `/message`
- clients receive responses through a long-lived GET SSE stream on `/sse`

The POST endpoint returns `202 Accepted` immediately after queuing the message. It does **not** wait for the handler to
complete.

That means:

- there is no HTTP-level backpressure on handler concurrency
- a client can send unlimited POST requests to `/message`
- each request can spawn a concurrent handler
- there is no built-in per-client concurrency limit

The GET stream does provide session lifetime bounds. Handler cancellation tokens are linked to the GET request’s
`HttpContext.RequestAborted`, so when the client disconnects the SSE stream, all in-flight handlers are cancelled.

This is similar to a connection-bound lifetime model, but unlike systems such as SignalR, it does not provide a
per-client concurrency limit. It only ensures cleanup on disconnect.

### 2. Obsolete 2-arg `RequestContext` constructor

The `RequestContext(McpServer, JsonRpcRequest)` constructor is now `[Obsolete]` and produces build warnings with
diagnostic `MCP9003`.

The `Params` property also changes from:

```csharp
TParams?
```

to:

```csharp
TParams
```

#### Migration

Use the new 3-argument constructor instead:

```csharp
new RequestContext(server, request, parameters)
```

## Notable changes

- Support specifying `OutputSchema` independently of return type for tools returning `CallToolResult`
- Fix `WithMeta` + `WithProgress` causing tool invocation failure
- Fix per-task DI scope creation in `ExecuteToolAsTaskAsync` to prevent `ObjectDisposedException`
- Route `SendRequestAsync` logic through outgoing message filters
- Add stateless documentation and disable legacy SSE by default
- Update documentation URLs to the new vanity domain
- Align roots terminology with the MCP spec clarification

## Full changelog

https://github.com/modelcontextprotocol/csharp-sdk/compare/v1.1.0...v1.2.0
```` e## netagents — What It Is

A **.NET package manager + compile-time MCP server source generator**. Two halves:

1. **CLI (`netagents`)** — manages reusable AI tool packages ("skills") across agent tools (Claude Code, Cursor, Codex, VS Code). Commands: `init`, `install`, `add`, `remove`, `sync`, `list`. Writes agent-specific config files so one skill definition works everywhere.

2. **Source generator (`Qyl.Agents.Generator`)** — you mark a class with `[McpServer]` and methods with `[Tool]`, and at compile time it generates: dispatch routing, JSON schemas, OTel spans, metadata, and serialization context. Zero runtime reflection.

---

## Usage in qyl (`/Users/ancplua/qyl`)

**Limited but strategic** — only `qyl.loom` uses it:

- **Packages**: `Qyl.Agents`, `Qyl.Agents.Abstractions`, `Qyl.Agents.Generator` all at v0.2.0
- **Single MCP server**: `LoomGodAnalyzerServer` in `src/qyl.loom/Agents/` — decorated with `[McpServer]`, `[Tool]`, and `[Prompt]` attributes
- **3 tools**: `loom_get_issue_insight`, `loom_start_fix_run`, `loom_review_pull_request`
- **Hosting**: `LoomGodAnalyzerHostingExtensions.cs` uses `Qyl.Agents.Hosting`
- No `agents.toml`/`agents.lock` — the CLI package management side isn't used, only the source generator

---

## Usage in qyl.mcp (`/Users/ancplua/qyl.mcp`)

**None.** qyl.mcp has a completely independent stack:

- Uses `ModelContextProtocol` NuGet package with `[McpServerToolType]` / `[McpServerTool]` attributes (not Qyl.Agents)
- Has its own custom Roslyn source generator (`qyl.mcp.generators/ToolManifestGenerator.cs`) that emits a `QylToolManifest` class
- 77 tool files use this separate pattern
- Zero references to Qyl.Agents anywhere

---

I ASSUME netagents located in /Users/ancplua/netagents/src/NetAgents
/Users/ancplua/netagents/src/Qyl.Agents
/Users/ancplua/netagents/src/Qyl.Agents.Abstractions
/Users/ancplua/netagents/src/Qyl.Agents.Generator
/Users/ancplua/netagents/src/Qyl.ChatKit should power the qyl.loom analyze codebase feature tool(if its not built or named differnelty that was the plan as anyone asking whats the code issue we let this monster inmemory create skills, mcpservers, tools with /Users/ancplua/qyl.mcp/src/qyl.mcp.generators/ToolManifestGenerator.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Models/ToolManifestModels.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Models
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Emitters
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Analyzers/ToolManifestAnalyzer.cs
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/Analyzers
/Users/ancplua/qyl.mcp/src/qyl.mcp.generators/qyl.mcp.generators.csproj


) call from qyl.mcp via source generation but qyl.mcp doesn't use its AOT ment migration codebase  at all — it has its own parallel tool system built on the official ModelContextProtocol SDK. from


I have located the current source code of MAF as of 5th april at the following location path:
/Users/ancplua/agent-framework/dotnet
Microsoft Agent Framework → unified runtime + workflows + state + enterprise system
also called MAF which absorbed:
AutoGen        → agent interaction (HOW agents talk)
Semantic Kernel → agent capabilities (WHAT agents can do)

AutoGen had:
flexibility focus ↑ UP
chaos focus ↑ UP
control focus ↓ DOWN

Semantic Kernel world:
structure focus ↑ UP
enterprise focus ↑ UP
freedom focus ↓ DOWN

Bonus mentions in eng and tests:

- [eng/verify-samples/WorkflowSamples.cs](/Users/ancplua/agent-framework/dotnet/eng/verify-samples/WorkflowSamples.cs)
- [eng/verify-samples/AgentsSamples.cs](/Users/ancplua/agent-framework/dotnet/eng/verify-samples/AgentsSamples.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/DefaultMcpToolHandlerTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/DefaultMcpToolHandlerTests.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests/Microsoft.Agents.AI.Workflows.Declarative.Mcp.UnitTests.csproj)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/DurableAgentFunctionMetadataTransformerTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/DurableAgentFunctionMetadataTransformerTests.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/FunctionMetadataFactoryTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.UnitTests/FunctionMetadataFactoryTests.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/WorkflowSamplesValidation.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/WorkflowSamplesValidation.cs)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests.csproj)
- [tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/SamplesValidation.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Hosting.AzureFunctions.IntegrationTests/SamplesValidation.cs)
- [tests/Microsoft.Agents.AI.GitHub.Copilot.IntegrationTests/GitHubCopilotAgentTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.GitHub.Copilot.IntegrationTests/GitHubCopilotAgentTests.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/InvokeToolWorkflowTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/InvokeToolWorkflowTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpToolWithApproval.yaml](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpToolWithApproval.yaml)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpTool.yaml](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Workflows/InvokeMcpTool.yaml)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Framework/IntegrationTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Framework/IntegrationTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests.csproj](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests/Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests.csproj)
- [tests/Microsoft.Agents.AI.GitHub.Copilot.UnitTests/GitHubCopilotAgentTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.GitHub.Copilot.UnitTests/GitHubCopilotAgentTests.cs)
- [tests/Microsoft.Agents.AI.Foundry.UnitTests/AzureAIProjectChatClientExtensionsTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Foundry.UnitTests/AzureAIProjectChatClientExtensionsTests.cs)
- [tests/Microsoft.Agents.AI.Foundry.UnitTests/TestDataUtil.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Foundry.UnitTests/TestDataUtil.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputResponseTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputResponseTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputRequestTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/Events/ExternalInputRequestTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/ObjectModel/InvokeMcpToolExecutorTest.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.Declarative.UnitTests/ObjectModel/InvokeMcpToolExecutorTest.cs)
- [tests/Microsoft.Agents.AI.Workflows.UnitTests/WorkflowHostSmokeTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Workflows.UnitTests/WorkflowHostSmokeTests.cs)
- [tests/Microsoft.Agents.AI.Declarative.UnitTests/AgentBotElementYamlTests.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Declarative.UnitTests/AgentBotElementYamlTests.cs)
- [tests/Microsoft.Agents.AI.Declarative.UnitTests/PromptAgents.cs](/Users/ancplua/agent-framework/dotnet/tests/Microsoft.Agents.AI.Declarative.UnitTests/PromptAgents.cs)
# Sentry Seer — Competitive Intelligence for Loom

> Compiled from official Sentry documentation, Sentry Kapa.ai bot interactions, and Sentry Dashboards/OTel docs audit.
> Date: 2026-04-08 (v7 — final)

---

## 1. What Seer Is (and Sentry's Broader AI Surface)

Seer is Sentry's **closed-source** AI debugging agent and a **paid add-on** to the Sentry subscription. It accesses Sentry's telemetry (issues, traces, logs, profiles) and linked GitHub codebases **as agentic resources** — not copy-pasted context blobs, but tool-exposed data sources that Seer queries on-demand during analysis (analogous to how code search is exposed as a tool). This is a critical architectural detail: Seer's context window isn't pre-stuffed, it's dynamically populated via tool calls.

**Seer's four capabilities:**

- **Autofix** — end-to-end RCA → solution → code fix pipeline
- **PR Creation** — pushes generated fixes to GitHub (requires separate Seer GitHub app)
- **Coding Agent Delegation** — hands off to external agents (e.g. Cursor)
- **Code Review** — pre-merge error prediction on GitHub PRs

**Other AI features in Sentry (not Seer-specific, included in base product):**

- **Issue Summary** — auto-generated overview of an issue highlighting what's wrong, potential cause, and insights from trace-connected issues. Uses event + issue-level metadata. Not an agent — a single-shot summarization pass.
- **Query Assistant** — NL→query translation for traces and spans data. Users describe what they want in natural language, Seer translates to structured queries and finds relevant compute metric samples. (This partially addresses the NL→query gap noted in §12 — but only for traces/spans, not logs.)
- **AI Summaries** — summarization of User Feedback submissions and Session Replays to surface common patterns. Separate from Issue Summary.

All generative AI features (Seer + the above) can be disabled org-wide via the `Show Generative AI Features` toggle in org settings.

---

## 2. Architecture: The Autofix Pipeline

Seer's Autofix is a **three-stage** sequential pipeline:

```
┌─────────────────────┐
│  Root Cause Analysis │  ← analyzes issue + code + telemetry
├─────────────────────┤
│ Solution Identific.  │  ← proposes fix steps, user can edit/remove/add
├─────────────────────┤
│   Code Generation    │  ← generates diffs, optionally opens PR
└─────────────────────┘
```

### Loom Comparison

Loom runs a **five-stage** pipeline:

```
Context Gathering → Root Cause Analysis → Solution Planning → Diff Generation → Confidence Scoring
```

Key structural differences:

| Aspect | Seer (3-stage) | Loom (5-stage) |
|--------|---------------|----------------|
| Context gathering | Implicit (built into RCA) | Explicit first stage |
| Confidence scoring | `output_confidence_score` + `proceed_confidence_score` on steps | Dedicated final stage |
| Persistence | Sentry's internal DB, exposed via API | `AutofixStepRecord` entries in DuckDB |
| Approval flow | `stopping_point` per-request | `PolicyGate` (AutoApply/RequireReview/DryRun) |
| User interaction | Mid-flow chat + comment threads | Background-to-conversation handoff via SSE |

### Stopping Point ↔ PolicyGate Mapping

| Seer `stopping_point` | Loom `PolicyGate` equivalent |
|------------------------|------------------------------|
| `root_cause` (default) | `DryRun` |
| `solution` | `RequireReview` (partial) |
| `code_changes` | `RequireReview` (full) |
| `open_pr` | `AutoApply` |

---

## 3. Seer's Agentic Codebase Tooling

Beyond consuming Sentry telemetry, Seer has **direct agentic access to codebases** with the following confirmed capabilities:

| Tool | Description |
|------|-------------|
| Grep-like search | Executes `ripgrep`-style searches across repository files |
| Documentation parsing | Reads and interprets project documentation |
| Commit history analysis | Traces and analyzes git commit history for recent changes |
| Multi-repo breaking change detection | Examines multiple repositories to catch cross-service breaking changes |
| Direct file modification | Can modify files directly when generating fixes |

This is architecturally significant: Seer operates as a **tool-using agent** with resource access, not a context-stuffed prompt. The API response confirms this — the `Retrieve Seer Issue Fix State` endpoint shows `progress` messages like `"Looking at src/seer/automation/autofix/tools/tools.py in getsentry/seer..."`, indicating real-time tool invocations during analysis.

### Implication for Loom

Loom's MCP projection model (agent isolation via MCP tool boundaries) is the same architectural pattern. The difference: Seer's tools are proprietary and internal. Loom's tools are exposed via the qyl MCP server (54+ tools) and fully inspectable.

---

## 4. Automated Scanning & Fix Flow

Seer has two automation tiers beyond manual "Find Root Cause":

### Tier 1: Automated Issue Scanning

When enabled, Seer continuously monitors all incoming issues and:

- Evaluates actionability (fixability score)
- Augments Slack/email alerts with AI analysis summaries
- Highlights the most actionable issues to reduce alert noise

### Tier 2: Automated Fixes

When "Automated Fixes" is enabled on top of scanning:

- Seer auto-triggers full Autofix pipeline (RCA → solution → code) without manual intervention
- Drafts solutions in the background
- **Nothing merges without human approval** — the guardrail is at the PR merge step, not the analysis step

### Auto-Trigger Conditions (from docs)

- Agent configured for background handoff
- Issue has 10+ events captured
- Medium-or-above fixability score

### Real-World Evidence

Seer has been used on Sentry's own codebase. In one documented case, Seer opened a PR in the `getsentry/sentry` repo to fix an exception caused by unhandled `None` values — the team reviewed the diff and merged it. This "Seer fixing itself" anecdote is used in their marketing.

### Customer Quote (Curai)

> "It's no longer one dev, one PR. I'm running Seer across all our issues in parallel. If a fix is off, no big deal—reject it, give more context, try again. Iteration is cheap, and it's saving my team days."
> — Neil Wang, Engineering Manager at Curai

### Sweet Spot Use Cases (from Sentry marketing)

- **Quick wins:** Type errors, null dereferences, missing keys, unhandled exceptions
- **Complex cross-service:** Issues involving multiple services talking to each other (Seer's trace-awareness gives it an edge here)
- **Performance:** Slow N+1 queries, detected via spans/profiles
- **Frontend → Backend:** e.g. `TypeError` in React component traced back to missing null check in API response

---

## 5. Data Sources Seer Consumes

Seer is **trace-aware** and builds connected trees across services:

| Data Source | Details |
|-------------|---------|
| Issue details | Error messages, stack traces, event metadata |
| Tracing data | Distributed traces, span trees |
| Structured logs | Beta — trace-connected logs via OTel SDK, CLI, or log drains |
| Performance data | Profiles and performance metrics |
| Session health | Session status (healthy/crashed/errored/abnormal), user counts, release-correlated health trends |
| Web Vitals | LCP, INP, CLS, FCP, TTFB — Performance Score (0–100) per page, Opportunity scoring weighted by traffic |
| Frontend assets | JS/CSS/image/font load duration, transfer size, render-blocking status via Resource Timing API |
| MCP telemetry | Tool calls, resource access, prompt usage, transport distribution, per-client traffic breakdown |
| Codebases | Linked GitHub repos (cloud only), multi-repo for distributed services |
| User feedback | Interactive mid-flow guidance |
| Rules files | Auto-parses `.cursorrules`, Windsurf, Cline, `CLAUDE.md` |

### Architectural Note: Seer Has Zero Independent Ingestion

Seer is a **pure consumer** — it has no ingestion pipeline of its own. It sits on top of whatever Sentry's collector layer already captured and queries the unified store on-demand via tool calls during Autofix runs. Vector, Fluent Bit, SDKs, OTLP direct, log drains — Seer doesn't care how data arrived. This is both a strength (zero ingestion work for the Seer team) and a constraint: **Seer's RCA quality is capped by whatever Sentry's collector preserves.** If Sentry samples, drops, or applies retention limits to data, Seer never sees what was lost.

### Relay and PII Scrubbing

All data flowing into Sentry passes through **Relay**, which applies PII scrubbing before events are stored. Advanced Data Scrubbing rules (mask, hash, replace, or remove sensitive fields) take precedence. Seer and the MCP server only ever see the **already-scrubbed version** of events. This means:

- Seer's RCA context may be missing information that was scrubbed (e.g. request bodies, user IDs, query parameters containing PII)
- There is no documented mechanism for Seer to know *what* was scrubbed or *whether* the scrubbed content was relevant to the root cause
- Overly aggressive scrubbing rules can silently degrade RCA quality with no feedback loop

### Sentry's Flywheel Strategy

Sentry's CTO has noted that every additional type of connected data "pays huge dividends" for Seer's debugging accuracy. The strategic play: Seer's quality becomes the upsell argument for sending *all* observability data (errors + traces + logs + profiles) to Sentry, not just errors. Logs are still in beta, meaning Sentry is actively expanding the surface area Seer can draw from to tighten this lock-in loop.

### Implication for Loom

qyl owns the full stack (ingestion via OTel collector → DuckDB storage → Loom RCA), so Loom has no equivalent "collector ceiling" — it queries exactly what qyl stored, with no intermediate sampling/retention layer it doesn't control. PII handling is also under the operator's control, not a third-party scrubbing layer that silently removes RCA-relevant data.

### Ingestion Paths (How Data Reaches Sentry → Seer)

```
App → Sentry SDK → Sentry (direct — primary path for errors, traces, performance)
App → OTel SDK → Sentry OTLP endpoint (direct OTLP export)
App → OTel Collector → Sentry OTLP endpoint (collector pipeline)
App → Vector → Sentry OTLP endpoint (log/trace forwarding pipeline)
App → Fluent Bit → Sentry OTLP endpoint (log/trace forwarding pipeline)
Platform → Log & Trace Drains → Sentry (Vercel, Cloudflare, Heroku, Supabase)
```

**Fluent Bit OTLP config reference:**
```yaml
pipeline:
  outputs:
    - name: opentelemetry
      match: "*"
      host: {ORG_INGEST_DOMAIN}
      port: 443
      logs_uri: /api/{PROJECT_ID}/integration/otlp/v1/logs
      tls: on
      tls.verify: on
      header:
        - x-sentry-auth sentry sentry_key={PUBLIC_KEY}
```

Once data lands in Sentry (regardless of path), Seer automatically uses it alongside other telemetry. The richer the data, the better the RCA.

### AI-Powered Log Analysis Integration Points

- **Sentry CLI** (`sentry-cli logs`) — pipe log data into AI tools
- **Sentry MCP Server** — Model Context Protocol bridge for NL queries from Claude/Cursor/VS Code
- **Seer** — automatic consumption during Autofix

---

## 6. API Surface

Seer exposes **three endpoints** (all marked experimental):

### 6.1 Start Seer Issue Fix

```
POST /api/0/organizations/{org}/issues/{issue_id}/autofix/
```

The process **runs asynchronously** — the POST returns immediately with a `run_id`, and you poll state via the GET endpoint (§6.2). Per the official docs, the issue fix process can: identify the root cause, propose a solution, generate code changes, and create a pull request with the fix. If no `stopping_point` is provided, it defaults to `root_cause` only.

**Body parameters (all optional):**

| Parameter | Type | Purpose |
|-----------|------|---------|
| `event_id` | string | Pin analysis to specific event (defaults to "recommended event") |
| `instruction` | string | Free-text NL guidance for the fix process |
| `pr_to_comment_on_url` | string | Existing PR URL where Seer should post comments |
| `stopping_point` | enum | `root_cause` \| `solution` \| `code_changes` \| `open_pr` |

**Auth:** Bearer token, `event:write` or `event:admin`
**Response:** `202` with `{ "run_id": 12345 }`

### 6.2 Retrieve Seer Issue Fix State

```
GET /api/0/organizations/{org}/issues/{issue_id}/autofix/
```

**Auth:** Bearer token, `event:read` / `event:write` / `event:admin`

**Response shape (under `autofix`):**

- `run_id`, `status` (e.g. `COMPLETED`), `updated_at`, `last_triggered_at`, `completed_at`
- `request` — original trigger context (org/project IDs, repos, tags, `options.auto_run_source`)
- `steps[]` — each step contains:
  - `id`, `index`, `key` (e.g. `root_cause_analysis_processing`, `root_cause_analysis`)
  - `status`, `type`, `title`
  - `progress[]` — timestamped messages (INFO type)
  - `insights[]`
  - `output_confidence_score`, `proceed_confidence_score`
  - `active_comment_thread`, `agent_comment_thread`, `queued_user_messages`
- `causes[]` (on RCA step):
  - `description`, `relevant_repos[]`, `reproduction_urls[]`
  - `root_cause_reproduction[]` — timeline items with `code_snippet_and_analysis`, `relevant_code_file` (path + repo), `timeline_item_type` (`human_action` | `internal_code`), `is_most_important_event`
- `codebases` — keyed by external repo ID, with `file_changes[]`, `is_readable`, `is_writeable`
- `repositories[]` — full repo metadata (integration ID, URL, provider, default branch, read/write)
- `coding_agents` — object (empty when no delegation active)

### 6.3 List Seer AI Models

```
GET /api/0/seer/models/
```

Region-specific: `us.sentry.io` or `de.sentry.io`
Docs claim no auth required, but list bearer token auth. No response schema documented (just `.`).

---

## 7. Webhook System

Seven webhook event types via `Sentry-Hook-Resource: seer`:

### Lifecycle Events

| Phase | Started | Completed |
|-------|---------|-----------|
| Root Cause Analysis | `seer.root_cause_started` | `seer.root_cause_completed` |
| Solution Generation | `seer.solution_started` | `seer.solution_completed` |
| Code Generation | `seer.coding_started` | `seer.coding_completed` |
| PR Creation | — | `seer.pr_created` |

### Common Fields (all events)

- `action` — event type string
- `data.run_id` — correlates all events in a single run
- `data.group_id` — Sentry issue ID
- `actor` — always `{ id: "sentry", name: "Sentry", type: "application" }`
- `installation.uuid`

### Payload Shapes by Event

**`root_cause_completed`:**
```
data.root_cause.description  — string
data.root_cause.steps[]      — timeline items:
  .title
  .code_snippet_and_analysis
  .timeline_item_type          ("human_action" | "internal_code")
  .relevant_code_file          { file_path, repo_name }
  .is_most_important_event     boolean
```

**`solution_completed`:**
```
data.solution.description  — string
data.solution.steps[]      — { title } only
```

**`coding_completed`:**
```
data.changes[] —
  .repo_name, .repo_external_id
  .title, .description
  .diff                        (full diff as string)
  .branch_name
```

**`pr_created`:**
```
data.pull_requests[] —
  .pull_request   { pr_number, pr_url, pr_id }
  .repo_name
  .provider       ("github")
```

---

## 8. Code Review (Pre-Merge)

Separate from Autofix — runs on GitHub PRs:

- **Auto-trigger:** On `opened` (non-draft), `ready_for_review`, and every commit while ready
- **Manual trigger:** `@sentry review` comment on PR
- **Output:** Review comments on PR + GitHub status check
- **Status check states:** Success (no errors) | Neutral (errors found) | Error (service issue) | Cancelled (superseded by new commit)
- **Recommendation:** Keep as optional check, not required in branch protection

### Permissions Required

- Pull Requests: Read & Write
- Checks: Read & Write

---

## 9. Seer in the Sentry UI

From Sentry bot intel + dashboard docs audit:

- **Issue Details sidebar** — dedicated Seer section with AI debugging features
- **Initial Guess** — automatic pre-analysis that runs before the user clicks anything. Shown as a panel containing a starting hypothesis about the issue, with a "Find Root Cause" button beneath it. This means Seer is doing lightweight analysis on every viewed issue, not just on-demand.
- **Issue Summary** — auto-generated quick overview on issue pages (what's wrong, potential cause, trace-connected insights)
- **AI Summaries** — summarization of User Feedback and Session Replays to surface common patterns
- **"Find Root Cause" button** — manual Autofix trigger on any issue (beneath the Initial Guess panel)
- **Automated Fixes** — if enabled, Seer pre-populates root cause + fix before the user even opens the issue
- **Coding Agent Handoff** — from the Autofix panel, send root cause to external coding agents (e.g. Cursor) for implementation
- **Query Assistant** — NL→structured query for traces/spans data, finds relevant samples without manual query building
- **Experimental: `Cmd + /`** — opens Seer NL interface anywhere in Sentry for querying, investigations, and triage
- **Auto-trigger conditions:** Agent configured for background handoff + issue has 10+ events + medium-or-above fixability score
- **Slack integration (beta):** "Fix with Seer" button on issue alert messages, results posted to thread
- **AI Dashboards** — dedicated hub linking to AI Agents dashboards (agent workflows, token usage, tool calls, model costs) and MCP dashboards (see §13)
- **MCP Dashboards** — four sub-dashboards: Overview (traffic, client distribution, transport protocol), Tools (call counts, errors, p95 latency), Resources (access patterns), Prompts (usage, errors, latency)

---

## 10. Pricing, Constraints & Deployment Boundaries

### Pricing

- **Model:** Seer is a paid **add-on** to the Sentry subscription, using active contributor pricing — anyone with 2+ PRs/month in a Seer-enabled project is billed
- **Seer-enabled:** Repo connected to Sentry with any Seer feature turned on

### SCM Constraints

- **SCM:** GitHub cloud only (no self-hosted GitHub, no GitLab, no Bitbucket)

### Self-Hosted vs. SaaS Boundary

This is a critical architectural boundary:

- **Seer is closed-source and SaaS-only.** It is not available on self-hosted Sentry — not partially, not in degraded mode, not at all.
- **Sentry MCP Server works on self-hosted**, but via stdio transport only (not the hosted cloud endpoint), and **Seer skills must be explicitly disabled**:

```bash
npx @sentry/mcp-server@latest \
  --access-token=YOUR_TOKEN \
  --host=sentry.example.com \
  --disable-skills=seer
```

This means self-hosted Sentry users get MCP connectivity to their data (for use with Claude, Cursor, VS Code, etc.) but are explicitly locked out of Seer's AI debugging capabilities. The `--disable-skills=seer` flag is not optional — Seer skills will fail on self-hosted because they call SaaS-only APIs.

### Privacy & Controls

- **Privacy:** Sentry does not train generative AI models using customer data by default and without permission. AI-generated output is shown only to authorized users in the account.
- **PII:** All data passes through Relay for Advanced Data Scrubbing before storage. Seer and MCP only see already-scrubbed data (see §5).
- **Global kill switch:** `Show Generative AI Features` toggle in org settings (disables **all** generative AI features, not just Seer)
- **Granular controls:** Per-feature enable/disable per project/repo, advanced settings to block PR creation and alert augmentation

---

## 11. Loom's Competitive Wedges (Summary)

Based on all documented Seer capabilities:

| Wedge | Loom Advantage |
|-------|----------------|
| **Self-hosted** | Seer is closed-source, SaaS-only. Self-hosted Sentry users get MCP but must run `--disable-skills=seer`. Loom is fully self-hostable with zero feature degradation. |
| **Pricing** | Seer charges per active contributor (2+ PRs/month). Loom is free, MIT-licensed. |
| **SCM lock-in** | Seer requires GitHub cloud. Loom is SCM-agnostic. |
| **Backend** | Seer uses Sentry's proprietary backend. Loom uses DuckDB as single backend for traces/metrics/logs. |
| **Pipeline depth** | Seer: 3 stages. Loom: 5 stages with explicit context gathering and confidence scoring. |
| **Approval model** | Seer: per-request `stopping_point`. Loom: configurable `PolicyGate` (AutoApply/RequireReview/DryRun). |
| **Handoff UX** | Seer: in-page chat + Slack thread. Loom: background-to-conversation SSE handoff with full context hydration ("Attach & Continue Chat"). |
| **OTel-native** | Seer consumes OTel data via Sentry's collector. qyl/Loom is OTel-first with DuckDB as native OTel backend. |
| **Data ownership** | Seer has zero independent ingestion — RCA quality is capped by Sentry's sampling/retention. qyl owns the full stack (collector → DuckDB → Loom), so Loom queries exactly what was stored with no intermediate ceiling. |
| **PII transparency** | Sentry's Relay scrubs PII before storage; Seer sees already-scrubbed data with no visibility into what was removed or whether it was RCA-relevant. qyl's operator controls PII handling directly — no silent third-party scrubbing layer. |
| **Transparency** | Seer is closed-source; its API is "experimental and may change". Loom's pipeline is open-source and inspectable. |
| **OTel SDK breadth** | Sentry's OTel linking (`propagateTraceparent`, OTLP Integration) is live for Python and Ruby only; Go and PHP "coming soon"; **.NET is absent entirely**. qyl is .NET-first with native OTel instrumentation via Roslyn source generators. |
| **MCP observability depth** | Sentry's MCP dashboards show traffic/tools/resources/prompts as read-only charts. qyl's MCP server (54+ tools) exposes the same telemetry as agentic resources Loom can query during RCA — not just display. |
| **Dashboard editability** | Sentry's built-in dashboards cannot be edited (only duplicated to custom). qyl dashboards are fully open/extensible. |

---

## 12. Known Seer Gaps (Loom Opportunities)

From Autofix docs, API analysis, dashboard docs audit, and Kapa.ai bot interactions:

- No documented session management (retrieve/list/resume past Autofix runs beyond single-issue GET)
- **Partial** NL→query translation: Query Assistant handles traces/spans, but no documented NL→query for **logs** or **metrics** exploration. The `Cmd + /` experimental UI may expand this, but log-level NL querying remains a Loom opportunity.
- No documented anomaly detection or proactive alerting from AI analysis
- No documented multi-model orchestration or model selection transparency (the `/seer/models/` endpoint exists but response schema is undocumented)
- Webhook payloads are relatively flat — `solution_completed` steps only have titles, no structured rationale
- No documented confidence thresholds or automatic escalation policies
- Code Review and Autofix are separate flows with no documented cross-pollination (e.g. Code Review doesn't feed back into Autofix learning)
- **No .NET OTel integration** — Sentry's `propagateTraceparent` and OTLP Integration cover JavaScript, Python, Ruby, mobile SDKs; Go and PHP are "coming soon"; .NET is completely absent from the OTel linking story. This is a structural gap for any shop running ASP.NET Core / .NET backends with OTel instrumentation — they can't get end-to-end Sentry+OTel traces without manual workarounds.
- **MCP dashboards are display-only** — Sentry's four MCP sub-dashboards (Overview, Tools, Resources, Prompts) surface metrics like call counts, error rates, and p95 latency, but there's no documented path from MCP telemetry into Seer's RCA pipeline. MCP failures don't auto-trigger Autofix. Loom's MCP projection model feeds the same telemetry directly into the RCA context-gathering stage.
- **Dynamic Sampling opacity** — Sentry's built-in dashboards are affected by Dynamic Sampling, and there's no documented mechanism for Seer to know what data was sampled away. Seer's RCA operates on whatever survived sampling, with no visibility into what was dropped. qyl's event-sourced core stores everything with no sampling layer.
- **PII scrubbing opacity** — Relay applies Advanced Data Scrubbing before storage. Seer sees the scrubbed result with no documented mechanism to know what was removed, whether the scrubbed content was relevant to the root cause, or how scrubbing affected RCA quality. Overly aggressive scrubbing silently degrades Seer's analysis with no feedback loop. qyl's operator controls PII handling directly.
- **Session health → Seer disconnect** — Session Health dashboards track crashed/errored/abnormal sessions with release correlation, but there's no documented integration where session health regressions auto-trigger Seer investigation. The dashboards and Seer are parallel surfaces, not connected workflows.
- **Asset performance → Seer disconnect** — Frontend asset monitoring (render-blocking detection, size tracking, duration analysis) is dashboard-only. No documented path where slow/failing assets feed into Seer's RCA or trigger Autofix.
- **Web Vitals → Seer disconnect** — Performance Score (0–100), Opportunity scoring, and per-page Web Vital breakdowns are dashboard-only. Seer doesn't consume Web Vital regressions as RCA signals — a CLS spike or LCP regression won't trigger an investigation.
- **Initial Guess is lightweight only** — the automatic pre-analysis on Issue Details provides a starting hypothesis, but there's no documented path for it to feed richer context back into a full Autofix run. It appears to be a separate single-shot pass, not a warm-start for the 3-stage pipeline.

---

## 13. Sentry's Observability Surface (What Seer Draws From)

Understanding Sentry's full dashboard taxonomy reveals both the breadth of data available to Seer and the gaps where dashboards and Seer don't connect.

### Dashboard Taxonomy

**App-Wide:** Outbound API Requests (HTTP response duration, 3xx/4xx/5xx rates), Domain Details (drill-down per domain).

**Frontend (5 dashboards):** Frontend Overview (Best Page Opportunities, Most Time-Consuming Assets, p50/p75 duration), Web Vitals (Performance Score 0–100, log-normal distribution, separate desktop/mobile weight tables — LCP 30%, INP 30%, CLS 15%, FCP 15%, TTFB 10%, Opportunity scoring weighted by traffic), Assets (JS/CSS/image/font duration, size, render-blocking status, URL parameterization for grouping, drill-down Asset Summary → Sample List → Trace View), Session Health (Unhealthy Sessions, Session/User Counts by status: healthy/crashed/errored/abnormal — frontend "crashed" = unhandled errors, "errored" = handled errors, mutually exclusive).

**Backend (4 dashboards):** Backend Overview (Most Time-Consuming Queries/Domains, p50/p75), Queries (throughput, avg duration, drill-down to query summary), Caches (hit/miss rates, throughput, latency), Queues (publish/processing latency, error rates, throughput).

**Mobile (5 dashboards):** Mobile Vitals (cold/warm starts, slow/frozen frames, TTID/TTFD), Mobile Session Health (crash-free session/user rates with release annotations), App Starts, Screen Rendering (slow frames >16.7ms, frozen >700ms), Screen Loads (TTID/TTFD per screen).

**Framework-Specific:** Next.js Overview (SSR tree view, rage/dead clicks), Laravel Overview.

**AI (2 dashboards):** AI Agents (agent workflows, token usage, tool calls, model costs), MCP (Overview with traffic/client/transport distribution, Tools with call counts/errors/p95 latency, Resources, Prompts).

### Key Observation for Loom

Sentry's dashboard surface is **wide but shallow for AI integration**. The dashboards provide excellent human-facing visualization, but Seer's documented data consumption is limited to issues, traces, logs, profiles, and codebases (§5). The richer dashboard-level aggregates — Web Vital Performance Scores, asset render-blocking analysis, session health trends, MCP tool error rates, cache hit ratios — are not documented as Seer RCA inputs. They're parallel read-only surfaces.

Loom's architecture collapses this gap: because qyl stores all telemetry in DuckDB and Loom's context-gathering stage queries DuckDB directly, every metric that powers a dashboard is also available as an RCA signal. There's no "dashboard layer" that's disconnected from the RCA engine.

---

## 14. Sentry's OTel Integration Landscape

### Trace Linking (Sentry SDK ↔ OTel Backend)

For apps using Sentry SDKs on frontend/mobile with OTel-instrumented backends, `propagateTraceparent` sends the W3C `traceparent` header to link into a single distributed trace.

**Supported SDKs:** All major JavaScript frameworks (Browser JS, Angular, Astro, Ember, Gatsby, Next.js, Nuxt, React, React Router, Remix, Solid, SolidStart, Svelte, SvelteKit, Vue, Wasm), plus mobile (Android, Flutter, Native, React Native).

### OTLP Integration (Same-Service Coexistence)

For backends running both Sentry SDK and OTel instrumentation in the same process, the OTLP Integration forces shared trace IDs so Sentry errors link to OTel traces.

**Live:** Python, Ruby.
**Coming Soon:** Go, PHP.
**Absent:** .NET, Java, Rust, Elixir.

### Implication for Loom

The .NET gap is structurally significant. Any team running ASP.NET Core with OTel instrumentation (which is the standard .NET observability pattern) cannot get automatic Sentry↔OTel trace linking. They'd need to either:
1. Abandon OTel and go Sentry-SDK-only, or
2. Accept disconnected traces

qyl is .NET 10 native with Roslyn source-generated OTel instrumentation (`MeterEmitter`, `Qyl.Instrumentation.Generators`). There's no "linking" problem because qyl *is* the OTel backend — traces, metrics, and logs all land in DuckDB via the OTel collector with zero SDK-level integration needed.

---

## 15. Early Adopter Pipeline (Sentry's Near-Term Evolution)

Sentry's Early Adopter opt-in (toggled org-wide via Settings → General Settings, features roll out in waves) surfaces upcoming capabilities. Two sources give partially different snapshots:

### From docs page (2026-04-07 live fetch)

- **Issue Views** — custom issue list layouts
- **Issue Status tags** — richer triage state
- **Span Summary** — aggregated span-level insights
- **Dynamic Alerts** — adaptive alerting thresholds
- **New Trace Explorer with Span Metrics** — enhanced trace querying with metric-level drill-down
- **Size Analysis** — bundle/asset size tracking
- **Uptime Monitoring Verification** — validation layer for uptime checks

### From Kapa.ai bot (2026-04-07 query)

- **Seer Slack Workflows** — listed under AI & Automation category
- **Prebuilt Sentry Dashboards** — listed under Dashboards category

The discrepancy likely reflects wave-based rollout (the page warns features may not appear immediately) or Kapa.ai training on a different docs snapshot. Both lists are included here for completeness.

### What This Signals for Loom

**Seer Slack Workflows** (Kapa.ai) is directly relevant — it confirms Sentry is investing in Seer→Slack integration beyond the current beta "Fix with Seer" button, moving toward full workflow automation in Slack. Loom's SSE-based handoff model is a different UX paradigm but should be compared against a maturing Slack-first experience.

**New Trace Explorer with Span Metrics** (live docs) is the most strategically relevant infrastructure feature — it suggests Sentry is moving toward richer span-metric querying, which could eventually feed into Seer's context. If Seer gets access to span-level metric aggregates (not just raw spans), its RCA quality for performance issues would improve significantly. Loom already has this via DuckDB queries over OTel metric data.

**Dynamic Alerts** is also notable — adaptive thresholds are a prerequisite for "anomaly detection triggers Seer" workflows. This is currently a gap (§12), but the Early Adopter pipeline suggests Sentry is building toward it.

This list explicitly excludes alphas, closed betas, and manual-opt-in features, so there may be additional AI/Seer evolution not visible here.
