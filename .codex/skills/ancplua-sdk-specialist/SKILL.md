---
name: ancplua-sdk-specialist
description: |
  Specialized agent for ANcpLua MSBuild SDK - props/targets, auto-injected packages, polyfills, test infrastructure, and EditorConfig rules
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# ANcpLua.NET.Sdk Specialist

Specialized agent for working with the ANcpLua MSBuild SDK.

## When to Use

- Modifying SDK props/targets
- Adding auto-injected packages
- Creating polyfills for older TFMs
- Changing test infrastructure
- Updating EditorConfig rules
- Debugging MSBuild evaluation

## Repository Context

**Path**: `/Users/ancplua/ANcpLua.NET.Sdk`
**Purpose**: Opinionated MSBuild SDK for standardized .NET builds
**Variants**: ANcpLua.NET.Sdk, ANcpLua.NET.Sdk.Web, ANcpLua.NET.Sdk.Test

## Architecture

```
src/
├── Sdk/
│   ├── ANcpLua.NET.Sdk/       # Main SDK entry (Sdk.props/targets)
│   ├── ANcpLua.NET.Sdk.Web/   # Web variant
│   └── ANcpLua.NET.Sdk.Test/  # Test variant
├── Build/
│   ├── Common/                # Core logic (15+ files)
│   ├── Enforcement/           # Policy enforcement
│   └── Packaging/             # Analyzer layout
├── Testing/                   # Test infrastructure
│   ├── Fixtures/              # Base classes
│   └── AotTesting/            # AOT test helpers
└── Config/                    # 13 EditorConfig files

eng/
├── LegacySupport/             # Polyfill source files
└── Extensions/                # Shared utilities
```

## Key Patterns

### Import Chain

```
Sdk.props
  → GlobalPackages.props (before Microsoft.NET.Sdk)
  → Microsoft.NET.Sdk
  → Common.props
  → Enforcement.props
  → Testing.props (if test project)
Sdk.targets
  → Common.targets
  → Enforcement.targets
```

### Auto-Detection

```xml
<!-- Test project detection -->
<IsTestProject>true</IsTestProject>  <!-- *.Tests, *.Test, or /tests/ -->

<!-- Integration test detection -->
<IsIntegrationTestProject>true</IsIntegrationTestProject>  <!-- .Web/.Api reference -->
```

### GlobalPackageReference

```xml
<!-- Auto-injected (immutable with CPM) -->
<GlobalPackageReference Include="ANcpLua.Analyzers"/>
<GlobalPackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers"/>
```

### Policy Enforcement

- **BANNED**: PolySharp, FluentAssertions, Microsoft.NET.Test.Sdk
- **REQUIRED**: Central Package Management enabled
- **REQUIRED**: CentralPackageTransitivePinningEnabled=true

## Big Picture

- **Consumers**: ErrorOrX, qyl, ServiceDefaults, Template (via global.json)
- **Upstream dependency**: ANcpLua.Roslyn.Utilities for shared SDK helpers
- **Auto-injects**: ANcpLua.Analyzers, xunit.v3, AwesomeAssertions

## Build & Test

```bash
dotnet build ANcpLua.NET.Sdk.slnx
dotnet test --solution ANcpLua.NET.Sdk.slnx
```

## Polyfill Opt-In

```xml
<PropertyGroup>
  <InjectIndexRangeOnLegacy>true</InjectIndexRangeOnLegacy>
  <InjectTrimAttributesOnLegacy>true</InjectTrimAttributesOnLegacy>
  <InjectTimeProviderOnLegacy>true</InjectTimeProviderOnLegacy>
</PropertyGroup>
```

## Key Files

| File | Purpose |
|------|---------|
| `src/Build/Common/Common.props` | Main configuration |
| `src/Build/Enforcement/Enforcement.props` | Banned packages/patterns |
| `src/Testing/Testing.props` | Test auto-injection |
| `src/Config/BannedSymbols.txt` | API banning rules |

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.
