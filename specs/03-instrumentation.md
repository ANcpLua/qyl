# Instrumentation Specification

Compile-time OTel instrumentation via Roslyn source generators. Zero runtime reflection.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Schema Generation](#2-schema-generation)
3. [Roslyn Generators](#3-roslyn-generators)
4. [Runtime](#4-runtime)
5. [Attributes](#5-attributes)
6. [Definition of Done](#6-definition-of-done)

---

## 1. Overview

Three distinct layers. Do not confuse them.

```text
Layer 1: Schema generation     eng/build/SchemaGenerator.cs         NUKE build time
Layer 2: Roslyn generators     src/qyl.instrumentation.generators/  MSBuild compile time
Layer 3: Runtime wiring        src/qyl.instrumentation/             Application startup
```

Layer 1 is NOT a Roslyn generator. Layer 2 is NOT runtime reflection. Layer 3 does NOT generate code.

## 2. Schema Generation

`eng/build/SchemaGenerator.cs` — runs during NUKE build.

Input: TypeSpec OpenAPI definitions.
Output: C# models, enums, DuckDB DDL.

Related generators in `eng/build/`:

- `ContractGenerator.cs` — C# contract types
- `SchemaMigrationGenerator.cs` — DuckDB migration SQL
- `TypeMappingTable.cs` — type mapping between TypeSpec and C#
- `NamespaceRoutingTable.cs` — namespace resolution

## 3. Roslyn Generators

`src/qyl.instrumentation.generators/` — IIncrementalGenerator implementations.

### 3.1 Generator Entry Point

`ServiceDefaultsSourceGenerator.cs` — main generator. Uses `ForAttributeWithMetadataName` to find attributed call sites.

### 3.2 Analyzers

Roslyn analyzers that detect instrumentation call sites:

- `GenAiCallSiteAnalyzer` — `[GenAi]` attributed methods
- `DbCallSiteAnalyzer` — `[Db]` attributed methods
- `TracedCallSiteAnalyzer` — `[Traced]` attributed methods
- `AgentCallSiteAnalyzer` — agent invocation call sites
- `AnalyzerHelpers` — shared analysis utilities

### 3.3 Emitters

Code emitters that generate interceptor source:

- `TracedInterceptorEmitter` — generates span-wrapping interceptors
- `MeterEmitter` — generates metric recording
- `CapabilityEmitter` — generates capability detection
- `EmitterHelpers` — shared emission utilities

### 3.4 Rules

- Always `IIncrementalGenerator`, never `ISourceGenerator`
- Always `ForAttributeWithMetadataName`, never syntax tree walking
- Value-equatable models only (no `ISymbol` in models)
- Raw strings for generated code, never `SyntaxFactory`
- Test with ANcpLua.Roslyn.Utilities infrastructure

## 4. Runtime

`src/qyl.instrumentation/` — runtime OTel wiring.

- `QylServiceDefaults.cs` — extension methods to register OTel providers
- `QylServiceDefaultsExtensions.cs` — `AddQylServiceDefaults()` for host builder
- `CollectorDiscovery.cs` + `CollectorDiscoveryLogger.cs` — auto-discover collector endpoint
- `ActivityExceptionTelemetry.cs` — exception-to-span enrichment

### 4.1 Instrumentation Attributes

- `CounterAttribute` — marks a method for metric counter generation
- `TracedReturnAttribute` — marks return value for span attribute capture
- `GenAi/GenAiInstrumentation.cs` — GenAI-specific OTel wiring
- `Db/DbInstrumentation.cs` — database-specific OTel wiring

## 5. Attributes

12 instrumentation attributes across 6 pipelines. See `docs/instrumentation-toolkit.md` for the canonical reference.

Key attributes:

- `[Traced]` — wraps method in an Activity span
- `[GenAi]` — wraps LLM call with GenAI semconv attributes
- `[Db]` — wraps database call with DB semconv attributes
- `[Counter]` — generates a metric counter for the method
- `[TracedReturn]` — captures return value as span attribute

## 6. Definition of Done

- [ ] All generators are IIncrementalGenerator with ForAttributeWithMetadataName
- [ ] Generated interceptors produce correct OTel spans with semconv 1.40 attributes
- [ ] No runtime reflection anywhere in the instrumentation pipeline
- [ ] Analyzer releases tracked in AnalyzerReleases.Shipped.md / Unshipped.md
- [ ] All generators pass ANcpLua.Roslyn.Utilities test suite
- [ ] CollectorDiscovery auto-detects collector endpoint without manual configuration
