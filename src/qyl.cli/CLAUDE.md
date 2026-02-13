# qyl.cli - Instrumentation CLI

Zero-config entry point. One command detects stack, modifies project files, adds qyl observability. Embodies the philosophy: eliminate every decision that isn't "what do I want to observe?"

## Role in Architecture

Terminal surface for onboarding. `qyl init` is the fastest path from "nothing" to "fully instrumented." Auto-detection means no configuration — the CLI figures out .NET, Docker, or Node and wires everything up. Pairs with Copilot (IDE surface) for guided setup.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| Tool | `qyl` (dotnet tool) |
| Dependencies | Spectre.Console, YamlDotNet |

## Usage

```bash
qyl init                               # Auto-detect and instrument
qyl init dotnet                        # .NET project instrumentation
qyl init docker                        # Add qyl to docker-compose
qyl init --dry-run                     # Show changes without applying
qyl init --project ./path              # Specify project path
qyl init --collector-url http://host   # Custom collector URL
```

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — command dispatch |
| `CliArgs.cs` | CLI argument parsing |
| `Commands/InitCommand.cs` | Auto-detect orchestrator |
| `Commands/DotnetInitCommand.cs` | .NET project instrumentation |
| `Commands/DockerInitCommand.cs` | Docker Compose instrumentation |
| `Detection/StackDetector.cs` | Stack detection (csproj/compose/package.json) |
| `Detection/CsprojEditor.cs` | Add PackageReference to csproj |
| `Detection/ProgramCsEditor.cs` | Insert code into Program.cs |
| `Detection/ComposeEditor.cs` | Add collector service to compose.yaml |
| `Output/ConsoleOutput.cs` | Spectre.Console formatted output |

## Commands

### `init` (auto-detect)

1. `StackDetector` scans for `.csproj`, `docker-compose.yml`, `package.json`
2. Runs appropriate sub-commands based on detection
3. Reports what was modified

### `init dotnet`

1. Finds `.csproj` file in project directory
2. Adds `qyl.servicedefaults` PackageReference via `CsprojEditor`
3. Inserts `AddServiceDefaults()` + `MapDefaultEndpoints()` via `ProgramCsEditor`
4. Reports changes (or dry-run preview)

### `init docker`

1. Finds `docker-compose.yml`/`compose.yaml` in project directory
2. Adds qyl collector service via `ComposeEditor` (YamlDotNet)
3. Configures OTLP environment variables on existing services
4. Reports changes (or dry-run preview)

## Rules

- `--dry-run` must never modify any files
- All file modifications are idempotent (skip if already present)
- Default collector URL: `http://localhost:5100`
