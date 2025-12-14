# ANcpLua.NET.Sdk — TL;DR

## Was existiert bereits (funktioniert!)

```
qyl/eng/MSBuild/Shared.targets    ← CLAUDE.md Generation aus .csproj Properties
qyl/eng/MSBuild/BannedSymbols.txt ← TimeProvider, Lock, etc.
```

## .csproj Properties → CLAUDE.md

```xml
<!-- In qyl.collector.csproj -->
<PropertyGroup>
  <InjectClaudeBrain>true</InjectClaudeBrain>
  <ClaudePurpose>Backend service: OTLP ingestion, DuckDB storage</ClaudePurpose>
  <ClaudeLayer>backend</ClaudeLayer>
  <ClaudeWorkflow>explore-plan-code-commit</ClaudeWorkflow>
  <ClaudeTestCoverage>80</ClaudeTestCoverage>
</PropertyGroup>

<ItemGroup>
  <ClaudeCriticalFile Include="Storage/DuckDbStore.cs" Reason="Core persistence"/>
  <ClaudeAntiPattern Include="DateTime.Now" UseInstead="TimeProvider.System" Severity="error"/>
  <ClaudeRequiredPattern Include="Lock" Description=".NET 9+ Lock class"/>
</ItemGroup>
```

**Build → Generiert automatisch:**

```markdown
# qyl.collector
@import "../../CLAUDE.md"

## Scope
Backend service: OTLP ingestion, DuckDB storage

## Critical Files
| File | Reason |
| Storage/DuckDbStore.cs | Core persistence |

## Anti-Patterns (FORBIDDEN)
| `DateTime.Now` | `TimeProvider.System` | error |
```

---

## SDK Package Struktur (Meziantou-Style)

```
sdk/
├── ANcpLua.NET.Sdk.csproj           # NoBuild=true, NuSpecFile
├── ANcpLua.NET.Sdk.nuspec
│
├── Sdk/ANcpLua.NET.Sdk/
│   ├── Sdk.props                    # → imports Common.props
│   └── Sdk.targets                  # → imports Common.targets
│
├── common/
│   ├── Common.props                 # Defaults, CI detection
│   ├── Common.targets               # Package injection, CLAUDE.md
│   ├── ContinuousIntegrationBuild.props
│   └── Tests.targets                # xunit.v3 + MTP
│
├── configuration/
│   ├── BannedSymbols.txt
│   └── *.editorconfig
│
└── Shared/
    ├── Throw/Throw.cs
    └── Polyfills/Lock.cs            # für netstandard2.0
```

---

## Sdk.props (Entry Point)

```xml
<Project>
  <PropertyGroup>
    <ANcpLuaSdkName>ANcpLua.NET.Sdk</ANcpLuaSdkName>
    <_MustImportMicrosoftNETSdk Condition="'$(UsingMicrosoftNETSdk)' != 'true'">true</_MustImportMicrosoftNETSdk>
    <CustomBeforeDirectoryBuildProps>$(CustomBeforeDirectoryBuildProps);$(MSBuildThisFileDirectory)../common/Common.props</CustomBeforeDirectoryBuildProps>
    <BeforeMicrosoftNETSdkTargets>$(BeforeMicrosoftNETSdkTargets);$(MSBuildThisFileDirectory)/../common/Common.targets</BeforeMicrosoftNETSdkTargets>
  </PropertyGroup>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" Condition="'$(_MustImportMicrosoftNETSdk)' == 'true'" />
  <Import Project="$(MSBuildThisFileDirectory)../common/Common.props" Condition="'$(_MustImportMicrosoftNETSdk)' != 'true'" />
</Project>
```

---

## Common.targets (Die Magie)

```xml
<Project>
  <!-- Package Injection -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="4.14.0" IsImplicitlyDefined="true">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)\..\configuration\BannedSymbols.txt"/>
  </ItemGroup>

  <!-- Web Projects: Inject OTel + Agents -->
  <ItemGroup Condition="'$(UsingMicrosoftNETSdkWeb)' == 'true'">
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" IsImplicitlyDefined="true"/>
    <PackageReference Include="Microsoft.Extensions.AI" IsImplicitlyDefined="true"/>
  </ItemGroup>

  <!-- CLAUDE.md Generation (von Shared.targets übernehmen) -->
  <UsingTask TaskName="_WriteTextFile" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup>
      <Path ParameterType="System.String" Required="true"/>
      <Content ParameterType="System.String" Required="true"/>
    </ParameterGroup>
    <Task>
      <Code Type="Fragment" Language="cs">
        System.IO.File.WriteAllText(Path, Content);
      </Code>
    </Task>
  </UsingTask>

  <Target Name="_GenerateClaudeMd" AfterTargets="Build" Condition="'$(InjectClaudeBrain)' == 'true'">
    <!-- Properties zu Markdown konvertieren und schreiben -->
  </Target>
</Project>
```

---

## Usage

```json
// global.json
{
  "msbuild-sdks": {
    "ANcpLua.NET.Sdk": "1.0.0"
  }
}
```

```xml
<!-- Directory.Build.props -->
<Project>
  <Sdk Name="ANcpLua.NET.Sdk"/>
</Project>
```

```xml
<!-- qyl.collector.csproj (unverändert!) -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

---

## Build & Pack

```bash
# Lokaler Test
dotnet build src/qyl.collector  # → CLAUDE.md generiert

# SDK packen
dotnet pack sdk/ANcpLua.NET.Sdk.csproj -o ./nupkgs

# SDK lokal testen
dotnet nuget add source ./nupkgs --name local
```

---

## Key Insight

**Meziantou Pattern:**
- `_MustImportMicrosoftNETSdk` prüft ob Microsoft SDK bereits geladen
- `CustomBeforeDirectoryBuildProps` injiziert VOR allem anderen
- `BeforeMicrosoftNETSdkTargets` injiziert targets
- `IsImplicitlyDefined="true"` auf PackageReference = user kann nicht entfernen

**CLAUDE.md Pattern:**
- Properties in .csproj = Source of Truth
- MSBuild Target generiert bei Build
- `@import` für Vererbung
- Condition `!Exists()` = nur generieren wenn nicht vorhanden (user customization)
