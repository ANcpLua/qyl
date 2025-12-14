# eng — Build Infrastructure & Tooling

@import "../CLAUDE.md"

## Scope

Engineering tools, build system, analyzers, and shared MSBuild infrastructure. NOT runtime components.

## Structure

```
eng/
├── build/              # Nuke build system (C#)
│   ├── Components/     # ICompile, ITest, ICoverage, etc.
│   ├── codex/          # Experimental planning (excluded from compile)
│   └── theory/         # Architecture exploration (excluded from compile)
├── MSBuild/            # Shared .props/.targets (future ANcpLua.NET.Sdk)
├── qyl.analyzers/      # Roslyn analyzers (RS0030 banned APIs)
├── qyl.cli/            # CLI tooling
├── policies/           # Code policies
└── build.{cmd,ps1,sh}  # Entry point scripts
```

## Dependency Rules

- `eng/` projects MUST NOT be referenced by `src/` runtime components
- `eng/build/` may reference Nuke packages only
- `eng/qyl.analyzers/` references Microsoft.CodeAnalysis only

## eng/build (Nuke)

Nuke build system with typed C# targets.

Key components:
- `ICompile` — dotnet build
- `ITest` — MTP test runner with filter support
- `ICoverage` — code coverage collection

### codex/ and theory/

Experimental directories excluded from compilation via:
```xml
<Compile Remove="theory\**\*.cs"/>
<Compile Remove="codex\**\*.cs"/>
```

These contain planning docs and architecture exploration, NOT production code.

## eng/MSBuild

Shared MSBuild infrastructure designed for extraction into standalone SDK.

Key files:
- `Shared.props` — common properties
- `Shared.targets` — InjectSharedThrow, etc.
- `LegacySupport.targets` — netstandard2.0 polyfills
- `BannedSymbols.txt` — RS0030 banned API definitions

## Commands

```bash
# Build everything
./eng/build.sh Compile

# Run tests
./eng/build.sh Test

# Run with coverage
./eng/build.sh Coverage

# Nuke directly
dotnet run --project eng/build -- Test --ITest-TestFilter "ClassName~Foo"
```
