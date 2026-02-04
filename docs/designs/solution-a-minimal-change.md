# Solution A: Minimal Change Approach - QYL Analyzers

## Executive Summary

Extend existing `qyl.instrumentation.generators` project with additional DiagnosticAnalyzers.
This approach minimizes structural changes while adding comprehensive diagnostics.

## Current State Analysis

### Existing Structure

```
src/qyl.instrumentation.generators/
├── Diagnostics/
│   └── DiagnosticDescriptors.cs      # QYL0001-0003, QYL1001
├── Interceptors/
│   └── GenAiInterceptorGenerator.cs  # Main incremental generator
├── Emitters/
│   └── InterceptorEmitter.cs         # Code generation
├── Extractors/
│   └── ResponseExtractor.cs          # Runtime attribute extraction
├── DuckDb/
│   ├── DuckDbAttributes.cs
│   ├── DuckDbEmitter.cs
│   └── DuckDbInsertGenerator.cs
├── AnalyzerReleases/
│   ├── AnalyzerReleases.Shipped.md
│   └── AnalyzerReleases.Unshipped.md
└── qyl.instrumentation.generators.csproj
```

### Existing Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| QYL0001 | Info | Interface call cannot be intercepted |
| QYL0002 | Info | Virtual method call cannot be intercepted |
| QYL0003 | Info | Call in compiled assembly cannot be intercepted |
| QYL1001 | Info | GenAI call intercepted (success indicator) |

## Proposed Diagnostic Expansion

### Diagnostic Numbering Strategy

Following Aspire's approach:
- **QYL0xxx**: Core/Design diagnostics (errors/warnings)
- **QYL1xxx**: Informational diagnostics (success indicators, suggestions)

### New Diagnostics (QYL004-QYL029)

Based on qyl's domains: instrumentation, service defaults, protocol, collector.

#### Category: Instrumentation (QYL004-QYL010)

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| QYL0004 | Warning | Missing OTel configuration | `AddQylServiceDefaults()` called but `AddOpenTelemetry()` is missing |
| QYL0005 | Warning | Duplicate instrumentation | Same method instrumented by both interceptor and manual span |
| QYL0006 | Error | Invalid ActivitySource name | ActivitySource name doesn't follow naming convention |
| QYL0007 | Warning | Missing schema URL | `ActivitySource` created without schema URL for semconv compliance |
| QYL0008 | Info | Manual span recommended | Complex flow detected; manual instrumentation may be better |
| QYL0009 | Warning | Deprecated GenAI attribute | Using deprecated OTel semantic convention attribute |
| QYL0010 | Warning | Missing model parameter | GenAI call without model specification |

#### Category: Service Defaults (QYL011-QYL016)

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| QYL0011 | Error | Service defaults misconfiguration | Conflicting configuration in `AddQylServiceDefaults()` |
| QYL0012 | Warning | Missing resilience configuration | HTTP client without resilience policies |
| QYL0013 | Warning | Missing health checks | Service registered without health check endpoint |
| QYL0014 | Info | Consider connection string | Hardcoded connection string; use configuration instead |
| QYL0015 | Warning | Insecure endpoint | HTTP endpoint used where HTTPS expected |
| QYL0016 | Warning | Missing service discovery | Direct URL used instead of service discovery |

#### Category: Protocol/Attributes (QYL017-QYL022)

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| QYL0017 | Error | Invalid attribute value | Attribute value violates OTel semantic convention spec |
| QYL0018 | Warning | Missing required attribute | Required semantic convention attribute not set |
| QYL0019 | Warning | Incorrect attribute type | Attribute set with wrong type (e.g., string for int) |
| QYL0020 | Info | Prefer constant attribute | Use `GenAiAttributes.X` instead of string literal |
| QYL0021 | Warning | Sensitive data in attribute | PII/credential detected in span attribute |
| QYL0022 | Warning | High cardinality attribute | Attribute value may cause cardinality explosion |

#### Category: Collector Integration (QYL023-QYL029)

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| QYL0023 | Warning | Missing OTLP configuration | OTEL_EXPORTER_OTLP_ENDPOINT not configured |
| QYL0024 | Warning | Uncompressed export | OTLP export without compression configured |
| QYL0025 | Error | Invalid OTLP endpoint | Malformed OTLP endpoint URL |
| QYL0026 | Warning | Batch export disabled | Single-span export reduces performance |
| QYL0027 | Info | Consider sampling | High-volume service without sampling configured |
| QYL0028 | Warning | Missing resource attributes | `service.name` or `service.version` not configured |
| QYL0029 | Warning | MCP endpoint exposed | MCP server running without authentication |

## Implementation Plan

### Phase 1: Infrastructure (2 files)

#### File 1: `/src/qyl.instrumentation.generators/Core/QylAnalyzer.cs`

```csharp
// Base class for all qyl analyzers (mirrors ANcpLua.Analyzers pattern)
namespace qyl.instrumentation.generators.Core;

public abstract class QylAnalyzer : DiagnosticAnalyzer
{
    public const string HelpLinkBase = "https://github.com/ANcpLua/qyl/blob/main/docs/diagnostics";

    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        RegisterActions(context);
    }

    protected abstract void RegisterActions(AnalysisContext context);
}

public static class DiagnosticIds
{
    // Interceptor limitations (existing)
    public const string InterfaceCallCannotBeIntercepted = "QYL0001";
    public const string VirtualCallCannotBeIntercepted = "QYL0002";
    public const string CompiledAssemblyCannotBeIntercepted = "QYL0003";

    // Instrumentation
    public const string MissingOTelConfiguration = "QYL0004";
    public const string DuplicateInstrumentation = "QYL0005";
    public const string InvalidActivitySourceName = "QYL0006";
    public const string MissingSchemaUrl = "QYL0007";
    public const string ManualSpanRecommended = "QYL0008";
    public const string DeprecatedGenAiAttribute = "QYL0009";
    public const string MissingModelParameter = "QYL0010";

    // Service Defaults
    public const string ServiceDefaultsMisconfiguration = "QYL0011";
    public const string MissingResilienceConfiguration = "QYL0012";
    public const string MissingHealthChecks = "QYL0013";
    public const string ConsiderConnectionString = "QYL0014";
    public const string InsecureEndpoint = "QYL0015";
    public const string MissingServiceDiscovery = "QYL0016";

    // Protocol/Attributes
    public const string InvalidAttributeValue = "QYL0017";
    public const string MissingRequiredAttribute = "QYL0018";
    public const string IncorrectAttributeType = "QYL0019";
    public const string PreferConstantAttribute = "QYL0020";
    public const string SensitiveDataInAttribute = "QYL0021";
    public const string HighCardinalityAttribute = "QYL0022";

    // Collector Integration
    public const string MissingOtlpConfiguration = "QYL0023";
    public const string UncompressedExport = "QYL0024";
    public const string InvalidOtlpEndpoint = "QYL0025";
    public const string BatchExportDisabled = "QYL0026";
    public const string ConsiderSampling = "QYL0027";
    public const string MissingResourceAttributes = "QYL0028";
    public const string McpEndpointExposed = "QYL0029";

    // Success indicators
    public const string GenAiCallIntercepted = "QYL1001";
}

public static class DiagnosticCategories
{
    public const string Instrumentation = "qyl.instrumentation";
    public const string ServiceDefaults = "qyl.servicedefaults";
    public const string Protocol = "qyl.protocol";
    public const string Collector = "qyl.collector";
}
```

**Lines of code:** ~80

#### File 2: Update `/src/qyl.instrumentation.generators/Diagnostics/DiagnosticDescriptors.cs`

Refactor to use new `DiagnosticIds` constants and add new descriptors.

**Lines of code:** ~200 (expansion from current 65)

### Phase 2: High-Value Analyzers (6 files)

Priority order based on impact:

| Priority | File | Diagnostic | Est. LOC |
|----------|------|------------|----------|
| 1 | `Analyzers/Qyl0004MissingOTelConfigurationAnalyzer.cs` | QYL0004 | 80 |
| 2 | `Analyzers/Qyl0009DeprecatedGenAiAttributeAnalyzer.cs` | QYL0009 | 120 |
| 3 | `Analyzers/Qyl0020PreferConstantAttributeAnalyzer.cs` | QYL0020 | 100 |
| 4 | `Analyzers/Qyl0006InvalidActivitySourceNameAnalyzer.cs` | QYL0006 | 70 |
| 5 | `Analyzers/Qyl0013MissingHealthChecksAnalyzer.cs` | QYL0013 | 90 |
| 6 | `Analyzers/Qyl0028MissingResourceAttributesAnalyzer.cs` | QYL0028 | 85 |

### Phase 3: Remaining Analyzers (19 files)

Each analyzer ~70-120 lines. Total: ~1700 LOC

### Phase 4: Tests

Using `ANcpLua.Roslyn.Utilities.Testing` pattern:

```
tests/qyl.instrumentation.generators.Tests/
├── Qyl0004Tests.cs
├── Qyl0009Tests.cs
└── ... (one per analyzer)
```

Estimated: ~2000 LOC for tests

## File List Summary

### New Files to Create

```
src/qyl.instrumentation.generators/
├── Core/
│   └── QylAnalyzer.cs                              # Base class + IDs (NEW)
├── Analyzers/
│   ├── Qyl0004MissingOTelConfigurationAnalyzer.cs  # NEW
│   ├── Qyl0005DuplicateInstrumentationAnalyzer.cs  # NEW
│   ├── Qyl0006InvalidActivitySourceNameAnalyzer.cs # NEW
│   ├── Qyl0007MissingSchemaUrlAnalyzer.cs          # NEW
│   ├── Qyl0008ManualSpanRecommendedAnalyzer.cs     # NEW
│   ├── Qyl0009DeprecatedGenAiAttributeAnalyzer.cs  # NEW
│   ├── Qyl0010MissingModelParameterAnalyzer.cs     # NEW
│   ├── Qyl0011ServiceDefaultsMisconfigAnalyzer.cs  # NEW
│   ├── Qyl0012MissingResilienceAnalyzer.cs         # NEW
│   ├── Qyl0013MissingHealthChecksAnalyzer.cs       # NEW
│   ├── Qyl0014ConsiderConnectionStringAnalyzer.cs  # NEW
│   ├── Qyl0015InsecureEndpointAnalyzer.cs          # NEW
│   ├── Qyl0016MissingServiceDiscoveryAnalyzer.cs   # NEW
│   ├── Qyl0017InvalidAttributeValueAnalyzer.cs     # NEW
│   ├── Qyl0018MissingRequiredAttributeAnalyzer.cs  # NEW
│   ├── Qyl0019IncorrectAttributeTypeAnalyzer.cs    # NEW
│   ├── Qyl0020PreferConstantAttributeAnalyzer.cs   # NEW
│   ├── Qyl0021SensitiveDataInAttributeAnalyzer.cs  # NEW
│   ├── Qyl0022HighCardinalityAttributeAnalyzer.cs  # NEW
│   ├── Qyl0023MissingOtlpConfigurationAnalyzer.cs  # NEW
│   ├── Qyl0024UncompressedExportAnalyzer.cs        # NEW
│   ├── Qyl0025InvalidOtlpEndpointAnalyzer.cs       # NEW
│   ├── Qyl0026BatchExportDisabledAnalyzer.cs       # NEW
│   ├── Qyl0027ConsiderSamplingAnalyzer.cs          # NEW
│   ├── Qyl0028MissingResourceAttributesAnalyzer.cs # NEW
│   └── Qyl0029McpEndpointExposedAnalyzer.cs        # NEW
```

### Files to Modify

```
src/qyl.instrumentation.generators/
├── Diagnostics/
│   └── DiagnosticDescriptors.cs                    # EXPAND
└── AnalyzerReleases/
    └── AnalyzerReleases.Unshipped.md               # UPDATE
```

## Estimated Lines of Code

| Component | Files | Est. LOC |
|-----------|-------|----------|
| Core/QylAnalyzer.cs | 1 | 80 |
| DiagnosticDescriptors.cs (expansion) | 1 | +135 |
| Analyzers (26 new) | 26 | 2,340 |
| Tests | 26 | 2,000 |
| **Total** | **54** | **~4,555** |

## Implementation Order

### Sprint 1: Foundation
1. `Core/QylAnalyzer.cs` - Base class and ID constants
2. Expand `DiagnosticDescriptors.cs` - All descriptors
3. Update `AnalyzerReleases.Unshipped.md`

### Sprint 2: High-Impact Analyzers
4. QYL0004 - Missing OTel configuration
5. QYL0009 - Deprecated GenAI attribute
6. QYL0020 - Prefer constant attribute
7. QYL0006 - Invalid ActivitySource name

### Sprint 3: Service Defaults
8. QYL0011-QYL0016

### Sprint 4: Protocol/Attributes
9. QYL0017-QYL0022

### Sprint 5: Collector Integration
10. QYL0023-QYL0029

### Sprint 6: Remaining & Polish
11. QYL0005, QYL0007, QYL0008, QYL0010
12. All tests
13. Documentation

## Comparison with Solution B

| Aspect | Solution A (Minimal) | Solution B (New Project) |
|--------|---------------------|--------------------------|
| New projects | 0 | 2 (analyzers + tests) |
| csproj changes | 0 | 2 new csproj files |
| Namespace | `qyl.instrumentation.generators` | `qyl.analyzers` |
| NuGet packaging | Existing infrastructure | New packaging setup |
| CI changes | None | New test job |
| Risk | Low | Medium |
| Separation of concerns | Mixed (generators + analyzers) | Clean separation |

## Recommendation

**Solution A is recommended** because:

1. **Zero infrastructure changes** - No new projects, no CI changes
2. **Proven pattern** - Aspire does exactly this (generators + analyzers in one project)
3. **Simpler packaging** - Single NuGet package with both capabilities
4. **Faster delivery** - Can start implementing immediately
5. **Existing test infrastructure** - Already set up for this project

The main trade-off is mixing generators and analyzers, but this is an accepted pattern in the Roslyn ecosystem (see Aspire.Hosting.Analyzers, which has both).

## Next Steps

1. Approve this design document
2. Create `Core/QylAnalyzer.cs` with base class and ID constants
3. Expand `DiagnosticDescriptors.cs` with all new descriptors
4. Implement Phase 2 high-value analyzers
5. Iterate on remaining phases
