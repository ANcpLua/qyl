# Reference: opentelemetry-dotnet-instrumentation extraction

Source material from
[open-telemetry/opentelemetry-dotnet-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation)
(Apache-2.0) that was **studied but deliberately not wired into qyl**. Kept as a
pattern library; nothing here is compiled.

## What was integrated instead (2026-07-07)

| Extracted treasure | Landed in qyl as |
|--------------------|------------------|
| `tools/SdkVersionAnalyzer` | `eng/tools/SdkVersionAnalyzer` + `VerifySdkVersions`/`UpdateSdkVersions` targets (expected version now comes from `global.json`) |
| `tools/DependencyListGenerator` (vendored dotnet-outdated) | `eng/tools/DependencyListGenerator` + `GenerateDependencyList` target |
| `tools/LibraryVersionsGenerator` | `eng/tools/LibraryVersionsGenerator` + `GenerateLibraryVersions` target (qyl-shaped definitions; full otel matrix kept here as `build/PackageVersionDefinitions.full.cs`) |
| `tools/GacInstallTool` | `eng/tools/GacInstallTool` |
| `build/Attributes/LazyPathExecutableAttribute.cs` | `eng/build/Attributes/` |
| `build/Extensions/DotNetSettingsExtensions.cs`, `build/Models/PackageBuildInfo.cs` | `eng/build/Extensions/`, `eng/build/Models/` (string-TFM adaptation) |
| `Build.NuGet.Steps.cs` pack + local-cache-purge pattern | `eng/build/BuildPack.cs` (`Pack`, `CleanLocalPackagesCache`) |
| `Common.props` NuGetAudit trio | root `Directory.Build.props` |
| `Common.props` base64-snk strong-naming task | `eng/MSBuild/StrongName.targets` (dormant, opt-in) |
| tools-scoped `Directory.Build.props`/`Directory.Packages.props` isolation pattern | `eng/tools/Directory.Build.props` (relaxations scoped to tools) |

## What is archived here and why it was not integrated

- `build/` — otel's partial-class NUKE build (`Build.cs`, NuGet/installation-script
  steps, `_build.csproj.txt` — extension neutralized so `*.csproj` globs like the CI
  dependency-audit sweep never treat the archive as a restorable project): qyl's
  interface-composed NUKE build is the better shape;
  the transferable steps were ported, the native-artifact plumbing is otel-specific.
  `AssemblyRedirectionSourceGenerator.cs` (Mono.Cecil → C-macro header) only serves
  otel's native profiler loader.
- `root/` — otel's root MSBuild files (`Common.props`, `Common.targets`,
  `Directory.Build.props`, `Directory.Packages.props`, `GlobalSuppressions.cs`):
  useful properties were cherry-picked; the StyleCop stack and strong-name key wiring
  don't apply wholesale.
- `docker/` — distro build images for otel's native profiler (glibc/musl matrix);
  qyl's services own their Dockerfiles.
- `dev/` — otel's collector dev-compose; qyl **is** the collector and has
  `eng/compose.yaml`.
