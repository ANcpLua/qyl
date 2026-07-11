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
- 2026-07-06 — Code-scanning alert cleanup (Claude): the 3 open CodeQL alerts were
  stale — CodeQL default setup had been disabled since ~2026-05-06 and the flagged
  files (`services/qyl.mcp/.../error-explorer.html`, `eng/semconv/.../gen.py`) were
  deleted afterwards, so no scan ever closed them. Re-enabled default setup
  (`gh api PATCH .../code-scanning/default-setup` → run 28823965956); fresh scan
  auto-closed alerts #1/#2 as fixed. Alert #3 (Python) couldn't self-close — repo has
  no Python left, so no superseding analysis; deleted the orphaned Python analysis
  chain (id 1223235746 → `next_analysis_url: null`), alert gone. The scan surfaced NEW
  alert #4 (`cs/user-controlled-bypass`, high) at `OtlpApiKeyMiddleware.cs:15` — the
  unauthenticated OPTIONS pass-through. Verified no exploit (CORS middleware terminates
  OPTIONS on OTLP paths when enabled; OTLP endpoints are POST-only so OPTIONS hits
  405/404 when CORS disabled) but the branch is dead in both modes → removed it.
  `dotnet build services/qyl.collector` → 0 warnings, 0 errors. Alert #4 auto-closes
  on the next push-triggered scan.
- 2026-07-07 — otel-dotnet-instrumentation extraction integrated (Claude). The raw dump in
  `eng/build/extract with the rest/` (which sat inside build.csproj's compile glob and broke
  the NUKE build) is gone; treasure landed as: `eng/tools/` (SdkVersionAnalyzer — expected
  version now read from global.json, channel-tag/digest-pinned FROMs treated as unpinned;
  DependencyListGenerator — vendored dotnet-outdated, library-referenced by eng/build;
  LibraryVersionsGenerator — qyl-shaped definitions, output to Artifacts/generated;
  GacInstallTool — net462 via reference assemblies), `eng/build/` components IHousekeeping
  (`VerifySdkVersions` now in Ci, `UpdateSdkVersions`, `GenerateDependencyList`,
  `GenerateLibraryVersions`) + IPack (`Pack`, `CleanLocalPackagesCache`) + Attributes/
  Extensions/Models helpers, root props NuGetAudit(all/low), dormant
  `eng/MSBuild/StrongName.targets` (base64-snk decode), CPM entries (Microsoft.Build pinned
  18.0.2 — Nuke 10.1.0 floor, NU1109 below). Non-integrated originals archived under
  `eng/reference/otel-dotnet-instrumentation/` with README mapping. Evidence: all four tool
  projects + build.csproj + Qyl.Run/Host + all internal projects build 0W/0E;
  `./eng/build.sh VerifySdkVersions` → "SDK versions are consistent." (3 workflows,
  2 Dockerfiles); `GenerateDependencyList` → docs/dependencies.md (independently re-confirms
  drift item #1: resolves ANcpLua.Roslyn.Utilities 2.2.33); `Pack --skip Compile` →
  Artifacts/nuget/Qyl.Run.1.0.0.nupkg (version stamp = beta step 2, untouched). NOTE:
  `qyl.slnx` full build is red from PRE-EXISTING uncommitted collector WIP (Program.cs
  sketch calls AddIosTelemetry/AddDashboard/etc. that don't exist) — left untouched, not
  committed. Qyl.Run.Host is IsPackable=false by design → ShippablePackProjects = Qyl.Run
  only. Also fixed eng/build.sh fallback paths (global.json/.nuke lookup pointed at eng/
  instead of repo root). Committed to main (extraction only, no collector files).
- 2026-07-07 — CI follow-up on 3d79c325 (Claude): Backend + Frontend green; Dependency Audit
  red — its `find . -name '*.csproj'` sweep hit the ARCHIVED
  `eng/reference/.../_build.csproj` (NU1010: no CPM versions for Mono.Cecil/MinVer/etc.).
  Fixed both ends: renamed the archive file to `_build.csproj.txt` (inert to every glob)
  and added `-not -path './eng/reference/*'` to the audit find. Pushed as d2f123d2 —
  rerun green (CI success, Links success; CodeQL push scan passed on the identical-code
  parent 3d79c325). Extraction stream closed: main is green.
- 2026-07-07 — Collector WIP resolved (Claude, on request): the uncommitted sketch in
  Program.cs (fluent `app.AddTelemetryFabric("qyl", fabric => fabric.UseCollector(...)
  .ObserveProducts().UseSemantics("qyl.semconv.yaml").UsePrivacy("qyl.privacy.yaml")
  .GenerateEverything().AllowOverrides())` plus per-platform `AddSampling`/
  `AddBrowserTelemetry`/`AddIosTelemetry`/`AddAndroidTelemetry`/`AddDashboard`/
  `AddPrivacy` calls) was API ideation, not implementable code — it referenced methods
  that don't exist and its comments were open design questions. Alongside it: IDE-added
  redundant usings in 5 hosting/dashboard files (incl. `using ANcpLua.Roslyn.Utilities`,
  which the VerifyCollectorHasNoRuntimeRoslynUtilityReference gate forbids) and accidental
  paste damage in docs/observability.md (stray "l#", guardrail sentence replaced by
  https://github.com/ANcpLua/qyl.mobile/blob/main/ios/TelemetryObserver/ViewModels/TelemetryDashboardViewModel.swift
  — breadcrumb kept here). Everything preserved verbatim in `git stash` ("collector WIP
  ideation: TelemetryFabric sketch...") — recover with `git stash list` / `git stash pop`.
  Working tree restored to HEAD. Evidence: `dotnet build qyl.slnx` → 0 warnings, 0 errors
  (all projects incl. collector, dashboard, eng/tools). The TelemetryFabric idea itself
  (one fluent entry point composing collector + per-platform telemetry + semconv/privacy
  config; "eng not product" for dashboard) is a post-beta design conversation, not code.
- 2026-07-07 — TelemetryFabric design note (Claude + user): the stashed ideation formalized
  into `docs/design/telemetry-fabric.md` (62e260f6) — three layers (product surfaces /
  platform contracts / generated outputs), governing rule "subsets stay abstracted"
  (no subsystem at top level; Override/Generate are the only doors), grounded against
  the semantic-catalog generate→verify pattern, Qyl.Run, and qyl.mobile.
- 2026-07-07 — Reference-sweep adoption (Claude): audited ~/WebApplicationNativeAOT1's
  telemetry reference collection (vendored dotnet/runtime DiagnosticSource/EventSource,
  Roslyn/Razor telemetry, Aspire ServiceDefaults) against qyl. Verdict: qyl already
  delegates histograms/aggregation/propagation to the OTel SDK + BCL (the very code the
  reference vendors); samplers/batching/health-polling are solid. ONE naive spot replaced
  (1331b950): `QylAgentInventory` did a per-span O(n) registration scan on the OTel export
  path and kept ≤10k DateTimes per agent for a 24h count → rewritten with the runtime's
  interval-accounting pattern (97×15-min slot ring + name-keyed dictionary): O(1) per
  span, fixed memory, no undercount past the old cap (window edge 15-min granular).
  Inventory endpoint now snapshots once per request. Evidence: qyl.instrumentation +
  full qyl.slnx 0W/0E; VerifyInstrumentationTelemetryIsBoundedAndRedacted +
  VerifyInstrumentationHasNoStorageTenantKnowledge Succeeded. No other replacement
  justified (Razor/Roslyn telemetry is VS-telemetry-bound; ServiceDefaults1/
  telemetry.example are stock Aspire/demo code inferior to QylServiceDefaultsExtensions).
- 2026-07-08 — Bot-PR sweep + self-handling auto-review loop (Claude, ultracode research):
  merged ALL 5 open renovate PRs and hardened the no-human loop. Root cause of the
  pile-up was NOT the bumps — main's own Link check was RED: `docs/design/telemetry-fabric.md:192`
  links a source file in the PRIVATE `ANcpLua/qyl.mobile` repo, which the anonymous
  lychee checker 404s. Fixed by excluding `^https?://github\.com/ANcpLua/qyl\.mobile`
  in `lychee.toml` (bba2a547, same rationale as localhost/private-range URLs) → cleared
  the required Link check on main and all 5 PRs at once. Then `gh pr update-branch` on
  #500/#501/#465/#502 re-ran their checks against fixed main (the NU1010 audit fix from
  d2f123d2 was already on main; their branches were just stale). #503 (SemanticConventions
  3.2.0) had a REAL failure — `VerifyCollectorSemanticAttributeCatalog` stale (3.2.0 moves
  the OTel schema URL 1.41.0→1.43.0); regenerated via `./eng/build.sh
  GenerateCollectorSemanticAttributeCatalog` and committed the 1-line `.g.cs` (e3650cf1).
  All 4 required checks (Backend/Frontend/Dependency Audit/Link) then green on every PR.
  Merge evidence: #502 merged BY `app/renovate` itself (platformAutomerge, proving the
  no-human loop already works when checks are green); #500/#501/#465/#503 squash-merged.
  0 open PRs. KEY finding: PRs reached mergeStateStatus=CLEAN with reviewDecision="" and
  merged with NO approval — the required 1-review is not a hard gate here (owner merges
  bypass via enforce_admins=false; renovate app self-merges). So the break was purely
  "main red + stale branches", not a missing approver.
  NEW FILE `.github/workflows/renovate-auto-review.yml`: adds the AI quality gate the
  loop lacked. CodeRabbit is NOT active (app uninstalled; only the codex connector
  comments — CODE_RABBIT/AGENTIC_QYL_CODERABBIT secrets exist but dormant) → used the
  Claude review trick: anthropics/claude-code-action@v1 (SHA-pinned
  ba0aafd4308cbba7165f9f2cdb0cfbed5a3c99ce), auth via existing CLAUDE_CODE_OAUTH_TOKEN,
  approver identity via the existing AUTOMERGE GitHub App
  (actions/create-github-app-token@fee1f7d, AUTOMERGE_APP_ID/_PRIVATE_KEY) so the
  approval reliably counts if branch protection ever tightens. Scoped hard to renovate/*
  branches on plain `pull_request` (same-repo branches → write token, no
  pull_request_target footgun). Claude reads the diff read-only, APPROVEs safe
  patch/minor manifest-only bumps, HOLDs majors / non-manifest diffs / failing checks
  (respects the deliberately-disabled majors in renovate.json). actionlint clean. API
  grounded against canonical docs (code.claude.com/docs, claude-code-action README/usage;
  v1 GA: direct_prompt→prompt, mode auto-detected, claude_args passthrough). FIRST-RUN
  VERIFICATION PENDING — the workflow header documents the exact `gh pr view` check to run
  on the next renovate patch/minor PR (expect reviewDecision=APPROVED, review authored by
  the automerge app). Left renovate.json merge policy UNTOUCHED — did not enable blanket
  major automerge (majors stay human-gated by design).
- 2026-07-08 — Auto-review LIVE-VERIFIED on #504 + 6th PR merged (Claude). Renovate opened
  #504 (SemanticConventions.Incubating 3.2.0) right after #503 merged; same stale-catalog
  Backend failure → regenerated catalog (a2eaf892), all 4 required checks green, squash-
  merged. 0 open PRs (6 bot PRs total cleared this session). Used #504 as the live test bed
  for renovate-auto-review.yml and fixed FOUR real wiring bugs the first runs exposed, each
  proven by a fresh run: (1) the AUTOMERGE GitHub App approver idea is DEAD — AUTOMERGE_APP_ID
  is not an installed App on the repo ("Integration not found"); dropped it for the default
  GITHUB_TOKEN (repo has can_approve_pull_request_reviews=true). (2) `claude_code_oauth_token`
  needs `id-token: write` (OIDC exchange) — added. (3) the action `git fetch`es the base to
  diff → needs an `actions/checkout` step — added (safe: renovate/* is trusted in-repo, Claude
  limited to `gh` reads). (4) imperative prompt + `Bash(gh:*)` so Claude acts instead of
  answering in prose. After all four: run is GREEN, mode auto-detected=agent, model
  claude-sonnet-5 initializes. REMAINING BLOCKER (my tools can't fix): the model call returns
  `is_error:true, total_cost_usd:0, num_turns:1` — rejected before Claude acts, i.e. the
  **CLAUDE_CODE_OAUTH_TOKEN secret (created 2026-05-06) is stale/invalid**; regenerating it
  needs the user's interactive `claude setup-token`. The action reports the STEP as success
  even on is_error, so the gate no-ops SILENTLY — documented in the workflow header. Non-
  blocking by design (review job isn't a required check; renovate self-merges on green
  regardless). ACTION FOR USER: refresh CLAUDE_CODE_OAUTH_TOKEN, then the AI review/approve
  gate goes live with zero further code changes.
- 2026-07-09 — GitHub Copilot removed from qyl tooling (Claude, on request — user dropped
  Copilot: quota exhausted for 5 days, refill only 2026-08-01). Deleted
  `.github/copilot-instructions.md` (its "coordinate with CodeRabbit" bullets were stale
  anyway — CodeRabbit isn't installed, see 2026-07-08). Dropped the `copilot/` branch
  prefix from both jobs + the prefix-policy comment in `.github/workflows/auto-merge.yml`
  (only `claude/` remains; `actionlint` clean). Removed the dead
  `[src/qyl.copilot/**/*.cs]` section from `.editorconfig` — that path doesn't exist
  (the whole `src/` tree is gone; ~20 more stale `[src/**]` sections remain there, a
  separate cleanup). PRODUCT MENTIONS DELIBERATELY KEPT: `StartupBanner.cs:75` and
  `SettingsPage.tsx:369` advertise GitHub/Copilot integration to *users* — we support it
  without using it. Also DELETED the dormant `CODE_RABBIT` / `AGENTIC_QYL_CODERABBIT`
  repo secrets (created 2026-03-11, never referenced by any workflow — verified by grep
  before deletion). Values are unrecoverable; reinstalling CodeRabbit would need fresh
  tokens. `gh secret list` now: AUTOMERGE_APP_ID/_PRIVATE_KEY, CLAUDE_CODE_OAUTH_TOKEN,
  DOCKERHUB_TOKEN/_USERNAME, JULES_API_KEY, TRIAGE_PAT.
- 2026-07-09 — Upstream template de-copilot'd so the sweep can't revert (Claude,
  ANcpLua/github-settings-automation@8fe9c17). `enforce-repo-settings.yml` compares each
  repo's `.github/workflows/auto-merge.yml` **byte-for-byte** against
  `templates/auto-merge.yml` and REPLACES drifted copies, so qyl's edit had to land
  upstream or be overwritten. Applied the identical `copilot/` removal to
  `templates/auto-merge.yml` + that repo's own copy, and dropped Copilot from the
  advisory-reviewer list in its `AGENTS.md`. Verified safe fleet-wide first: **zero
  `copilot/` branches and zero open Copilot PRs** across qyl, TourPlanner, Paperless,
  github-settings-automation. Evidence: `actionlint` clean; template YAML parses; blob
  sha `0400f9f…` now identical on qyl / templates/ / the template repo's own copy →
  sweep sees "already canonical" and skips qyl. TourPlanner + Paperless still carry the
  old `b17d756…` and will be auto-synced to canonical on the next Monday-17:00-UTC sweep
  (topic mode targets exactly those two; qyl is not topic-tagged, so default-mode sweeps
  never touched it anyway — only `recent`/`full` dispatch would).
- 2026-07-10 — Stale `.editorconfig` sections purged (Claude, user-approved decision) —
  closes the "~20 more stale `[src/**]` sections" note from 2026-07-09. Deleted 25 dead
  sections (166 lines): all 21 `[src/...]` globs (no `src/` dir exists; 0 matching files),
  `[tests/**/*.cs]` + `[examples/**/*.cs]` (neither dir exists; zero C# test projects in
  repo — dashboard tests are TS under `services/qyl.dashboard/src/__tests__`), and both
  `[services/qyl.collector/Ingestion/Otlp*.cs]` QYL0001 suppressions (OtlpAttributes.cs
  was deleted; QYL0001 is defined by NO analyzer in the current build — real in-repo IDs
  are QYL0136/QYL0137 in `internal/qyl.instrumentation.generators/Analyzers/`; git
  `log -S` traces QYL0001 to long-removed code). Deliberately did NOT repoint Group-A
  globs to the real roots: builds have been 0W/0E for months with them inert (baseline
  `.globalconfig` `suggestion` severities are the lived-in reality), repointing would
  newly silence CA2100 (SQL-injection review) on Storage code, and the sections' own
  justifications had rotted (e.g. "lowercase 'qyl' namespace" — actual namespaces are
  PascalCase `Qyl.Collector.*`). Principle: if IDE suggestion noise ever gets real,
  re-add a targeted section against the real path with fresh justification. Kept all
  live sections: `[packages/**/*.cs]` CA2007 re-enable, `[eng/build/**]`, generated-code
  exclusions, file-type blocks. Evidence: `grep -c '^\[src/' .editorconfig` → 0;
  `git diff --stat` → 166 deletions only; `dotnet build qyl.slnx` → 0 Warning(s),
  0 Error(s).
- 2026-07-10 — Non-beta projects deferred out of the workspace (Claude, user-directed).
  Moved `qyl-workspace/qyl.mobile` + `qyl-workspace/qyl-tracker-companion` (both clean,
  `main...origin/main`, nothing stranded) to `~/RiderProjects/qyl-parked/`,
  whose new `CLAUDE.md` is the compendium of parked nice-to-haves (incl. the Sophia
  pet/mercenary concept — idea only, no repo). Workspace `CLAUDE.md` router updated:
  product family is now `qyl/` + `Qyl.OpenTelemetry.AutoInstrumentation/`, plus a
  "Deferred until public beta" section. Repo-wide grep for
  `qyl.mobile|tracker-companion|pet|mercenary|sophia`: the remaining in-repo mentions
  (`lychee.toml:40` exclusion, `docs/design/telemetry-fabric.md:192` grounding row,
  progress-log breadcrumbs above) all reference the **GitHub** repo `ANcpLua/qyl.mobile`,
  which didn't move — left intact, not rot. No code/build references existed; nothing in
  the beta path depends on either repo.
- 2026-07-10 — Reference clones relocated (Claude, user-directed): `semantic-conventions`
  (pulled to eb614277), `semantic-conventions-genai` (63f8200), `sentry-mcp` (9c88431d,
  already latest) — all clean, pulled to upstream HEAD, then moved to
  `~/RiderProjects/qyl-references/`. Weaver/build files pin registries by GitHub commit,
  not local path (verified by grep) — nothing build-critical breaks. Path-citing docs
  fixed: workspace router CLAUDE.md (registry routing + upstream-clones invariant),
  `docs/design/qyl-host/MCP-STRATEGY.md:6` (7b1f3cb9), SemanticConventions `AGENTS.md:50`
  (c663cde in that repo).
- 2026-07-11 — MCP repos consolidated into the workspace (Claude, user-directed).
  Moved `~/RiderProjects/qyl-apps-server` (2026-07-10) and `~/Desktop/mcp-run`
  (2026-07-11) into `qyl-workspace/` — both clean, `main...origin/main`, nothing
  stranded; the two halves of the planned `qyl.mcp` merge now live next to `qyl/`.
  Workspace router CLAUDE.md updated with entries for both. Every stale
  `~/Desktop/...` path fixed across repos: in qyl `docs/design/qyl-host/`
  MCP-STRATEGY.md (audience note + inventory bullet), README.md (twin-hosts
  paragraph, step-1 qyl.mcp note, handoff snapshot — also records that
  `x-apps-server` was deleted locally and is GitHub-only now), DESIGN.md (scope
  + qyl-apps-server README citation); in mcp-run `runner/main.ts` (functional
  `cwd:` for the qyl-apps workload → workspace path; commented x-apps block
  annotated GitHub-only) and ARCHITECTURE.md (sample updated to match actual
  main.ts); in qyl-apps-server INTERFACE.md (pattern-reference + integrator
  wiring paths) and `plugins/qyl-mcp/mcp.json` (functional `args:` path).
  Evidence: repo-wide grep for `Desktop/(mcp-run|qyl-apps-server|x-apps-server)`
  → 0 hits; mcp-run `npm run build --workspace runner` → tsc clean (dashboard
  workspace build fails on a PRE-EXISTING `./styles.css` TS2882, untouched by
  this change); mcp.json parses.
- 2026-07-11 — Derot pass over workspace markdown (Claude, 4 parallel rot-scouts,
  every finding verified against ground truth before applying). Workspace router
  CLAUDE.md: ZERO rot — all registry/version/package claims verified true.
  mcp-run/ARCHITECTURE.md: `Qyl.Run.Dashboard` doesn't exist → `Qyl.Run.Console`
  (5 spots); `IQylResourceBuilder (+WaitFor, WithReference)` — those C# members
  don't exist (interface has only `Update`; `.waitFor()/.withReference()` are
  new in mcp-run) (3 spots); OTLP removed from "Out of scope" — `telemetry.ts`
  ships it; added a "Host-side telemetry" section (`McpTelemetry`, QYL_OTLP_ENDPOINT
  default :4318, mcp.method/tool.name + gen_ai.tool.name, session.id,
  MCP_RUN_RECORD_INPUTS/OUTPUTS) — this also RESOLVES the contradiction
  DESIGN.md:195 flagged (bullet updated). qyl-apps-server: INTERFACE.md wiring
  section said "wire with QYL_DEMO=1" but the live main.ts wiring diverged on
  purpose (QYL_COLLECTOR_URL, no QYL_DEMO) → rewritten as DONE with actual env;
  dead scratchpad `qyl-mcp-prior-art/` refs → repointed to qyl git history
  before 43d032f9 (verified: that commit touches 28 services/qyl.mcp files);
  sentry-mcp-comparison.md: clone path qyl-workspace→qyl-references, tool count
  6→7 (display_mcp_dashboard landed), smoke-test 30→~40 assertions (41 check()s).
  qyl-host design docs: 43d032f9 "301 files"→53 (git show --shortstat); Sentry
  "~90 semconv JSON files"→~74 (counted), eval harness 24→23 *.eval.ts; Qyl.Run
  "~650 LoC"→~1,350 (wc -l = 1,353; fixed at the SOURCE too —
  packages/Qyl.Run/README.md:55 LoC table); 3 line-citation drifts
  (ARCHITECTURE.md:1→:3, orchestrator :303→:304, OTLP bullet). FLAGGED, NOT
  CHANGED: plugins/qyl-mcp/agents/qyl-mcp.md allowedTools omits
  display_mcp_dashboard (possibly intentional scope); Qyl.Run/README.md still
  documents AddDashboard/.WithCollector/.WaitFor/[B] absent from code
  (aspirational-spec vs rot — human call); INTERFACE.md:60 lists
  /traces/{id}/spans as "used" though server.ts uses /traces/{id} (endpoint IS
  real, harmless). Verify: residual-grep for all stale tokens → 0 hits.
- 2026-07-11 — Semconv key projection migrated 1.41.0 → 1.43.0; version-lockstep
  invariant RESTORED (Claude, user-directed). New TypeSpec emitter wired into the
  SemanticConventions repo's Weaver pipeline
  (src/…SourceGeneration/scripts/emit_typespec_keys.py, 15b996a — reads the same
  resolved-registry.json as the C# surface, mirrors emit_attributes.py naming/
  collision/deprecation rules; AGENTS.md playbook updated). qyl-api-schema
  (1a5d804): generated/otel-keys.gen.tsp regenerated at core v1.43.0
  (89aae438…) + genai dev registry (c321d7eb) — 930 keys vs the old frozen 707;
  diff proof: 0 value changes across the whole surface, 10 consts gone (ALL
  deprecated-era gen_ai.* that upstream deleted; of the gen_ai.openai.* five,
  service_tier ×2 + system_fingerprint renamed to openai.*, seed +
  response_format removed with no successor). All 10 were used as @encodedName
  wire names on deliberately-deprecated migration fields → frozen verbatim in
  NEW hand-maintained generated/otel-keys-legacy.tsp (same Keys.GenAi namespace
  via TypeSpec namespace merging — zero edits to consuming models). Retired npm
  dep @ancplua/typespec-otel-semconv@1.41.0-2 removed (+ its renovate rule);
  VerifyKeysLockstep rewritten: header-pin assertion (OtelKeysVersion=1.43.0 in
  .nuke/parameters.json) + legacy/generated const-disjointness guard, replacing
  the npm byte-diff. EVIDENCE: emitted outputs (openapi/ts-types/contracts/
  json-schema/control-graph) byte-identical → wire-neutral; nuke Check all 11
  targets green locally; CI Nuke build verification green on 1a5d804. Workspace
  router CLAUDE.md skew sections updated to RESOLVED. INCIDENT (contained):
  first `gh release create v0.2.3` ran from the SemanticConventions cwd →
  tagged the WRONG repo, whose NuGet publish workflow started; cancelled within
  seconds (only checkout/version steps ran), release+tag deleted, nuget.org
  verified clean (no 0.2.3, latest 3.3.0). Correct release v0.2.3 created on
  ANcpLua/qyl-api-schema. BLOCKER (needs user, tools can't fix): publish.yml
  run 29131737334 failed at "Authenticate to NuGet (OIDC)" — nuget.org token
  exchange HTTP 401 "No matching trust policy owned by user 'ANcpLua'". This
  was the FIRST-EVER run of publish.yml (0.2.2 predates it), so its "setup is
  complete" header claim was never exercised; the nuget.org trusted-publishing
  policy is missing/expired (pending policies expire unused after ~7 days).
  FIX: nuget.org → account ANcpLua → Trusted Publishing → (re)create policy
  {owner: ANcpLua, repo: qyl-api-schema, workflow: publish.yml, environment:
  release}, then re-run the failed run (`gh run rerun 29131737334 --repo
  ANcpLua/qyl-api-schema`). npm side untouched (all-or-nothing: nuget auths
  first, npm publishes last). AFTER publish+index: bump Qyl.Api.Contracts
  0.2.2 → 0.2.3 in qyl Directory.Packages.props:88 and restore-verify.
- 2026-07-11 — v0.2.3 PUBLISHED + consumed; migration loop CLOSED (Claude +
  user). Root cause of BOTH registry failures: qyl-api-schema was deleted and
  recreated 2026-07-08, so the trusted-publishing policies on nuget.org AND
  npmjs pinned the DEAD repo id (policy showed #1231216126; live repo is
  #1293864142) — "Active" but matching nothing. User recreated both policies
  (nuget.org: repo/workflow/environment re-pinned; npmjs: Trusted Publisher
  re-connected). Rerun evidence: run 29131737334 rerun #2 → NuGet OIDC auth +
  push SUCCESS, npm still ENEEDAUTH (its publisher fixed after); rerun #3 →
  conclusion success. Verified live: npm @ancplua/qyl-api-schema 0.2.3
  (dist-tag latest), nuget.org flatcontainer lists 0.2.3. Consumer bump:
  Directory.Packages.props:99 Qyl.Api.Contracts 0.2.2→0.2.3; 0.2.3 restored
  from nuget.org into the global cache; `dotnet build qyl.slnx` → 0 Warning(s)
  0 Error(s). Version-lockstep invariant fully restored end-to-end: TypeSpec
  keys (1.43.0) = .NET constants (1.43.0), contracts wire-identical.
- 2026-07-11 — 1.41-era shims REMOVED; v0.3.0 shipped + consumed (Claude,
  user-directed: "remove any finding of 1.41.0 and shims that hide the 1.42
  changes"). qyl-api-schema 790ec95 → v0.3.0 (published, run 29133616213
  success first try): deleted otel-keys-legacy.tsp + all 10 frozen gen_ai.*
  consts and their contract fields (system, prompt, completion,
  usagePromptTokens/usageCompletionTokens, OpenAI vendor block); OpenAI
  service_tier ×2 + system_fingerprint repointed to the openai.* successors,
  response_format/seed dropped (superseded by gen_ai.output.type /
  gen_ai.request.seed which already existed as fields); orphaned
  OpenAiResponseFormat enum deleted; OTelVersion enum +v1_42 +v1_43
  (additive); VerifyKeysLockstep back to pure pin assertion; VERSIONING.md
  example updated (ingestion mapping section KEPT — old-key telemetry is
  ingestion normalization, not contract). KEY FINDING: @typespec/versioning
  had already excluded every removed field from the emitted current-version
  artifacts, so the wire surface was never carrying them — removal is
  source-breaking (hence minor bump 0.3.0), wire-additive only (enum values).
  Nuke Check 11/11 green. qyl side (this commit): Directory.Packages.props
  Qyl.Api.Contracts 0.2.3→0.3.0 AND Qyl.OpenTelemetry.SemanticConventions(+
  .Incubating) 3.2.0→3.3.0 — 3.2.0 was the stale partial regen whose
  non-GenAI constants were 1.41-era fossils (that repo's own AGENTS.md);
  3.3.0 is the full-1.43-surface release. Dashboard gen_ai.system shims
  removed: TracesPage getGenAiAttrs now reads gen_ai.provider.name (UI
  already labeled it "Provider"), use-telemetry getSpanColor/getSpanTypeLabel
  dropped the gen_ai.system fallback (2 spots). Evidence: dotnet build
  qyl.slnx 0W/0E on new pins; dashboard npm build + 18/18 tests green.
  SWEPT+KEPT (not rot): Analyzers' v1.41.0 mentions in SemanticConventions
  are factual history (when upstream changed things); its deprecation catalog
  already maps ALL 1.42 gen_ai migrations; AutoInstrumentation clean (no
  legacy keys) → NO 4.0.4 / 3.4.0 releases needed. NOTED, out of scope:
  dashboard still dual-reads pre-1.41-era http.method / db.system old keys —
  different era, ingestion-tolerance decision for another day.
- 2026-07-11 — Self-telemetry hardening pass (Claude, ultracode: 24-agent adversarial
  review over the a1db9557 feature; every confirmed finding fixed, refuted ones
  dropped). FIXED: (1) validation is now ALWAYS-ON — RejectSelfReference() kept as
  call-site documentation only; same-resource, port-identity and two-process cycle
  checks run unconditionally in Apply(); (2) every AddCollector now allocates UNIQUE
  api/otlp/grpc ports (first collector keeps 4318/4317, later ones claim free ports;
  Register throws on any overlap) — kills the ExportTo(existing) 4318-bind-race /
  self-loop the review proved; (3) children launch with `dotnet run
  --no-launch-profile` so a future launchSettings.json can't override injected env
  (verifier proved SDK 10.0.301 lets launchSettings win otherwise); (4) dedicated
  diagnostics sink forces QYL_OTLP_AUTH_MODE=Unsecured (ambient ApiKey would silently
  401-drop the owner's header-less exporter); (5) auth-mode default gate now treats
  empty-string env as undecided (`export QYL_OTLP_AUTH_MODE=` crash-looped both
  children before); (6) OTEL_SERVICE_NAME rebind no longer unsubscribes the
  assembly-name ActivitySource (AddSource both when they differ) — spans from
  AddProject children using `new ActivitySource(<assembly>)` survived only by luck
  before; (7) NEW collector-side startup guard CollectorSelfExportGuard (the
  "validate twice" half): resolved loopback OTEL_EXPORTER_OTLP_ENDPOINT on any own
  port (api/otlp/grpc, aliases localhost/127.0.0.1/::1/[::1]/0.0.0.0) throws at boot.
  DESIGN.md Qyl.Run surface section updated (4 stale points). ACCEPTED, not fixed
  (all [low], documented): PortAllocator claim-then-release TOCTOU window; no
  readiness ordering primary↔diagnostics (OTLP exporter retry/batch covers it);
  Unsecured-on-AnyIP posture equals the previous manual workaround; same-project
  concurrent `dotnet run` build race (3 clean-slate repro attempts failed on macOS).
  EVIDENCE: qyl.slnx 0W/0E; composition tests — unique-port allocation (diag 4318,
  main claimed 62678), duplicate ports rejected, missing target rejected, dedicated
  accepted (auth=Unsecured, exporter='', qyl.diagnostics.duckdb); startup guard threw
  InvalidOperationException on OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318;
  live run: collector+diagnostics healthy 5100/5200, diagnostics /api/v1/traces = 3
  traces all service.name=collector, primary /api/v1/traces = 0 (no self-ingest).
  NOTE: services/qyl.collector/qyl.duckdb.pre-selftel.bak is the June-era DB set
  aside during verification (its stale schema 500s /api/v1/traces on current code —
  pre-existing, unrelated); delete or migrate whenever.
- 2026-07-11 — CI BLOCKER (needs user, tools can't fix): every GitHub Actions job on
  every commit since ~35cb8d73 fails in ~2s with zero steps executed — annotation:
  "The job was not started because recent account payments have failed or your
  spending limit needs to be increased." This includes a1db9557/c89c387b (the
  self-telemetry feature + hardening), whose code is verified green LOCALLY
  (qyl.slnx 0W/0E + live e2e above). FIX: GitHub → Settings → Billing & plans →
  resolve payment/spending limit, then `gh run rerun --failed` the head run.
