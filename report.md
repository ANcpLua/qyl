# qyl docs ↔ codebase reality check

- **Generated:** 2026-05-04 19:55–20:00 CEST
- **Branch:** `add-owner-automerge-tier`
- **HEAD:** `f86426d6` (ci(auto-merge): add owner tier)
- **Scope:** `/Users/ancplua/qyl/AGENTS.md` (CLAUDE.md is a symlink to it), `/Users/ancplua/.claude/CLAUDE.md` (global), and the `SKILL.md` files AGENTS.md references
- **Codegen commands actually executed end-to-end:**
  `./eng/build.sh GenerateSemconv`, `./eng/semconv/run-weaver.sh`, `./eng/build.sh Generate`

## TL;DR

The doc is mostly accurate on **what files exist** and **what the platform looks like**, but inaccurate on several **specific anchors agents are told to follow**: two cited files do not exist, the cited HTTP-client pattern is the opposite of what the file actually does, and the two `SKILL.md` references at the top of the MAF section don't exist on disk. The codegen commands all run and succeed — but `nuke Generate` produces ~26 k lines of TypeScript drift against the committed state, so the "regenerated output ships in the same commit" rule is currently broken for the JS http-client emitter.

---

## 1. Verified working as claimed

| Claim | Status |
|---|---|
| `CLAUDE.md` is a symlink to `AGENTS.md` | ✓ |
| `qyl.slnx`, `eng/compose.yaml`, `nuget.config` exist where stated | ✓ |
| All 11 generated-codegen paths in the table exist | ✓ |
| All 13 semconv `eng/semconv/model/qyl/*.yaml` files exist | ✓ |
| `QylAttr` (internal) + `QylAttributes` (public) constants classes match documented shape | ✓ |
| `[QylSkill]` / `[QylCapability]` attributes defined; `internal/qyl.mcp.generators/` exists | ✓ |
| `LoomToolEnvelope` non-generic with `Ok(data)` / `Fail<T>(error)` factory methods | ✓ |
| `InvestigationLineage.TryEnter()` exists; depth 3 / spawn 10 enforced via `QYL_AGENT_MAX_DEPTH` / `QYL_AGENT_MAX_SPAWNS` | ✓ |
| `[McpServerTool]` methods are `partial` (sampled `ErrorTools`, `AutofixMcpTools`, `RcaTools`) | ✓ |
| `DuckDbStore` exposes `GetReadConnectionAsync(ct)` + `ExecuteWriteAsync(Func<DuckDBConnection, CancellationToken, ValueTask>, ct)` | ✓ |
| `.UseQylMcpInstrumentation(...)` called in `QylMcpServerRegistration.cs:78` and `qyl.loom/Program.cs:35` after transport | ✓ |
| `GenAiInstrumentation.cs:53/100/141` cite real `WithQylTelemetry` / `UseQylTelemetry` / `UseQylAgentTelemetry` definitions | ✓ |
| `ExplorationOrchestrator.cs:37`, `AutofixWorkflowFactory.cs:44-51`, `ExplorationWorkflowFactory.cs:23-28` all match | ✓ |
| 17/17 user-facing Nuke targets exist; sub-targets are `.Unlisted()` | ✓ |
| All 23 `MAF.Advanced.Patterns` classes + 5 sibling packages + `QylLoomShowcase` sample exist | ✓ |
| No `dynamic` / `ExpandoObject` / `.Result` / `.Wait()` in business code (one safe `IsCompletedSuccessfully` fast-path in `LspClientWrapper.cs:101`) | ✓ |
| No `DateTime.UtcNow` / `Now` / `DateTimeOffset.UtcNow` in `services/` | ✓ |
| ESLint dashboard rules (`@radix-ui` ban, `asChild`/`Slot` ban, `argsIgnorePattern: '^_'`) all present | ✓ |
| GitHub state: PR #172 merged 2026-04-28, issue #173 open | ✓ |
| `~/framework/` siblings (ANcpLua.Agents, ANcpLua.Analyzers, ANcpLua.NET.Sdk, ANcpLua.Roslyn.Utilities, MAF.Advanced.Patterns) all exist | ✓ |

---

## 2. Wrong / made up

### 2a. Files that don't exist

| Cited at | Path | Status |
|---|---|---|
| AGENTS.md:217-218 | `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs` | **Does not exist.** Directory has Confidence/Context/Fixability/Hypothesis/HypothesisJudge/Report/SelfCritiqueRouter/Solution/StoppingPointGate executors plus a stray `NewFile1.md`, but no `Rca` executor. |
| AGENTS.md:222 | `internal/qyl.instrumentation/Instrumentation/Loom/LoomToolFactoryBridge.cs` | **Does not exist** anywhere under `internal/`. |
| AGENTS.md:198-199 | `~/.claude/skills/microsoft-agent-framework-qyl/SKILL.md` | **Does not exist.** `~/.claude/skills/` contains only `.DS_Store`. |
| AGENTS.md:198-199 | `~/.claude/skills/microsoft-agent-framework/SKILL.md` | **Does not exist.** Same as above. |

The MAF cheat-sheet rows that point at `RcaExecutor.cs:35-40`, `:42`, and `LoomToolFactoryBridge.cs:99-119` are cargo-culted — agents pointed at those line ranges will land on missing files.

### 2b. Wrong line ranges

| Cited at | Claim | Reality |
|---|---|---|
| AGENTS.md:219 | `services/qyl.loom/Autofix/AutofixAgentService.cs:66-77` contains the streaming `await foreach … WatchStreamAsync` loop | File is **only 67 lines total**. The streaming loop is in `services/qyl.loom/Autofix/LoomAutofixRunner.cs:188`. |

### 2c. Pattern claim inverted from reality

**AGENTS.md:127-135 — "HTTP client error handling"** claims `services/qyl.loom/CollectorClient.cs` follows a specific pattern: `Content.Headers.ContentType?.MediaType == "application/json"` check, `try/catch (JsonException)` on non-success paths, and "structured DTO failures, throwing on expected HTTP error status is the exception". 

The actual file calls `response.EnsureSuccessStatusCode()` on every call (~20 sites), with no ContentType check and no JsonException wrapping. The pattern the doc describes simply isn't there — either the convention is aspirational and was never adopted, or the file regressed away from it.

### 2d. Inaccurate but close

| Cited at | Claim | Reality |
|---|---|---|
| AGENTS.md:260 | `QylTelemetryExtensions` defines `WithQylTelemetry` | `WithQylTelemetry` exists, but in `QylWorkflowExecutionExtensions.cs`, not `QylTelemetryExtensions.cs`. The latter file contains `BeginQylSpan`/`WithQylSpanAsync`/`SetQylOperation`/`SetQylTag`. |
| AGENTS.md:62 | `<LangVersion>preview</LangVersion>` is enabled | Not set at root. `Directory.Build.props` defers to SDK defaults; individual csprojs declare `latest` or `14`. The "preview features are fair game" assertion isn't actually wired up at the root. |
| AGENTS.md:66 | `FakeChatClient` "lives in `tests/qyl.collector.tests/Instrumentation/`" | Lives in the external `ANcpLua.Agents.Testing` NuGet package. The tests just import it via `using ANcpLua.Agents.Testing.ChatClients;`. |
| AGENTS.md:89 | `Verify` target description (4 sub-checks) | Actually 5 sub-targets — also runs `VerifyGeneratedFilesClean` (the CI gate that flags drifted generated files; relevant to §3 below). |
| AGENTS.md table around line 145 | `GenerateSemconv` referenced in Codegen Boundaries | But **omitted from the Nuke workflow table** at lines 82-100. It exists at `eng/build/BuildPipeline.cs:45-107` and is callable via `./eng/build.sh GenerateSemconv`. |

---

## 3. Codegen commands — actual behavior vs claims

All three commands ran successfully on this machine. But they don't do exactly what the doc claims, and one of them produces non-trivial drift.

### `./eng/build.sh GenerateSemconv` — runs in <1 sec

| Doc claim (line 145) | Reality |
|---|---|
| Generates `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs` | ✓ Generates that file. |
| (nothing else listed) | Also regenerates **all** of `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Attributes/**` plus `SchemaUrl.g.cs` and `SchemaVersion.g.cs`. |

Output is deterministic against committed state — zero diff after run.

### `./eng/semconv/run-weaver.sh` — runs in <1 sec

| Doc claim (lines 144,146) | Reality |
|---|---|
| Generates `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}/Attributes/**` | **Wrong.** This script does **not** write to those paths. (`GenerateSemconv` does — see above.) |
| Generates `packages/Qyl.Telemetry/Conventions/Qyl.g.cs` | ✓ Generates that file. |
| (nothing else listed) | Also generates: `services/qyl.dashboard/src/lib/semconv.ts` (1368 lines), `services/qyl.collector/Storage/promoted-columns.g.sql` (1369 lines), `core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json` (675 attributes), `packages/qyl-client/src/conventions.ts`, `docs/attributes/qyl.attrs.md`. |

Output is deterministic — zero diff after run. There's also an `ℹ No registry manifest found: …/eng/semconv/model/qyl/manifest.yaml` notice (informational, not blocking).

### `./eng/build.sh Generate` — runs in ~22 sec

Pipeline: `GenerateSemconv` → `TypeSpecInstall` → `TypeSpecCompile` (which runs 7 emitters in turn).

| Doc claim (line 143) | Reality |
|---|---|
| Generates `packages/Qyl.Contracts/Generated/**` | ✓ |
| Generates `packages/qyl-client/src/generated/**` | ✓ but with drift — see below |
| (not listed) | Also writes to `services/qyl.collector/Storage/` (DuckDB emit), `packages/Qyl.Client/Generated/` (HTTP-client csharp emit, `Qyl.Client.slnx`), `packages/qyl-client/schemas/qyl-api` (JSON schemas), and `services/qyl.collector/Generated/` (server csharp + README + docs). |

**Drift:** running this command from a clean tree produces **49 modified files, 11 629 insertions / 14 792 deletions**. Stripping whitespace (`git diff -w`) still leaves **45 files, 890 insertions / 4 053 deletions** — so it's not just formatting. The drift is concentrated in:

- `packages/qyl-client/src/generated/**` — biggest delta is `models/internal/serializers.ts` (8 492 lines) and `models/models.ts` (8 495 lines)
- `packages/qyl-client/schemas/qyl-api`
- `services/qyl.collector/Generated/{README.md,docs/emitter.md,docs/usage.md}` (output by `@typespec/http-server-csharp`)

The C# server output under `services/qyl.collector/Generated/generated/` is **clean** (0 drifted files). So the issue is specific to the JS-side TypeSpec emitters and the http-server emitter's docs. The `@typespec/http-client-csharp` emitter also showed a `⚠` warning during the run.

**Implication:** AGENTS.md line 150 — "Source change + regenerated output ship in the same commit" — is currently broken. The committed `qyl-client` does not match what the local toolchain produces. Verify's `VerifyGeneratedFilesClean` sub-target (which the doc inaccurately omits) would presumably flag this in CI.

---

## 4. Not checked

- Whether issue #1 in `Alexander-Nachtmann/MAF.Advanced.Patterns` actually says what AGENTS.md:292 claims (different repo, didn't query).
- Whether commits `12a746f` and `bae3d2e` in MAF.Advanced.Patterns match the consolidation status descriptions on AGENTS.md:20-21 (would need git log in that other repo).
- The `ANcpLua.Agents` → `MAF.Advanced.Patterns` consolidation phases 1-7 — only verified the resulting packages exist, not that the migration commits did exactly what the bullet points say.
- `packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/**` (non-Incubating) — neither command above wrote to this directory in this run, but the directory has 90 generated files committed. Some other path generates them; AGENTS.md:144 attributes them to `run-weaver.sh` which is incorrect (see §3). The actual generator wasn't pinned down in this session.

---

## 5. Working-tree state at end of session

The codegen-drift modifications were restored before writing this report. The drift was real (recorded in §3) but not kept on disk — to reproduce it: `./eng/build.sh Generate` from a clean tree, then `git diff -w --shortstat HEAD`. Only the pre-existing `M .github/copilot-instructions.md` from session start and the new `report.md` remain.

---

## Recommendations (prioritised)

1. **Fix the cargo-culted file paths in AGENTS.md** — replace `RcaExecutor.cs` and `LoomToolFactoryBridge.cs` with files that actually exist, or delete those rows.
2. **Either create the two `SKILL.md` files or remove the references** at AGENTS.md:198-199 — agents will follow them and hit nothing.
3. **Decide whether `CollectorClient.cs` should match the documented HTTP-error pattern**, then either rewrite the file or correct the doc.
4. **Run `nuke Generate` and commit the drift** so the committed state matches what local tooling produces — or pin the TypeSpec emitter versions if reproducibility across machines is the issue.
5. **Add `GenerateSemconv` to the Nuke workflow table** at AGENTS.md:82-100; correct the codegen-boundaries table to reflect that `run-weaver.sh` doesn't write to `Qyl.OpenTelemetry.SemanticConventions{,Incubating}/Attributes/**` (and add the outputs it actually produces).
6. **Fix `AutofixAgentService.cs:66-77` line range** to point at the actual streaming-loop file (`LoomAutofixRunner.cs:188`).
