# READ THIS — Qyl.Host handoff

> **He left this before resetting it all.** Read this, then work through it *with
> the user* — do not execute silently. This folder is the single place the
> Qyl.Host unification lives. Everything below is committed and pushed to the
> `ANcpLua/qyl` remote (public again since 2026-07-11), so it survives a local reset.

## What this is

A plan to merge the two app hosts that exist today into one polyglot engine.
The full design, with file:line evidence, is **[DESIGN.md](./DESIGN.md)** in this
folder. Read that second; read this first.

## The one-paragraph version

There are two app hosts, deliberately written as twins. `packages/Qyl.Host` (C#, renamed from `Qyl.Run` 2026-07-11)
supervises processes over **HTTP health** but can only launch **.NET**
(`QylAppBuilder.cs:185-191` hardcodes `dotnet run --project`). The TS twin
(shaped 1:1 after `Qyl.Run`; since the 2026-07-11 merge it is the `runner/`
half of `qyl-workspace/qyl.mcp`, formerly the standalone `mcp-run`)
launches **any executable** but only
supervises **MCP servers** (readiness is the `initialize`+`tools/list` handshake).
Each is locked on the axis the other is free. The plan makes **runtime** and
**readiness** independent strategies in one engine, `Qyl.Host`.

## Where to start with the user (in order)

1. **Confirm the direction.** `Qyl.Host` as the engine; `qyl run` stays the CLI
   verb. MCP becomes a plugin (`Qyl.Host.Mcp`), not the substrate. Do NOT reuse
   the name `qyl.mcp` — it was deleted in `43d032f9` and the merged MCP repo
   `qyl-workspace/qyl.mcp` (qyl-apps-server's successor) already inherited it.
2. ~~Step 1: the public-door unlock~~ — **✅ DONE 2026-07-11 (#510 ①)** as
   `AddCommand(name, command, port, workingDirectory?, healthPath?)`; the
   .NET-only lock is gone (`qyl run --dev` launches Vite through it), and
   **`WaitFor` + cycle detection shipped the same day** (#510 ①②;
   `withReference` cut per the #510 triage — explicit `WithEnvironment` +
   `GetEndpoint` instead).
3. **Remaining migration path** in DESIGN.md §"Migration path": step 2
   `IReadinessProbe`, step 3 `Qyl.Host.Mcp` (+ port `qyl.mcp/runner/src/telemetry.ts`),
   step 5 Console convergence, step 6 rename last.

## Repo state at handoff (2026-07-10)

- **`ANcpLua/qyl` was set PRIVATE at handoff** (was public; set back on request
  — system critical; verified `visibility: PRIVATE` then). **Public again since
  2026-07-11.**
- **All four repos clean and fully pushed** (dirty=0, ahead=0): `qyl`,
  `mcp-run`, `qyl-apps-server`, `x-apps-server`. (Since then: `mcp-run` and
  `qyl-apps-server` moved from `~/Desktop` into this workspace 2026-07-10/11,
  then merged into `qyl.mcp` and were archived on GitHub 2026-07-11;
  `x-apps-server` was deleted locally and lives only on GitHub.)
- All three are private on `github.com/ANcpLua`. `x-apps-server` is
  **architectural reference only** — the user does not want the X product
  developed further.

## Facts to carry forward (verified, correct the old record)

- **Aspire is NOT a dependency** anywhere. `Qyl.Run.csproj:7` says "Zero Aspire
  deps." Drop the "Aspire-style" framing; it undersells a ~1,700 LoC zero-dep engine.
- **There is no `qyl.run.dashboard`.** The runner frontend is
  `packages/Qyl.Host.Console`; the product dashboard is `services/qyl.dashboard`.
- **`Qyl.Run/README.md` is trustworthy again** — rewritten 2026-07-11 to the
  real surface (`AddCollector`/`AddProject`/`AddCommand` + `WaitFor` + the
  self-telemetry composition primitives). The old fiction became real code
  later the same day (#510 ①–③): `WaitFor` dependency ordering and the `[B]`
  browser key now EXIST; only `AddDashboard`/`.WithCollector` stay fiction
  (dashboard-as-resource was cut in the #510 triage).
- **Rename cost is zero externally.** `Qyl.Run` is not on nuget.org; its only
  consumer is `Qyl.Run.Host`.
- **The autoinstrumentation goal is not a separate engine** — it is what
  `Qyl.Host` becomes once the probe/transport are pluggable.
  `qyl.mcp/runner/src/telemetry.ts` already proves "instrument the host, not
  the client."

## Session context (what happened right before the reset)

Non-qyl, but part of the same sitting, for whoever resumes:
- Reclaimed ~18.6 GB of regenerable build output across `~/RiderProjects` and home.
- Fixed Codex `~/.codex/config.toml` (a stray `model` string inside
  `tui.model_availability_nux` was breaking chat/task startup), updated Codex to
  0.143.0, repaired its state DB, cleared dead config paths.
- Disabled all `ancplua`/`personal` Codex plugins; left OpenAI's enabled. Overview
  at `~/Desktop/codex-plugins.html`.
