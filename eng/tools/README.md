# eng/tools — repo maintenance tooling

Standalone tools extracted from
[open-telemetry/opentelemetry-dotnet-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation)
(Apache-2.0; original license headers preserved) and adapted to qyl's layout.
Non-integrated originals live in `eng/reference/otel-dotnet-instrumentation/`.

| Tool | What it does | NUKE target |
|------|--------------|-------------|
| **SdkVersionAnalyzer** | Verifies (or rewrites) pinned .NET SDK versions in GitHub workflows/actions and Dockerfiles against `global.json`, the toolchain's single source of truth. Channel tags (`sdk:10.0@sha256:...`) count as unpinned and are skipped. | `VerifySdkVersions` (part of `Ci`), `UpdateSdkVersions` |
| **DependencyListGenerator** | Vendored [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated) analysis: resolves the full transitive dependency graph of a project via `dotnet list package`. Referenced directly by `eng/build` as a library. | `GenerateDependencyList` → `docs/dependencies.md` |
| **LibraryVersionsGenerator** | Generates package-version test matrices (xunit + build flavors) from `PackageVersionDefinitions.cs`, resolving `*` against the CPM `Directory.Packages.props`. Opt-in scaffolding for future multi-version integration tests; output goes to `Artifacts/generated`. | `GenerateLibraryVersions` |
| **GacInstallTool** | Installs/uninstalls a directory of assemblies into the Windows GAC (`-i dir` / `-u dir`). .NET Framework-only; compiles everywhere via reference assemblies, runs on Windows. | — |

Run targets through the build entry point, e.g.:

```bash
./eng/build.sh VerifySdkVersions
./eng/build.sh GenerateDependencyList
./eng/build.sh UpdateSdkVersions --housekeeping-sdk-version 10.0.302
```
