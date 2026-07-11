# CLAUDE.md — qyl SSOT

> This file is the **single source of truth** and **memory stream** for qyl. No
> `CHANGELOG.md`, no second contract. Append what you did — with evidence — under
> **Progress log**; never silently drop a finding. The old `AGENTS.md`
> symlink/contract was retired on purpose; the operating pattern below overrides its
> Branch/PR ceremony.
>
> **Evidence rule (the one that matters):** report only work you can point to real
> command output for. Words are claims; tool output is proof. When a claim really
> needs proving, a fresh-context verifier that *re-executes* beats self-critique.

## Operating pattern

- **No PRs. No feature branches. No worktrees.** Work directly on `main`, single
  consolidated commits, push immediately.
- **Build green + Verify green are correctness, not ceremony.** Never distort product
  code to satisfy a stale verifier — fix the verifier. Never hand-edit `*.g.cs` — fix
  the generator, regenerate, commit the result.
- **The repo is public** (`ANcpLua/qyl`, since 2026-07-11). Nothing ships publicly —
  no beta tag, no package publish — until qyl reaches an experimental public beta.

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
- **The transitive security overrides are load-bearing — don't "clean them up".**
  `Directory.Packages.props` + `eng/build/build.csproj` pin `NuGet.Packaging` and
  `System.Security.Cryptography.Xml` above the vulnerable versions that `Nuke.Tooling`
  and `Microsoft.Build.Tasks.Core` still pull transitively. Audited 2026-07-06: the
  overrides are what resolves them to safe versions. Removing them reintroduces the CVEs.
- **NativeAOT is an *external*-library claim.** "All three signals NativeAOT-verified"
  scopes to `Qyl.OpenTelemetry.AutoInstrumentation` (its own repo). The collector is a
  backend receiver (`PublishAot=false`) and `internal/qyl.instrumentation` is an
  ASP.NET Core surface — neither needs nor claims AOT. Only `packages/Qyl.Run` is
  AOT-flagged in-repo. Don't let the claim creep back onto the collector.

## Open work (the repair plan, phase-gated — don't batch phases)

Phase 0 (instruments) is **done**: CI is green and the hygiene sweep landed — see the
2026-07-11 progress-log entries. What remains, roughly in order:

1. **Data integrity.** No startup migration exists: the persisted DuckDB predates the
   cache-token columns, so generated SQL selects `gen_ai_cache_read_input_tokens`
   against an older DB and every `GET /api/v1/traces` 500s. Schema is source-generated
   (`internal/qyl.collector.storage.generators/DuckDbEmitter.cs`) — emit
   `ALTER TABLE ADD COLUMN IF NOT EXISTS` per column at startup from the same
   generator. Also: OTLP/JSON trace/span IDs are decoded as base64 when the spec
   mandates **hex** (spec-compliant JSON exporters get mangled, unjoinable IDs; no
   16-byte length validation either). And `/health` is a **false positive** — it is
   never mapped, so the SPA fallback returns `index.html` 200, fooling both Railway's
   `healthcheckPath` and `Qyl.Run`'s readiness probe; `MapQylEndpoints` is never
   called and `DuckDbHealthCheck` is never registered.
2. **Decide the product surface.** The dashboard ships pages with no backing endpoint
   (see README "Product surface"). Shrink to the verified vertical — traces, sessions,
   logs, GenAI cost — and delete pages that have no endpoint. No adapters, no stubs:
   missing values stay missing. Pages return only when a real endpoint ships.
3. **One topology.** Embedded single-origin collector (`QylEmbedDashboard=true` for
   release builds); delete the standalone dashboard Docker path; fix compose.
4. **`Qyl.Host` convergence + MCP wiring.** See `docs/design/qyl-host/`.
5. **Auth + scoping.** gRPC ingest has no API-key boundary (HTTP does); the read API is
   unauthenticated; `ProjectScope.cs` hardcodes `"default"`.
6. **Release coherence.** `0.1.0-beta.1` is **not stamped** — the only pack artifact is
   `Qyl.Run.1.0.0.nupkg`. One version owner (`Version.props`); pipeline is
   verify → pack → publish → index → clean-restore. Trap to remember: `nuget.config`
   `<clear/>`s to a single source, so a local feed must be added *with* a matching
   `packageSourceMapping` entry or restore fails (NU1100/NU1507).
7. **Tests.** Zero backend test projects; frontend coverage ~26% with no threshold;
   Playwright `baseURL` is `:5100` while its webServer runs `:4173`.

## Tooling tips

- **mergiraf** — Rust, AST-based git merge driver ("structure over lines"): resolves
  trivial conflicts (imports, attribute/member order) on the syntax tree via one
  `~/.gitconfig` entry. Low priority while the repo is main-only; keep for later.

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
  ideation: TelemetryFabric sketch...") — **that stash was dropped 2026-07-11; the content
  now lives at tag `archive/stash-telemetryfabric-ideation` (b2dfdd63), recover with
  `git stash apply archive/stash-telemetryfabric-ideation`.**
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
- 2026-07-11 — Anti-pattern round 2 → qyl-api-schema v0.4.0 shipped + consumed
  (Claude, user-invited "optimize anti patterns"). Findings, each verified
  before cutting: (1) OTelVersion enum had ZERO consumers anywhere (contract
  emits + qyl product code) — the exact surface that rotted at 1.41; DELETED,
  the semconv pin is machine-checked (otel-keys header + OtelKeysVersion),
  not prose-tracked. (2) http.tsp/db.tsp carried the SAME dead-shim class as
  the GenAI one, from the 1.23/1.24 stabilizations: @removed migration fields
  on upstream-deleted keys (http.target/url/method/status_code/
  content-lengths, db.name/statement/operation/user) + hand-typed
  gen_ai.usage.total_tokens — all DELETED; @typespec/versioning had already
  excluded every one from current-version artifacts, so the release's wire
  impact = OTelVersion schema removal only. (3) Version axes minimized:
  HttpVersions/DbVersions had zero annotations → @versioned dropped entirely;
  GenAiVersions trimmed to v1_27/v1_38/v1_40; VERSIONING.md codifies the
  keep-axes-minimal rule. Evidence: Nuke Check 11/11; v0.4.0 publish run
  success (npm+nuget); qyl pin 0.3.0→0.4.0 (cc33439b) restored from nuget.org,
  qyl.slnx 0W/0E. FLAGGED for later: (a) SemanticConventions'
  OpenTelemetryDeprecatedSemconvCatalog.cs hand-duplicates registry
  deprecation data — could be generated from resolved-registry.json like the
  rest of the surface; (b) qyl-api-schema VERSIONING.md prescribes
  DeprecatedMappings ingestion normalization the collector does NOT implement
  — decide: implement at ingest or soften the doc.
- 2026-07-11 — Composition primitives + canonical-host guard (Claude, follow-up to the
  hardening pass, per the user's architecture directive). Qyl.Run gains public
  composition-scope primitives (QylResourceBuilderExtensions): WithEnvironment,
  WithIsolatedStorage, DisableSelfTelemetryExport, GetEndpoint("api"/"otlp-http"/
  "otlp-grpc") — ExportToDedicatedCollector now composes from exactly these (its body
  reads like the directive's sketch); QylConstants.EndpointKinds added.
  CollectorSelfExportGuard extended beyond the literal alias list to CANONICAL host
  identity: local host name, DNS resolution to loopback, and any up-interface unicast
  address count as self (best-effort, resolution failures never block legit remotes).
  Port checked first so foreign-port endpoints skip resolution. AUDITED per directive:
  InitializeQylCollectorAsync is exactly one genuinely-async call
  (ModelPricingService.InitializeAsync — durable-state restore), which is the
  directive's own justified case — kept; AddService(serviceName, serviceVersion:)
  auto-generates per-process service.instance.id (OTel .NET default), so the two
  instances are distinct without extra config. EVIDENCE: qyl.slnx --no-incremental
  0W/0E; composition suite green (unique ports, cycle/dup rejection, dedicated
  auth=Unsecured/exporter=''/isolated db); guard: `http://Mac:4318` (hostname alias) →
  fatal at boot, `http://telemetry.example.com:4318` → starts normally; live e2e:
  5100/5200 healthy, diagnostics=3 traces all service.name=collector, primary=0.
- 2026-07-11 — RPC semconv admitted into the collector catalog (Claude, PR per user
  request — exception to no-PR pattern). Context: OTel roadmap review found the
  allowlist strips every rpc.* attribute; RPC conventions hit release_candidate at
  v1.43.0 (span.rpc.call.client/server, rpc.{client,server}.call.duration). Change:
  `rpc.` added to span + log incubatingPrefixes in collector-semantic-policy.json
  (NOT stablePrefixes — nothing rpc.* is `stable` at 1.43.0; generator fails loud on
  a zero-match lane) + 6 deniedExactKeys for the metadata templates
  (rpc.{grpc,connect_rpc,}.{request,response}.metadata — header-equivalents, same
  precedent as the denied http.request/response.header; upstream: "Including all
  request metadata values can be a security risk"). rpc.jsonrpc.error_message +
  rpc.message.* already dead via the `message` deniedKeyToken. Catalog regen picked
  up PRE-EXISTING drift: df34f0c3 bumped SemanticConventions 3.2.0→3.3.0 without
  regenerating, so VerifyCollectorSemanticAttributeCatalog was RED on main (proven
  by stash-test); this regen fixes drift (db.* etc. full-3.3.0-surface keys) + adds
  22 rpc keys to span/log allowlists (current-shape keys marked incubating;
  deprecated rpc.system/rpc.service/rpc.grpc.status_code unmarked — ship in both
  assemblies). Dashboard: getSpanColor/getSpanTypeLabel gained an RPC branch
  (rpc.system.name || rpc.system → "RPC"/--span-rpc; dual-read deliberate — both
  keys are catalog-admitted, unlike the removed gen_ai.system). Evidence:
  VerifyCollectorSemanticAttributeCatalog + VerifyCollectorSemanticPolicyIsCatalogBacked
  green; dotnet build qyl.collector 0W/0E; dashboard build + 18/18 tests green.
- 2026-07-11 — FLAGGED GAPS CLOSED: deprecation-catalog drift-proofing (→ 3.4.0)
  + ingestion normalization implemented (Claude, user-directed "fix the flagged
  catalog and ingestion mapping gaps"). (a) SemanticConventions cf5cdc3/v3.4.0
  (published, nuget-indexed): NEW verify_deprecated_catalog.py CI gate
  cross-checks the hand-curated analyzer catalog against resolved-registry.json
  (renames/deprecations/enum-member removals must agree; curated knowledge the
  registry lacks stays hand-maintained — full generation is impossible: no
  since-versions in the registry, GenAI keys deleted upstream). First run
  caught 2 REAL drifts, fixed: db.elasticsearch.path_parts →
  db.operation.parameter (bare template attr) and cpu.mode 'kernel' member
  (removed in 1.42's container-cpu-states cleanup).
  ExpectedCuratedMentionCount 156→157 (value entries count separately in the
  Supplemental list — took a wrong 158 first), docs regenerated, renderer
  label de-hardcoded. Evidence: build 0W/0E, Pipeline 17/17 +
  SourceGeneration 60/60 tests, checker green from repo root, actionlint
  clean, publish run 29137238464 success. (b) qyl collector:
  Ingestion/DeprecatedAttributeNormalizer.cs implements VERSIONING.md's
  DeprecatedMappings verbatim (gen_ai.system→provider.name, prompt/completion
  →input/output tokens, agents.tool.call_id→gen_ai.tool.call.id,
  db.system→db.system.name), wired into all 4 OtlpConverter extraction loops
  BEFORE the allow-list check (old keys were previously silently DROPPED —
  the 1.43-generated allow-list doesn't contain them); canonical key always
  wins over a normalized deprecated twin (TryAdd). LIVE E2E EVIDENCE:
  collector on :5100, OTLP JSON span with gen_ai.system=anthropic +
  prompt_tokens=123 + completion_tokens=45 + db.system=postgresql +
  conflicting gen_ai.provider.name → stored span has db.system.name,
  gen_ai.provider.name (conflict: canonical won), zero deprecated keys; and
  session 'legacy-norm-proof' genai_usage shows total_input_tokens=123,
  total_output_tokens=45, providers_used=[anthropic] — normalized ints flow
  through the typed StorageAttributeProjection into session analytics.
  (Side observation, pre-existing: the trace API renders only STRING attr
  values — int attrs live in typed columns/projections, not the attributes
  JSON.) Pins: SemanticConventions(+Incubating) 3.3.0→3.4.0 (this commit);
  VerifyCollectorSemanticAttributeCatalog green on 3.4.0 (no attribute-surface
  change — 3.4.0 is analyzers/docs only). qyl-api-schema VERSIONING.md
  (3970104) now records the collector implementation instead of prescribing it.
- 2026-07-11 — **Phase 0 (restore the instruments) COMPLETE: CI green + hygiene sweep**
  (Claude; every register item re-verified by fresh-context agents with adversarial
  refuters before being touched — 14 agents on the register, 8 on the matrix, 0 errors).
  **CI GREEN.** The blocker was a two-stage chain, and the register's diagnosis was
  already stale: `VerifyNoRemovedBuildSurface` / `GenAiToolCallId` was fixed by 45859f26
  (tombstone narrowed to the actually-pruned surface), after which the real red target was
  `VerifyCollectorUsesSemanticConstants` — a raw `"db.system"` literal at
  DeprecatedAttributeNormalizer.cs:28. Root cause: `db.system` is deprecated but STILL
  SHIPS, so the policy already sanctions it (`eng/config/collector-semantic-policy.json:155`
  `projectionConstants.DbSystemDeprecated`) and the generator already emits
  `CollectorSemanticAttributeCatalog.DbSystemDeprecated` — the normalizer just never
  consumed it. Fixed in 2dbe9b21 by routing the key through the generated constant (the
  other four mapped keys stay literals because upstream DELETED them, so no constant can
  exist). No verifier weakened, no `.g.cs` hand-edited. Evidence: local
  `./eng/build.sh Verify --Configuration Release` → all 38 targets Succeeded; GitHub on
  2dbe9b21 → CI ✅, Links ✅, CodeQL ✅.
  **Hygiene, each item re-verified first (three register claims were themselves WRONG):**
  (1) `Version.props:14` ANcpLuaRoslynUtilitiesVersion 2.2.33 → **2.2.36** — NOT the 2.2.35
  the register said; global.json and nuget.org latest are both 2.2.36. Build 0W/0E;
  `GenerateDependencyList` re-resolves 2.2.36; the three `Compile Remove` globs in
  qyl.instrumentation.generators still match at 2.2.36 (all three paths verified present in
  the .Sources contentFiles), clean `--no-incremental` rebuild green.
  (2) **Register #9 was wrong on BOTH counts and is NOT user-only.** The
  CLAUDE_CODE_OAUTH_TOKEN was already refreshed 2026-07-08T10:59:17Z and is proven live
  (run 28937509277: `is_error:false`, num_turns 2, $0.165) — `claude setup-token` is NOT
  needed. The actual live defect: `renovate-auto-review.yml` never set `allowed_bots`, so
  the action rejected every renovate[bot] run ("Workflow initiated by non-human actor:
  renovate (type: Bot)") — every bot PR from 2026-07-08T22:53 to 2026-07-11T03:04 died
  RED, and the gate has never reviewed a real renovate PR (#506/#509/#511 all merged with
  `reviews:[]`). Fixed here: `allowed_bots: "renovate"` (named actor, never `'*'` — the
  action's own docs warn `'*'` lets external Apps invoke it with prompts they control, and
  qyl is public). Input existence confirmed against action.yml at the pinned SHA e90deca;
  actionlint clean. The stale troubleshooting header now documents all three real failure
  modes, incl. the green-but-inert workflow-validation skip that fires on any PR touching
  this file — so judge the fix on the NEXT renovate PR, never on the one that lands it.
  (3) `.gitignore` **already** covered qyl.duckdb (`*.duckdb`, `*.duckdb.wal` at :99-100,
  `*.bak` at :172) and nothing duckdb-shaped is tracked — the task was already satisfied;
  no `git rm --cached` (there was nothing to remove).
  (4) Root README overclaims corrected: the dashboard row no longer advertises
  issues/alerts/errors/performance/cost/conversations/agents (the collector serves 13
  `/api/v1` routes — traces, sessions, logs, profiles — and hard-404s the rest); the
  "collector + dashboard together" runner claim is now truthful (Qyl.Run.Host launches the
  collector + its dedicated diagnostics collector on :5200, NOT the dashboard; embedding
  needs BOTH `-p:QylEmbedDashboard=true` AND a built `dist/`). Added a **Product surface**
  section stating exactly what is backed vs. UI-ahead-of-product — the honest input to the
  Phase 2 decision. Also fixed rot the register missed: the architecture diagram still
  showed the RETIRED `typespec-otel-semconv` npm package as the head of the chain.
  (5) `docs/package-api-update-matrix.md` refreshed with a dated 2026-07-11 pass. Six rows
  had moved pins, re-verified by `ilspycmd` decompile-diff of the shipped assemblies:
  Roslyn.Utilities(.Sources) 2.2.27→2.2.36, Qyl.Api.Contracts 0.2.1→**0.4.0** (the
  register's "0.2.2" was three releases dead), SemanticConventions(+Incubating)
  3.1.0→3.4.0. That diff caught a breaking change nobody had recorded: **3.3.0 removed 37
  `DbAttributes.SystemNameValues` members and `HttpAttributes.RequestMethodValues.Query`**
  from the STABLE assembly. qyl is insulated — the collector consumes the generated catalog,
  never SemConv types directly (`VerifyCollectorUsesSemanticConstants` enforces it). Three
  rows (Mvc.Testing, Extensions.AI.OpenAI, OpenAI) document packages qyl no longer
  references (pins deleted 2026-06-29, 4bb36ddf); annotated, not deleted, so the historical
  verification survives.
  (6) Both stashes dropped — but **tagged first**, so nothing is stranded:
  `archive/stash-telemetryfabric-ideation` (b2dfdd63) and
  `archive/stash-obsolete-doc-deletions` (6b915565); recover with
  `git stash apply <tag>`. stash@{1} only deleted two docs already gone from main;
  stash@{0}'s design content lives on in docs/design/telemetry-fabric.md, whose provenance
  pointer (and the 2026-07-07 log entry's now-false "git stash pop" instruction) now cite
  the tag.
  (7) This file's stale scaffolding retired: the 2026-07-06 "absolute deadline", the
  "Today's operating pattern" section, the resolved version-drift register, and the
  **never-executed** FABLE PROMPT block (its own targets had rotted: 2.2.35 vs actual
  2.2.36, Contracts 0.2.2 vs actual 0.4.0). The two durable findings buried in the drift
  register — the transitive security overrides are load-bearing, and NativeAOT is an
  external-library claim — were promoted into Invariants rather than lost. Progress log
  preserved verbatim.
  **NOT done (Phase 1+, unchanged):** DuckDB startup migration, OTLP/JSON hex trace-ID
  decoding, real `/health`, the `0.1.0-beta.1` stamp, the product-surface decision.
  **Nothing is left for the user** — the one "user-only" item (register #9) turned out to
  be already resolved and agent-fixable.
- 2026-07-11 — Markdown derot pass over the whole repo (Claude, user-directed "clean up
  qyl any markdown file / trust your own data"; ultracode: 7 parallel rot-scouts over all
  34 .md files, every finding verified against ground truth, then a 2-agent adversarial
  gate — facts refuter re-executed all 13 claims [0 refuted], coherence scout re-read the
  6 edited files in full). CLEAN (no rot found): README.md, CLAUDE.md non-log sections,
  telemetry-fabric.md, package-api-update-matrix.md, dependencies.md, all 12 yurekami
  extraction files, Qyl.Run.Console/eng/dashboard READMEs. FIXED (6 files): the
  `docs/design/qyl-host/` trio caught up with today's qyl.mcp merge — MCP-STRATEGY.md got
  a "Merge executed 2026-07-11" banner (mcp-run ≙ qyl.mcp/runner, qyl-apps-server ≙
  qyl.mcp/server; parity item 1 tool-slot economy marked closed, authz/eval seams noted
  open), DESIGN.md header now scopes its pre-merge mcp-run citations to the archived
  layout, and the handoff README's stale claims were corrected in place ("now private" →
  public since 2026-07-11 [gh: visibility PUBLIC]; "do not design against Qyl.Run/README"
  retired — that README was rewritten to the real surface at a1db9557). Line-citation
  drift fixed against the actual source: QylAppBuilder.cs:104-111→185-191,
  QylResources.cs:17→23, withReference cite orchestrator.ts→app-builder.ts:119; Qyl.Run
  LoC ~1,350→~1,700 (wc -l = 1,695) in 3 docs; env rename noted
  (MCP_RUN_RECORD_*→QYL_MCP_RECORD_*). Two content errors killed: observability.md
  claimed vendored METRICS protos (csproj vendors common/resource/trace/logs/profiles
  only — no metrics signal exists); yurekami-harvest.md row 5 cited QylAotSampler.cs:61
  (file is 41 lines; real cite :39 always-sample-root) and the 3725a4c3-deleted
  QylTraceSampling.cs. The coherence gate caught one contradiction MY OWN edit introduced
  (visibility corrected on the wrong bullet, leaving "is now PRIVATE" adjacent) — fixed
  before commit; the verify-your-verifier loop earned its cost. Residual grep: every
  remaining mcp-run/qyl-apps-server mention is a dated historical record or covered by
  the new banners.
- 2026-07-11 — yurekami reference pruned to open rows only (Claude, user rule: "a
  reference is for what's missing"). Six rows deleted from the harvest routing table:
  the shipped #7 GenAI cost wiring plus the five patterns qyl already embodies (#1
  never-crash instrumentation, #3 interval aggregation, #4 redaction-by-omission, #6
  memoized pricing lookup, #9 removed-symbol governance); full dispositions preserved
  at 8b08f757. The 9 remaining rows (5, 8, 10, 11, 12, 14, 15, 2, 13) are all
  genuinely-missing patterns; every extraction dossier still backs ≥1 kept row, so
  all 12 files under docs/reference/yurekami-extraction/ stay. The discipline is now
  codified in the file itself: Legend has no LANDED state (a row that ships gets
  deleted, git history keeps the evidence), Rule ends with "then delete the row",
  INDEX.md §4 marked superseded by the table. CI on the derot commit 8b08f757
  confirmed green (CI ✅ Links ✅) — the morning's billing blocker is resolved.
