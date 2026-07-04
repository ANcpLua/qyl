# Agent guidance — qyl

Repo-specific instructions for AI agents (Claude Code, Copilot, etc.) working in
this repository. `CLAUDE.md` is a symlink to this file. For review-only rules see
`.github/copilot-instructions.md`; this file is the authoring contract.

## What qyl is

qyl is an OpenTelemetry-compatible observability platform. OTel is the
ingestion/instrumentation layer; qyl's product is the **collector, storage model,
API, and investigation dashboard** built around telemetry. See `README.md` for the
architecture diagram and run instructions.

## Hard rules

- **Contracts are single-sourced.** Public API request/response/DTO/schema shapes
  live in the external `qyl-api-schema` TypeSpec repo and flow in via the
  `Qyl.Api.Contracts` package. Do **not** add new public API models in
  `qyl.collector`, and do **not** create a second contract source in this monorepo.
  The path is `Schema → OpenAPI → DTO contracts → clients`. Runtime storage rows,
  OTLP ingest wire types, and internal projections are fine **only** when they are
  not returned as the product API contract.
- **Generated files are not hand-edited.** Fix the generator or the schema input,
  regenerate, then commit the result. This covers Roslyn `*.g.cs`, generated
  protobuf, and the dashboard's generated TS API types (`npm run generate:ts`).
- **`Version.props` is the single owner of package versions.** Never edit `<Version>`
  lines or hard-code versions in a `.csproj`. Central Package Management
  (`Directory.Packages.props`) governs package versions; nullable is enabled
  repo-wide.
- **Don't reintroduce removed code.** `eng/build/BuildVerify.cs` carries a
  `removedCollectorTokens` guard that fails the build if deleted symbols come back.
  If a check there fires, the fix is to keep the symbol gone — not to delete the
  guard entry.

## Toolchain

- .NET SDK pinned by `global.json` (currently `10.0.301`, `rollForward:
  latestFeature`), **C# 14**, built on the `ANcpLua.NET.Sdk` MSBuild SDK family.
- Tests run on **Microsoft.Testing.Platform** (`global.json` → `test.runner`).
- Dashboard: ESM, Node ≥ 20, React 19 + Vite + TanStack + Tailwind 4; no new
  external runtime deps unless already declared in `package.json`.

## Build / test / run

```bash
# Collector (REST API + OTLP ingest + DuckDB), serves on :5100, OTLP :4317/:4318
dotnet run --project services/qyl.collector
dotnet build services/qyl.collector/qyl.collector.csproj

# Dashboard
cd services/qyl.dashboard && npm install && npm run dev
npm run build        # tsc -b && vite build
npm test             # vitest

# Local distributed runner (collector + dashboard together)
dotnet run --project packages/Qyl.Run.Host
```

## Branches, commits, PRs

- **Branch prefix `claude/…` for agent-driven work.** The fleet `auto-merge.yml`
  enables GitHub native auto-merge for `claude/` (and `copilot/`) branches once the
  required status checks pass. Naming an agent branch `claude/<topic>` is the opt-in
  signal that it is automation-authored and safe to auto-merge; other prefixes
  (`fix/`, `refactor/`, `chore/`, `renovate/…`) do **not** auto-merge. Never commit
  to `main` directly and never force-push `main`.
- **Conventional Commits** for titles and squash-merge messages, with a scope:
  `feat(run): …`, `fix(collector): …`, `refactor(run): …`, `chore!: …` for breaking
  changes. Match the surrounding history.
- Deliver through a PR; let CI (`.github/workflows/ci.yml`) go green before merge.

## See also

- `README.md` — architecture, contract path, ports, local run.
- `.github/copilot-instructions.md` — reviewer focus and skip rules.
- `Version.props` / `Directory.Packages.props` — version + package ownership.
