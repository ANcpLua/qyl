# Routine Test Run — qyl-e2e-tests 2026-05-17

## Status: BOOTSTRAP PR OPENED

## Outcome

First-ever run of `qyl-e2e-tests` on a workstation with the full toolchain
present (dotnet 10.0.300, Docker 29.4.0). The previous run (2026-05-16)
exited as a no-op because the remote container lacked `dotnet`.

Per the skill's bootstrap section, this run produced the **infrastructure-only
PR** (project + topology fixture + Nuke target + central-package pins). **No
scenario tests** were added; the next routine run picks up from here and adds
the first scenario.

## Files added / changed

- `tests/qyl.e2e.tests/qyl.e2e.tests.csproj` — new test project, `ANcpLua.NET.Sdk.Test`
- `tests/qyl.e2e.tests/E2ECollection.cs` — `[CollectionDefinition("E2E", DisableParallelization = true)]`
- `tests/qyl.e2e.tests/Topology/QylTopologyOptions.cs` — image tags + startup timeout
- `tests/qyl.e2e.tests/Topology/QylTopologyFixture.cs` — programmatic Testcontainers
  topology: bridge network → `qyl-collector:latest` → `qyl-mcp:latest`; WireMock
  process-local; containers reach it via `host.docker.internal`.
  `WithImagePullPolicy(_ => false)` enforces local-image-only.
- `tests/qyl.e2e.tests/Bootstrap/WireMockLlmSeamTests.cs` — Category=E2EBootstrap
  smoke (no Docker) — proves the WireMock seam roundtrips a scripted
  `/v1/chat/completions` and shows up in `LogEntries`.
- `eng/build/BuildTest.cs` — new `E2ETests` target depending on
  `IDocker.DockerImageBuild`, filters `Category=E2E`. Default `Test` excludes
  `Category=E2E` (bootstrap tests stay in).
- `qyl.slnx` — registered the project.
- `Version.props` + `Directory.Packages.props` — added `WireMock.Net 2.6.0` and
  `Testcontainers 4.11.0`. Split-pinned `OpenTelemetry.Instrumentation.AspNetCore`
  to 1.15.2 (forced by WireMock.Net 2.6.0 transitive) while keeping Http +
  Runtime at the umbrella 1.15.1 (no 1.15.2 release exists for them).

## Verification

- `dotnet build qyl.slnx` — 0 errors, 1454 warnings (baseline 1393; ~61 new
  warnings are all `MultipleGlobalAnalyzerKeys` from the dual-`.globalconfig`
  worktree setup — benign, present on every worktree build).
- `dotnet test tests/qyl.e2e.tests --filter-trait Category=E2EBootstrap` — 3
  consecutive runs, all green (~1s each, 0 flakes).
- `nuke E2ETests` target wired up but **not executed** this run (would require
  rebuilding all four qyl Docker images — out of scope for the bootstrap PR).

## Overlap with `Smoke`

`eng/smoke/run.sh` (Nuke target `Smoke`) is the PRD #173 quality gate: real
Ollama + real qyl Compose stack, asserts on cost / activity / conversations /
inventory wiring. It uses a **real LLM** (Ollama).

E2E (this routine) uses **WireMock** for deterministic LLM stubbing. The two
don't overlap — Smoke covers "does the stack work against a real model"; E2E
covers "does the stack route data correctly given a known-bad/redacted LLM
response we can assert on".

## Gaps for the next run

The next `qyl-e2e-tests` cycle should add the first real scenario. Highest-value
candidates:

1. **Agent submits chat → trace arrives at downstream sink with credentials
   redacted** — exercises qyl.mcp → qyl.collector → OTLP egress with a
   scripted LLM response that contains a fake bearer token; asserts the token
   is `<redacted>` in the sink output.
2. **MCP HTTP session reconnect after transient collector failure** — proves
   the MCP transport's resumption behavior under collector restart.
3. **Cost rollup updates after a single chat completion** — the smallest
   slice of PRD #173 surface that can be tested deterministically.

Pick exactly one. Add a sink container (e.g. an OTel collector configured to
write to a file volume) to `QylTopologyFixture` for scenario 1.

## Branch and PR

- Branch: `tests/auto-e2e-2026-05-17` (off `origin/main`)
- PR: opened against `main` as draft → ready-for-review once green.
