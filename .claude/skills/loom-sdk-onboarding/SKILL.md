---
name: loom-sdk-onboarding
description: Install and configure qyl .NET SDK features (error monitoring, tracing, profiling, logging, metrics, crons) in the user's project. Detection-first — scans the repo to classify framework (ASP.NET Core / WPF / WinForms / MAUI / Blazor WASM / Azure Functions / AWS Lambda / Classic ASP.NET / Console). Never recommends a package or init pattern that contradicts the evidence.
---

# loom-sdk-onboarding — qyl .NET SDK setup wizard

Loom's .NET SDK onboarding workflow. Drives a detection-first setup across the six qyl feature areas: error monitoring, tracing, profiling, logging, metrics, crons.

## Invoke this skill when
- The user asks to install / set up qyl in a .NET project (any framework).
- The user mentions `Loom.AspNetCore`, `Loom.Maui`, `Loom.Profiling`, `Loom.Extensions.Logging`, `LoomSdk.Init`, `UseLoom`, `TracesSampleRate`, or MSBuild symbol upload.
- The user asks about error monitoring / tracing / profiling / logging / metrics / cron monitoring for any .NET app.
- `loom-workflow` routed to `SetupDotnetSdk`.

## Non-negotiable rules

1. **Detect first, recommend second.** Never pick a package or init pattern that contradicts detection evidence.
2. **Tracing is disabled by default.** Profiling requires tracing. Both `TracesSampleRate > 0` and `AddProfilingIntegration(...)` are needed.
3. **`EnableLogs` and `EnableMetrics` are opt-in gates.** Without them every `LoomSdk.Logger.*` / `LoomSdk.Metrics.*` call is a silent no-op.
4. **Desktop apps require `IsGlobalModeEnabled = true`** (WPF / WinForms / Console).
5. **Serverless requires `FlushOnCompletedRequest = true`** (AWS Lambda, Azure Functions via Loom.OpenTelemetry).
6. **Never skip verification.** Finish with a test capture and confirm the event in the Issues dashboard.

## How to run this skill

### Step 1 — Detect the project shape

Call MCP tool `loom_detect_dotnet(repoRoot)`. It scans `*.csproj` files, classifies the framework, surfaces target frameworks, existing qyl packages, logging libraries (Serilog/NLog/log4net/ILogger), scheduler libraries (Hangfire/Quartz/BackgroundService), AI SDKs, sibling frontend dirs, and whether profiling / global-mode / flush-on-completed-request apply.

Output shape (selected fields):

```json
{
  "framework": "AspNetCore | Wpf | WinForms | Maui | BlazorWasm | AzureFunctions | AwsLambda | ClassicAspNet | ConsoleOrWorker | Unknown",
  "targetFrameworks": ["net10.0"],
  "existingLoomPackages": [],
  "recommendedPackage": "Loom.AspNetCore",
  "recommendedInitFile": "src/My.Web/Program.cs",
  "loggingLibraries": ["ILogger"],
  "schedulerLibraries": ["BackgroundService"],
  "aiSdks": ["Microsoft.Extensions.AI"],
  "siblingFrontendDirs": ["../frontend"],
  "requiresGlobalMode": false,
  "requiresFlushOnCompletedRequest": false,
  "supportsProfiling": true,
  "recommendations": { "errorMonitoring": true, "tracing": true, "logging": true, "profiling": true, "metrics": false, "crons": true }
}
```

### Step 2 — Fetch the wizard prompt

Call MCP prompt `qyl.loom.setup_dotnet(detectionJson, features?)` with the detection JSON verbatim. The prompt returns the full wizard plan, detection-driven, with the six hard rules at the top.

### Step 3 — Confirm the feature set with the user

The wizard recommends a baseline of **error + tracing + logging**; profiling / metrics / crons are opt-in. Confirm before adding the optional features.

### Step 4 — Fetch per-feature prompts as needed

For each agreed feature, fetch the dedicated prompt:

| Feature | Prompt |
|---|---|
| Error monitoring | `qyl.loom.setup_dotnet_error` |
| Tracing | `qyl.loom.setup_dotnet_tracing` |
| Profiling | `qyl.loom.setup_dotnet_profiling` |
| Logging | `qyl.loom.setup_dotnet_logging` |
| Metrics | `qyl.loom.setup_dotnet_metrics` |
| Crons | `qyl.loom.setup_dotnet_crons` |

Each prompt takes the detection JSON and returns the feature-specific directive with the load-bearing invariants (enablement gates, platform caveats, SDK version floors).

### Step 5 — Install + configure + verify

1. If `existingLoomPackages` is non-empty → skip install, jump to feature config.
2. Otherwise run `dotnet add package <recommendedPackage> -v 6.1.0` in the project directory.
3. Edit `recommendedInitFile` with the framework-specific init pattern.
4. Wire in the requested features per their prompts.
5. If `siblingFrontendDirs` is non-empty, point the user at the matching frontend SDK (same qyl project → distributed tracing across frontend + .NET server).
6. Throw a test exception, confirm event in qyl Issues, confirm readable stack traces (requires MSBuild symbol upload: `LoomOrg`, `LoomProject`, `LoomUploadSymbols`, `Loom_AUTH_TOKEN`).
7. Remove the test throw and commit.

## MCP surface this skill uses

| Tool | Purpose |
|---|---|
| `loom_detect_dotnet` | Scans the repo, classifies framework, surfaces evidence + recommendations. |

| Prompt | Purpose |
|---|---|
| `qyl.loom.setup_dotnet` | Wizard preamble with the six hard rules. |
| `qyl.loom.setup_dotnet_error` | Error monitoring — automatic vs manual capture, scope discipline, filtering, fingerprinting. |
| `qyl.loom.setup_dotnet_tracing` | Tracing — enablement gate, auto-instrumentation, custom spans, distributed tracing, OTel interop. |
| `qyl.loom.setup_dotnet_profiling` | Profiling — .NET 8+ only, tracing-prerequisite, platform caveats (Linux container bug, iOS/Mac Catalyst native path). |
| `qyl.loom.setup_dotnet_logging` | Logging — MEL / Serilog / NLog / log4net integration selection, `EnableLogs` gate, NLog minlevel trap. |
| `qyl.loom.setup_dotnet_metrics` | Metrics — `EnableMetrics` gate, supported numeric types, Loom1001 analyzer. |
| `qyl.loom.setup_dotnet_crons` | Crons — two-signal vs heartbeat, Hangfire auto, Quartz manual, rate limits. |

## Framework → package mapping (what the detector picks)

| Framework | Package |
|---|---|
| ASP.NET Core | `Loom.AspNetCore` |
| WPF | `Loom` + `IsGlobalModeEnabled = true` + `DispatcherUnhandledException` hook in `App()` constructor |
| WinForms | `Loom` + `SetUnhandledExceptionMode(ThrowException)` before init |
| MAUI | `Loom.Maui` |
| Blazor WASM | `Loom.AspNetCore.Blazor.WebAssembly` (no profiling available) |
| Azure Functions (Isolated Worker) | `Loom.Extensions.Logging` + `Loom.OpenTelemetry` |
| AWS Lambda | `Loom.AspNetCore` + `FlushOnCompletedRequest = true` |
| Classic ASP.NET (System.Web) | `Loom.AspNet` |
| Console / Worker / Generic Host | `Loom.Extensions.Logging` + `IsGlobalModeEnabled = true` |

## Hard rules

- **Detection is the only source of truth.** If `framework == "Unknown"`, stop and ask which project to target — do not guess.
- **`IsGlobalModeEnabled` on desktop / console** — without it, background-thread exceptions are lost.
- **`FlushOnCompletedRequest` on serverless** — without it, events are lost when the container freezes.
- **WPF init location** — `App()` constructor, NOT `OnStartup()`. Hook `DispatcherUnhandledException` alongside.
- **Profiling is not free.** Net rate = `TracesSampleRate × ProfilesSampleRate`. Linux containers may hit `ReflectionTypeLoadException` (known issue #4815) — wrap `AddProfilingIntegration()` in try/catch.
- **Symbol upload** — stack traces show `<unknown>` without PDBs. Set `LoomUploadSymbols=true` + `Loom_AUTH_TOKEN` in CI.

## Troubleshooting

| Issue | Fix |
|---|---|
| `framework == "Unknown"` | Ask the user which `.csproj` is the primary app; re-run detection scoped to its directory. |
| Events not appearing | Set `options.Debug = true`; check console for SDK diagnostic messages; verify DSN. |
| Stack traces show no file/line | Add `LoomUploadSymbols=true` to `.csproj`; set `Loom_AUTH_TOKEN` in CI. |
| `TracesSampleRate` has no effect | Default is 0 — must set to `> 0` to enable tracing. Profiling depends on this. |
| Azure Functions duplicate HTTP spans | Set `options.DisableLoomHttpMessageHandler = true` — let OTel drive tracing. |
| Blazor WASM profiling request | Not supported. Explain + skip. |
