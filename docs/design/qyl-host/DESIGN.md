# Qyl.Host — one polyglot app host

> **Status:** design-of-record / proposal. Retires the "Aspire-style" framing.
> **Scope:** unify the two app hosts that exist today — `packages/Qyl.Run` (C#)
> and the TS host `mcp-run` (since the 2026-07-11 merge, the `runner/` half of
> `qyl-workspace/qyl.mcp`; the standalone repo is archived) — into one engine
> whose runtime and protocol are both pluggable. Every claim below cites source;
> `mcp-run` file citations refer to the pre-merge layout, preserved verbatim in
> the archived repo (the same code lives on under `qyl.mcp/runner/src/`). Words
> are claims, tool output is proof.

## TL;DR

There are two app hosts, written to be twins. `Qyl.Run` supervises processes
over **HTTP health** but can only launch **.NET**. `mcp-run` launches **any
executable** but only supervises **MCP servers**. Each is locked on the axis the
other is free. The fix is not to pick a winner or to port one into the other as
a service — it is to make **runtime** and **readiness** independent strategies in
a single engine, `Qyl.Host`. The .NET lock is one private method; the polyglot
capability the product needs is ~15 additive lines away.

## The two engines today

### `Qyl.Run` (C#) — HTTP-health supervisor, .NET-only

A dependency-light distributed-app runner: launches local service processes,
health-probes them, supervises/restarts, renders a Spectre.Console TUI, and
exposes a read-only HTTP/SSE state feed. Two NuGets (`ANcpLua.Roslyn.Utilities`,
`Spectre.Console`), `IsAotCompatible=true`, ~1,700 LoC.

- **Public surface:** `QylAppBuilder.Create(args)` → `AddCollector(name, port?,
  project?, selfTelemetry?)` / `AddProject(name, project, port?)` → `Build()` →
  `QylApp.RunAsync()`. Fluent primitives: `IQylResourceBuilder.Update(mutate)` and
  `QylSelfTelemetryBuilder` (`ExportTo` / `ExportToDedicatedCollector` /
  `RejectSelfReference` — validation is always-on; the method is call-site
  documentation) for one-way collector self-telemetry into a dedicated
  diagnostics collector whose own exporter stays disabled.
- **Resource model:** `QylResource { Name, Kind, Port, OtlpHttpPort, GrpcPort, Launch }`,
  `QylLaunchSpec { Executable, Args, Env, WorkingDirectory, HealthPath="/health" }`,
  `enum ResourceLifecycle { Pending, Starting, Ready, Stopping, Stopped, Failed }`.
- **Readiness = HTTP:** `QylOrchestrator.PollHealthAsync` polls `GET
  <endpoint>/health` every 500 ms until success or `StartupTimeoutSeconds` (60s).
- **Restart policy:** crash-restart bounded by `MaxRestarts = 3`; user restart
  from the TUI resets the budget and relaunches on the same port.
- **Runner API:** loopback `HttpListener` (BCL, AOT-clean), GET-only, prefix
  `http://127.0.0.1:18888/runner/` — `resources`, `resources/stream` (SSE),
  `resources/{name}/logs`, `resources/{name}/logs/stream` (SSE).

**The lock.** `QylProcessLauncher` is generic — it runs whatever
`Launch.Executable` + `Args` say. But the *only* builder path that creates
resources hardcodes the toolchain, `QylAppBuilder.BuildLaunchSpec`
(`packages/Qyl.Run/QylAppBuilder.cs`):

```csharp
Executable = QylConstants.Orchestrator.DotnetExecutable,   // "dotnet"
Args = [ RunCommand, NoLaunchProfileFlag, ProjectFlag, project ]
```

There is **no public API to set a custom executable** — `AddCollector`/`AddProject`
take only name/project/port (plus collector self-telemetry wiring).
`Qyl.Run.Host/Program.cs` states it plainly:
*"resources are launched via `dotnet run --project <path>`, so only .NET projects
can be added."* The mechanism is polyglot; the shipped surface is not.

### `mcp-run` (TypeScript) — MCP supervisor, polyglot

Written **1:1 after `Qyl.Run`** so it can be ported back mechanically
(`mcp-run/ARCHITECTURE.md:3`, *"deliberately shaped 1:1 after
qyl/packages/Qyl.Run … so it can later be ported into qyl mechanically"*). Same
lifecycle enum, same `MaxRestarts: 3`, same runner API on `:18888`, same
replay-on-subscribe SSE contract. ~1300 LoC runner + ~1380 LoC dashboard.

The mirror, per its own translation table (`ARCHITECTURE.md:13-23`):

| `Qyl.Run` (C#)            | `mcp-run` (TS)          |
| ------------------------ | ----------------------- |
| `QylConstants`           | `constants.ts`          |
| `QylResource/LaunchSpec` | `McpResource/LaunchSpec`|
| `QylOrchestrator`        | `Orchestrator`          |
| `QylRunnerApi`           | `RunnerApi` (express)   |
| `QylLogStore`            | `LogStore`              |

**Polyglot by construction.** The builder takes an unconstrained command
(`app-builder.ts:30-48`, `command: string`), and the spawn passes it straight
through (`orchestrator.ts:255-261`):

```ts
new StdioClientTransport({ command: launch.command, args: [...launch.args], env, cwd: launch.cwd })
```

No interpreter literal anywhere in the launch path. `node`, `python`, `go run`,
`dotnet` are all just caller-supplied `command`+`args`. The .NET case is one
tuple among many, not a wired-in assumption.

**Its lock is the other axis: readiness = the MCP handshake.** Ready requires
`initialize` + `tools/list` to succeed (`orchestrator.ts:214`); liveness is an
MCP `ping` (`:304`); stdout is reserved as the JSON-RPC channel and never logged.
It captures `serverInfo`, `toolCount`, and `hasAppUi` from the handshake. It
**cannot supervise a plain HTTP service** — which is the only thing `Qyl.Run`
launches today.

## What each got right that the other lacks

- **`mcp-run` has the dependency API `Qyl.Run` still lacks.** `waitFor` +
  `withReference` with dependency-ordered startup and cycle detection
  (`app-builder.ts:119`, `orchestrator.ts:68-91`) — no equivalent exists in
  `Qyl.Run`. (Its README
  once advertised `.WaitFor`, `.WithCollector`, `AddDashboard`, and a `[B]`
  browser key that never existed in code; it was rewritten 2026-07-11 to the
  real surface and is trustworthy again.)
- **`mcp-run` has `/runner/mcp` passthrough + host-side OTLP self-monitoring**
  (`runner-api.ts:108-136`, `telemetry.ts`). `Qyl.Run` has neither.
- **`Qyl.Run` has HTTP-health supervision, the Spectre TUI, AOT, and lives in the
  product repo.** `mcp-run` has none of these.

## The insight: reverse the dependency arrow

Do **not** port `mcp-run` into qyl as a service, and do **not** swap `Qyl.Run`'s
engine out for it wholesale — that would trade the .NET lock for an MCP lock and
lose the ability to supervise `qyl.collector`. Instead, absorb `mcp-run`'s
*design* into the engine and make **protocol a strategy, not the substrate.**

```
packages/
  Qyl.Host                         ← the engine. polyglot, protocol-agnostic.
    QylLaunchSpec { Executable, Args, Env, Cwd }     (already exists — QylResources.cs:23)
    AddExecutable(name, command, args, port?)        (the missing public door — ~15 lines)
    IReadinessProbe                                  (the missing abstraction)
      ├─ HttpHealthProbe   → GET /health             (what Qyl.Run does today)
      └─ McpHandshakeProbe → initialize + tools/list (what mcp-run does today)
    waitFor / withReference + cycle detection        (port from app-builder.ts:119 + orchestrator.ts:68-91)
    lifecycle · MaxRestarts=3 · log ring buffer · /runner SSE   (already exists)

  Qyl.Host.Mcp                     ← MCP as a plugin, not the core
    McpHandshakeProbe · /runner/mcp passthrough · OTLP self-monitoring (telemetry.ts port)

  Qyl.Host.Console                 ← TS frontend, MCP Apps capable (ext-apps)
    convergence of Qyl.Run.Console + mcp-run/dashboard
```

`Qyl.Host` for the engine — it *hosts*. `qyl run` stays the CLI verb, so the
ergonomics survive the rename with no noun collision. That answers "qyl.run or
qyl.host": **both, at different layers** — `Qyl.Host` the package, `qyl run` the
command.

## Naming: why `Qyl.Host`, not `qyl.mcp`

`qyl.mcp` is a **burned name**. It was deleted in commit `43d032f9` (2026-05-25,
*"Path C Phase 1: qyl.mcp destruction"*, 53 files). Its successor already exists
and is not this engine: `qyl.mcp/server`'s README (the merged successor to
`qyl-apps-server`) calls itself *"the successor to the deleted services/qyl.mcp
Apps."* A host that **runs** MCP servers
is not itself an MCP server; reusing `qyl.mcp` for the host would collide with the
server that already inherited the name.

**Rename cost is zero externally.** `Qyl.Run` is not published on nuget.org
(`api.nuget.org/.../qyl.run` → `BlobNotFound`), and its only consumer is
`Qyl.Run.Host` (no `PackageReference` anywhere; no test links it). The rename is a
mechanical, last-step change.

## Migration path — ordered, each step independently shippable

1. **`AddExecutable(name, command, args, port?)`** — ends the .NET-only lock.
   Additive, non-breaking; `AddProject`/`AddCollector` stay as the .NET
   convenience wrappers over it. `QylLaunchSpec` already carries `Executable`;
   this only adds the public door. **~15 lines. Do this first.**
2. **`IReadinessProbe`** — extract today's `GET /health` poll behind an interface
   (`HttpHealthProbe` as the default, behaviour unchanged). One method:
   `Task<bool> IsReadyAsync(QylResourceState, CancellationToken)`.
3. **`Qyl.Host.Mcp`** — `McpHandshakeProbe` (initialize + tools/list), the
   `/runner/mcp` passthrough, and a port of `telemetry.ts`. MCP support becomes an
   opt-in package, not a core assumption.
4. **`waitFor` / `withReference`** — port dependency-ordered startup + cycle
   detection from `orchestrator.ts:68-91`. This is the API the old `Qyl.Run`
   README advertised (removed in the 2026-07-11 rewrite); make it real.
5. **`Qyl.Host.Console`** — converge `Qyl.Run.Console` and `mcp-run/dashboard`;
   keep the ext-apps MCP Apps rendering from the latter.
6. **Rename `Qyl.Run` → `Qyl.Host`** — last, mechanical, once the surface is
   settled.

## The autoinstrumentation throughline

The stated goal — *autoinstrument any language client via a yet-to-be-made
engine* — is not a separate future project. It is what `Qyl.Host` becomes once
the probe and transport are pluggable. `telemetry.ts` already proves the thesis
(`telemetry.ts:1-15`): *"instrumenting HERE monitors every MCP server without
touching any of them."* It emits `mcp.tool.name` **and** `gen_ai.tool.name`
together (`:122-123`), gates payload capture behind `MCP_RUN_RECORD_INPUTS/OUTPUTS`,
and targets the qyl collector via `QYL_OTLP_ENDPOINT`. (Post-merge the gate env
vars are `QYL_MCP_RECORD_INPUTS/OUTPUTS`.) The principle generalizes:
**instrument the host, not the client.** A host that spawns arbitrary executables
and owns their transport is exactly the seam where you inject instrumentation into
a language you don't control. Polyglot host + owned transport = the injection
point. That is the engine.

## Corrections to the record

- **Aspire is not a dependency.** No `Aspire.Hosting` in any `.csproj` or
  `Directory.Packages.props`; `Qyl.Run.csproj:7` says *"Zero Aspire deps."* Every
  mention is positioning. Drop "Aspire-style"; the honest one-liner is *a polyglot
  app host — launch, supervise, and observe any process in any language, with one
  origin and one trace.*
- **There is no `qyl.run.dashboard`.** The runner frontend is
  `packages/Qyl.Run.Console` (package name `qyl.run.console`), distinct from the
  product dashboard `services/qyl.dashboard`.
- **RESOLVED 2026-07-11:** `mcp-run/ARCHITECTURE.md` used to list OTLP as
  "out of scope (v1)" while `telemetry.ts` shipped; the doc now has a
  "Host-side telemetry" section documenting `McpTelemetry` and OTLP was
  removed from the out-of-scope list.
