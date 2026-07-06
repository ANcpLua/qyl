# CLAUDE.md — qyl beta-launch SSOT (Fable execution stream)

> This file is the **single source of truth** and **memory stream** for the beta
> push. No `CHANGELOG.md`, no second contract. Fable keeps it perma-updated: append
> what you did (with evidence) under **Progress log**, tick the checklist, never
> silently drop a finding. The old `AGENTS.md` symlink/contract was retired on purpose;
> today's pattern below overrides its Branch/PR ceremony.
> **Absolute deadline: beta launch in ~1 day (today = 2026-07-06).**

## Today's operating pattern — no ceremony

- **No PRs. No feature branches. No worktrees.** Work directly on `main`, local.
- **No blockers today.** `git commit`, `--force`, `git reset`, delete — allowed locally.
  Nothing is forbidden until shortly before launch.
- **Publish locally, not via CI.** Don't wait on `ci.yml`. Pack to a **local NuGet feed**;
  build dashboard/collector locally. CI is re-armed only at the very end (final green
  before the public beta tag).
- **Scope filter:** only reason about things **created or modified in July 2026**. Older
  is out of scope unless you can concretely argue it must change.
- **Version target: `0.1.0-beta.1`.** SemVer prerelease; NuGet treats `-beta.1` as
  prerelease; `beta.1 → beta.2` iterates cleanly. Base `0.1.0` already in both
  `package.json`; add the `-beta.1` suffix where the product is stamped.

## Invariants that still hold (correctness, not ceremony — never skip)

- **Contracts single-sourced.** Public API shapes live in the external `qyl-api-schema`
  TypeSpec repo → flow in via `Qyl.Api.Contracts`. Don't add public API models in
  `qyl.collector`; don't create a second contract source. Runtime storage rows / OTLP
  ingest wire types / internal projections are fine only when not returned as the
  product API contract.
- **Generated files are not hand-edited.** Fix the generator/schema, regenerate, commit
  the result (`*.g.cs`, generated protobuf, dashboard `npm run generate:ts`).
- **`Version.props` is the single owner of package versions.** Never hard-code a version
  in a `.csproj`; CPM via `Directory.Packages.props`. Nullable on repo-wide.
- **Don't reintroduce removed code.** `eng/build/BuildVerify.cs` `removedCollectorTokens`
  fails the build if deleted symbols return — keep them gone, don't delete the guard.
- **Toolchain:** .NET SDK `10.0.301` (`rollForward: latestFeature`), **C# 14**,
  `ANcpLua.NET.Sdk` family. Tests on **Microsoft.Testing.Platform**. Dashboard: ESM,
  Node ≥ 20, React 19 + Vite + TanStack + Tailwind 4; no new external runtime deps.
- **Ports:** REST/dashboard/OTLP-HTTP `:5100`, OTLP/gRPC `:4317`, OTLP HTTP `:4318`.

## Version drift found (noted, not yet fixed — Fable fixes in step 1)

1. **`Version.props:14`** `ANcpLuaRoslynUtilitiesVersion = 2.2.33` is **stale**;
   `global.json:11-12` (SDK + Testing) already at **`2.2.35`** (commits #493/#494).
   → bump Version.props to `2.2.35`. **Real drift.**
2. **`docs/package-api-update-matrix.md:30-31`** lists Roslyn.Utilities `2.2.26 → 2.2.27`
   — stale "latest" rows (actual 2.2.35). Historical log, but the top row is now wrong.
3. **`docs/package-api-update-matrix.md:60`** shows `Qyl.Api.Contracts 0.2.0 → 0.2.1`
   but `Directory.Packages.props:88` = **`0.2.2`**. Matrix one release behind.
4. Product version consistent at `0.1.0` in both dashboard `package.json`; no `.NET`
   product `<Version>` exists yet.
5. **`nuget.config`** comment was doubly wrong: cited **"NuGet 6.13+"** (actual bundled
   NuGet in SDK 10.0.301 = **7.6.0**) and justified `packageSourceMapping` by "**multiple**
   package sources" while the file `<clear/>`s to a **single** source. **FIXED 2026-07-06:**
   rewrote the comment to the real reason (defensive supply-chain lock, not a requirement)
   and made it **version-number-free** so it can't rot again. **Beta blocker still stands:**
   step 4's local feed must be added *with* a matching `<packageSource>`/`<package pattern>`
   entry or restore fails (NU1100/NU1507) — now spelled out in the comment itself.
7. **Version-citing comments audited repo-wide — the rest are correct, not rot.** Verified
   `Directory.Packages.props:72-76` + `eng/build/build.csproj:34-37` security overrides:
   the cited `NuGet.Packaging 6.12.1` / `System.Security.Cryptography.Xml 9.0.0` are the
   **vulnerable transitive versions still pulled** (`Nuke.Tooling` → 6.12.1,
   `Microsoft.Build.Tasks.Core` → 9.0.0; override resolves 7.6.0 / 10.0.9). Override is
   **still required** — leave it. `renovate.json` OpenApi/js-yaml rationales match current
   pins. `docs/package-api-update-matrix.md` is a deliberate historical log (see #2/#3).
6. **Doc wording — "NativeAOT-verified"** (`README.md:62-64`, `docs/observability.md:57,73`):
   the claim legitimately refers to the **external**
   `Qyl.OpenTelemetry.AutoInstrumentation` library (`4.0.3`, verified in its own repo) —
   **not** the collector (backend receiver, `PublishAot=false`/`IsAotCompatible=false`,
   needs no AOT) and not in-repo `internal/qyl.instrumentation` (`IsAotCompatible`
   deliberately unset — ASP.NET-Core minimal-API surface has no upstream AOT
   annotations; only `packages/Qyl.Run` is truly AOT-flagged). Not a bug — a wording
   gap: a careful reader of "all three signals NativeAOT-verified" expects the in-repo
   instrumentation package to be AOT-flagged. **FIXED 2026-07-06:** README + both
   observability.md spots now scope the claim to the external library and state that
   collector/`qyl.instrumentation` neither need nor claim AOT.

---

## === FABLE PROMPT (execute; ≤3000 chars) ===

You are the sole dev shipping qyl's first local beta today. Work directly on `main`,
local only — no PR, no branch, no worktree, no CI wait. Force-push/reset/delete are
fine locally. This CLAUDE.md is your source of truth and memory: after each step append
to **Progress log** with the command + its output as evidence.

Goal (prompt ends here): a **buildable, packable, locally-runnable `0.1.0-beta.1`**.

Do, in order, stopping only on a blocker your tools can't solve:

1. Fix version drift: set `Version.props` `ANcpLuaRoslynUtilitiesVersion` to `2.2.35`.
   Refresh/annotate the two stale `docs/package-api-update-matrix.md` rows
   (Roslyn.Utilities → 2.2.35, Qyl.Api.Contracts → 0.2.2). `Version.props` is the only
   place package versions live — never hard-code in a `.csproj`.
2. Stamp the beta `0.1.0-beta.1`: dashboards keep base `0.1.0`; add the `-beta.1`
   prerelease suffix at the product/pack layer.
3. Build green locally: `dotnet build` (collector + Qyl.Run*), dashboard
   `npm ci && npm run build && npm test`. Fix real failures; don't delete the
   `BuildVerify` `removedCollectorTokens` guard to pass — keep removed symbols gone.
4. Pack + local-publish shippable packages (`Qyl.Run`, `Qyl.Run.Host`, generators) to a
   **local NuGet feed**; build collector + dashboard artifacts. NOTE: `nuget.config`
   `<clear/>`s to nuget.org only — add the local feed AND a matching
   `packageSourceMapping` entry or restore fails (NU1100/NU1507). Fix its stale comment
   too (says "6.13+"/"multiple sources"; actual NuGet 7.6.0, single source).
5. Verify it runs: `dotnet run --project packages/Qyl.Run.Host`. **Set
   `QYL_OTLP_AUTH_MODE=Unsecured`** or children crash-loop ("health probe timed out").
   Confirm collector `:5100`, OTLP `:4317`/`:4318`, dashboard reachable.

Evidence rule: report only work you can point to real command output for — words are
claims, tool output is proof. When done, write a short "beta ready" note here and stop.

## === END FABLE PROMPT ===

---

## Fable model notes (Anthropic Fable guidance — apply while executing)

- **Ground progress claims.** Report only work with evidence (command + output). Words
  are claims, not proof — only the evidence block counts. (Near-eliminated fabricated
  status reports in Anthropic's tests.)
- **Don't over-prescribe.** Fable degrades under too-prescriptive per-step checklists.
  Definition + trigger is enough; let the model sequence. Keep prompts short/precise.
- **Avoid the `reasoning_extraction` refusal.** Never instruct "explain your reasoning"
  or "show your derivation in the response" — it triggers a refusal classifier and forces
  fallback to Opus 4.8. Point wording at **tool/command output only**, never internal
  reasoning. Tool-results are fine to show; internal reasoning is not.
- **Verify with a fresh context.** When a step genuinely needs verification, a separate
  fresh-context verifier subagent that **re-executes** beats self-critique. Optional
  today given the lean/local pattern — use only where a claim really needs proof.

## Tooling tips (optional, post-beta)

- **mergiraf** — Rust, AST-based git merge driver ("structure over lines"): resolves
  trivial conflicts (imports, attribute/member order) on the syntax tree via one
  `~/.gitconfig` entry. Low priority today (no branches/merges); keep for later.

## Progress log (Fable appends here, newest last)

- 2026-07-06 — SSOT created by Claude. Drift + beta target + Fable prompt + invariants
  captured. Nothing built yet; Fable to start at step 1.
- 2026-07-06 — Comment-rot sweep (Claude): fixed stale `nuget.config` comment
  (version-free rewrite). Audited every version-citing comment repo-wide — security
  overrides in `Directory.Packages.props`/`eng/build/build.csproj` verified STILL NEEDED
  (Nuke pulls vuln 6.12.1/9.0.0), left intact. Fixed 2 dangling `AGENTS.md` refs created
  by its deletion (`docs/observability.md`, `.github/copilot-instructions.md`). README +
  observability.md AOT-wording scoped to the external library. Committed to main.
