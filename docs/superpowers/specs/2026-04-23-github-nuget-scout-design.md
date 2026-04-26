# github-nuget-scout skill — Design

**Date:** 2026-04-23  
**Status:** approved  
**Location:** `~/.claude/skills/github-nuget-scout.md` (global)

---

## Problem

Valuable helpers and newer package versions exist upstream (ANcpLua/*, microsoft/agents, NuGet) but are discovered too late — after manual implementation is already underway. Hardcoded version references in skills go stale and cannot solve this systemically.

---

## Solution

A Claude Code skill that fires on intent-signal keywords, runs live tool research (NuGet API + `gh` CLI), and ends by updating code — not just reporting findings.

---

## Trigger Keywords

Fires when any of these appear in the user's prompt:

`refactor` · `rework` · `update` · `analyse` · `analyze` · `check` · `look for` · `think hard` · `ultrathink` · `important`

Invoked automatically by `using-superpowers` skill-checking behavior before any other response.

---

## Phases

### Phase 1 — Context scan (always first, read-only)

Read the live project state:

| File | Extract |
|------|---------|
| `Directory.Packages.props` | All `PackageVersion` entries and pinned versions |
| `global.json` | `dotnet-sdk` version, `msbuild-sdks` versions |
| `Version.props` (if present) | First-party package versions |

No guessing. No hardcoded baselines.

### Phase 2 — Research (always tool-driven)

**NuGet — for each relevant package:**

Query the v3 registration API — returns all versions including pre-release:
```
https://api.nuget.org/v3/registration5-semver1/{package}/index.json
```
Parse the full version list; filter for both stable latest and highest pre-release. This surfaces RC and preview builds hidden from standard `dotnet add package` output.

**GitHub — two searches in priority order:**

1. `gh search code <topic> --owner ANcpLua --limit 10`  
   Own ecosystem first — catches helpers already built in-house.

2. `gh release list -R <repo> --limit 10`  
   Latest releases including pre-release tags for the relevant upstream repo.

3. `gh search code <topic>` against upstream repos inferred from the query context  
   (e.g. `microsoft/agents`, `dotnet/extensions`, `dotnet/aspnetcore`).

The `<topic>` is extracted from the user's message — never hardcoded.

### Phase 3 — Gap analysis (signal-to-noise filtered)

Output only actionable findings:

- **Version gap:** `Package X` pinned at `1.2.0` · stable `1.4.1` available · preview `2.0.0-rc.1` exists  
- **Pattern gap:** `Pattern Y` already implemented at `repo/path/file.cs:42` — summary + link  
- **No gap:** explicit *"nothing newer found, proceed"* — silence is not an answer

### Phase 4 — Apply

Does not stop at reporting:

- Updates `Directory.Packages.props` or `Version.props` to the latest appropriate version  
  (stable unless the project is already on a preview track)
- Rewrites code under review to use the better upstream pattern if one was found
- Does **not** auto-commit — user reviews the diff before any commit

---

## Repo Priority Order

1. `ANcpLua/*` — own ecosystem, first-party helpers
2. `microsoft/agents` — MAF upstream
3. `dotnet/extensions` — MEAI and DI primitives
4. Inferred from query context — any other repo the topic points to

---

## Output Format (minimal)

```
SCOUT FINDINGS
==============
[NuGet]  ANcpLua.Analyzers  1.26.2 → 1.28.0-preview.1 available
[GitHub] ANcpLua/qyl: ExpiringCache already exists at internal/qyl.instrumentation/...
[GitHub] No upstream match for <topic> in microsoft/agents

APPLYING
========
- Bumping ANcpLua.Analyzers to 1.26.2 (stable, not jumping to preview)
- Replacing hand-rolled cache with ExpiringCache from qyl.instrumentation
```

---

## Non-goals

- Does not replace TDD or the fix/debug skills
- Does not auto-push or auto-commit
- Does not cache results — every invocation is a fresh tool query
- Does not fire on read-only questions (e.g. "what does X do")
