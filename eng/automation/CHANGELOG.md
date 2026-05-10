# Automation Workflow Changelog

This file is for cron-style automation runners, not product release notes or normal development narration. Read it before
starting a matching automation run. At the end, read it again after the final changelog write, or immediately before the
final no-op/blocker output when no repository write is allowed.

## Retention Contract

- Keep `Latest Changes` newest-first.
- Keep exactly the most recent 10 entries when 10 or more exist.
- When adding entry 11, delete the oldest entry in the same edit.
- Use `YYYY-MM-DD HH:mm TZ` timestamps.
- Record material outcomes only: changed refs/files, pushed commits, merged or closed PRs, deleted branches, exact blockers.
- Do not use this file as authority for branch or PR decisions; rebuild those from live git and GitHub state.
- Do not use this file as a product changelog; release notes are owned by the release workflow.

## Workflow: Upstream SemConv Generator Contribution

**Scope:** Maintainer-quality OpenTelemetry Semantic Conventions generator work for C#/.NET and possible upstream .NET or
dotnet-contrib contribution.

**Authority:**

- Upstream OpenTelemetry Semantic Conventions YAML model for the pinned release.
- Official OpenTelemetry semantic-conventions repository, raw model YAML, release notes, Weaver documentation, code-generation
  guidance, group/version-selection documentation, and current OpenTelemetry .NET or dotnet-contrib repository state.
- Repository-local generated output only after proving it was produced from the pinned upstream model.

**Non-authority:**

- Private qyl package shapes, branded artifacts, custom registries, cached chat summaries, generated output edited by hand, or
  unstated maintainer expectations.

**Run order:**

1. Read `AGENTS.md`, this file, and the semconv generator files under `eng/semconv/`.
2. Confirm the target upstream migration scope is v1.40.0 to v1.41.0 unless Alexander explicitly changes it.
3. Confirm the generator uses pinned Weaver and pinned upstream semconv source; never fetch `latest`.
4. Confirm release pins and declarative version-selection config are not mixed:
   - upstream release pin: tag-style source such as `v1.41.0`;
   - declarative config path: `.instrumentation/development.general.<domain>.semconv`;
   - declarative `version`: integer such as `1`.
5. Load model files recursively from `model/**/*.yaml` and `model/**/*.yml`; prove `model/graphql/spans.yml` is included if
   full model loading is in scope.
6. Discover groups from YAML `type`, not directory name.
7. Preserve group and attribute metadata needed for constants now and analyzers later: `span`, `metric`, `event`, `entity`,
   `attribute_group`, `extends`, local ref overrides, requirement levels, conditional requirements, stability, deprecation,
   enum-like `members`, and schema URLs.
8. Generate conservative C# output: constants and enum-like helper values only where YAML supports them; deprecated symbols
   must carry `[Obsolete]` or the chosen .NET deprecation mechanism.
9. Keep analyzer behavior out of constants packages unless maintainers explicitly ask for it.
10. Ship source changes and regenerated output in the same commit.

**Required checks:**

- Bash and PowerShell generation scripts read the same pinned semconv version and Weaver version.
- Clean checkout generation is byte-identical.
- Generated C# compiles.
- Deprecated generated symbols are marked.
- Schema URLs match the pinned semconv release.
- v1.41.0 breaking changes are visible in generated output when the affected surface is generated.
- Generated files are not manually edited.

**v1.41.0 migration checks:**

- GenAI execute-tool span naming has `gen_ai.tool.name` available as required by upstream YAML.
- GraphQL `graphql.document` is not treated as recommended if upstream YAML marks it opt-in.
- `process.executable` entity metadata and process identifying attributes are preserved.
- RPC server spans do not retain `client.address` or `client.port` when upstream YAML removed them.
- New domain-specific exception events across FaaS, GenAI, and Messaging use exception attributes from YAML, not invented
  `error.*` requirements.

**Result block:**

```text
semconv-generator
Changed: <files, generated outputs, scripts, or docs changed>
Evidence: <source tag/commit, commands, generated diff proof, compile/test output>
Pushed/Merged/Closed/Deleted: <refs, commits, PR URLs>
Blocked: <exact missing permission/data/tooling only>
```

## Workflow: branch-three Branch/PR Hygiene

**Scope:** Autonomous live-state branch and PR cleanup across the configured repositories.

**Authority:**

- Current local git state, current remotes, GitHub PR data, checks, review threads, and dirty worktrees.
- `/Users/ancplua/.codex/automations/branch-hygiene-sweep/branch-hygiene.sh` as quick gate, classifier, and deletion helper.
- `/Users/ancplua/.codex/automations/branch-hygiene-sweep/pr-review-pass.md` for useful open PRs.

**Non-authority:**

- Cached branch labels, prior chat summaries, stale AGENTS/CLAUDE conclusions, and old changelog entries.

**Configured repositories:**

- `/Users/ancplua/Arqio` -> `https://github.com/ANcpLua/Arqio.git`
- `/Users/ancplua/marketplaces/ancplua-claude-plugins` -> `https://github.com/ANcpLua/ancplua-claude-plugins.git`
- `/Users/ancplua/qyl` -> `https://github.com/Alexander-Nachtmann/qyl.git`
- `/Users/ancplua/framework/ANcpLua.Roslyn.Utilities` -> `https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities.git`
- `/Users/ancplua/framework/ANcpLua.NET.Sdk` -> `https://github.com/ANcpLua/ANcpLua.NET.Sdk.git`
- `/Users/ancplua/framework/ANcpLua.Analyzers` -> `https://github.com/ANcpLua/ANcpLua.Analyzers.git`
- `/Users/ancplua/framework/ANcpLua.Agents` -> `https://github.com/ANcpLua/ANcpLua.Agents.git`
- `/Users/ancplua/fh/ErrorOrX` -> `https://github.com/ANcpLua/ErrorOrX.git`
- `/Users/ancplua/framework/typespec-otel-semconv` -> `https://github.com/ANcpLua/typespec-otel-semconv.git`
- `/Users/ancplua/framework/renovate-config` -> `https://github.com/ANcpLua/renovate-config.git`

**Fast-exit gate:**

1. Read `/Users/ancplua/.codex/automations/branch-hygiene-sweep/memory.md`.
2. Read this file.
3. Run `/Users/ancplua/.codex/automations/branch-hygiene-sweep/branch-hygiene.sh --quick-gate`.
4. If the output contains `NO_WORK=1`, stop immediately and output only:

```text
branch-three
Changed: none
Evidence: quick gate proved all configured repos clean/default-only/no-open-PR
Pushed/Merged/Closed/Deleted: none
Blocked: none
```

5. Do not run the classifier, historical PR queries, review-thread inspection, check watches, or stricter evidence pass in
   the `NO_WORK=1` path.
6. Treat `NO_WORK=0` plus exit code 10 as "work exists", not helper failure.

**Full hygiene order after `NO_WORK=0`:**

1. Re-read live state for each configured repository:
   - `git rev-parse --show-toplevel`
   - `git remote -v`
   - `git status --short --branch`
   - `git fetch --all --prune --tags`
   - `git worktree list --porcelain`
   - `git remote show origin`
   - `git branch -vv --all`
   - `git for-each-ref --format='%(refname:short)%09%(objectname:short)%09%(committerdate:iso8601)%09%(authorname)%09%(upstream:short)' refs/heads refs/remotes`
   - `gh auth status`
   - `gh repo view --json nameWithOwner,defaultBranchRef,url`
   - `gh pr list --state all --limit 200 --json number,title,state,headRefName,baseRefName,author,updatedAt,mergedAt,closedAt,mergeStateStatus,reviewDecision,isDraft,url,headRefOid,baseRefOid,statusCheckRollup`
2. For each relevant PR, read:
   - `gh pr view <number> --json files,commits,comments,reviews,reviewDecision,mergeStateStatus,statusCheckRollup,url,headRefName,baseRefName`
   - `gh api graphql` review-thread data when flat PR output omits unresolved thread context.
3. Before changing any branch or PR, know local ref, remote ref, upstream, worktree owner, last commit hash/date/author,
   ahead/behind, merge-base against `main`, PR URL/state, unresolved comments, checks, review decision, draft state,
   mergeability, and dirty files.
4. Use `branch-hygiene.sh --apply` only for branches proved merged or already landed.
5. Use `branch-hygiene.sh --apply --aggressive` only after proving the branch has no PR, no protected worktree owner, no
   useful unlanded work, and no dirty local dependency.
6. For open useful PRs, follow `pr-review-pass.md`, fix actionable issues locally, run relevant verifiers, push, then take
   exactly one `gh pr checks` snapshot.
7. If checks are queued, running, pending, or not scheduled, stop with `Blocked: pushed-checks-running` plus PR URL, head
   SHA, and check states.
8. After every action, re-read state and prove the intended result.

**Action policy:**

- Merged or landed: delete local and remote branch after proof and worktree check.
- Closed, superseded, obsolete, duplicate, or invalid: close PR if needed; delete local and remote branches after proof.
- Open and useful: fix, verify, push; merge only when live review/check evidence says ready and match recent repo merge style.
- Stale or orphan: prove delete/close, or revive with a concrete branch/PR update.
- Dirty local state: inspect it, delete generated/stale residue, finish real branch work, stash only as last resort.
- Never use `git reset`.
- Never enter unbounded `gh pr checks --watch`.

**Result block:**

```text
<repo>
Changed: <successful commands/API effects>
Evidence: <hashes, PR URLs, check/review sources>
Pushed/Merged/Closed/Deleted: <refs and URLs>
Blocked: <real blocker only>
```

## Latest Changes

### 2026-05-10 22:32 CEST - Push hygiene review fixes

Workflow: branch-three
Changed: pushed existing qyl PR #313 and renovate-config PR #22 review fixes, fast-forwarded ANcpLua.NET.Sdk main to `f35ca55`, fast-forwarded ErrorOrX main to `64b7c97`, deleted qyl remote-tracking ref `origin/copilot/na` for remote branch `copilot/na`, and removed landed qyl local branch `chore/remove-qyl-analyzer-package`.
Evidence: quick gate returned `NO_WORK=0`; qyl PR #313 head `13c44b4f32e4aee4e59c4e33ddd713c4447d5808`; renovate-config PR #22 head `813d36f7075e868180198e1f5da90011dec9fa60`; `origin/copilot/na` commit `38669915142747cb6c2ae16d5e0696d22b53e067` was ancestor of `origin/main`; `chore/remove-qyl-analyzer-package` commit `11a66c4f35cbb4fafc41da187f1fc1252ca87762` was ancestor of `origin/main`; qyl remains dirty on `dev/forgejo-summary-research`; ancplua-claude-plugins PR #241 remains `CHANGES_REQUESTED`; qyl PR #307 still has failing `Backend (.NET)`.
Pushed/Merged/Closed/Deleted: pushed qyl `automation/branch-hygiene-run-20260510-1932`; pushed renovate-config `automation/branch-hygiene-changelog-20260510-1734`; deleted qyl remote `copilot/na`; removed `/Users/ancplua/RiderProjects/qyl-rename-relocate` worktree and deleted local branch `chore/remove-qyl-analyzer-package`.
Blocked: `pushed-checks-running` on qyl PR #313 `https://github.com/Alexander-Nachtmann/qyl/pull/313` head `13c44b4f32e4aee4e59c4e33ddd713c4447d5808` and renovate-config PR #22 `https://github.com/ANcpLua/renovate-config/pull/22` head `813d36f7075e868180198e1f5da90011dec9fa60` with checks pending after the one allowed snapshot.

### 2026-05-10 19:32 CEST - Push SDK review cleanup

Workflow: branch-three
Changed: quick gate returned `NO_WORK=0`; confirmed renovate-config PR #21 was already merged and pushed ANcpLua.NET.Sdk PR #138 review cleanup commit `4c945d356f8a5de2a00ff8643b923e7cc74a3b85`.
Evidence: SDK targeted verifier `dotnet test --project tests/ANcpLua.Sdk.Tests/ANcpLua.Sdk.Tests.csproj --no-restore -- --filter-method '*LocalEditorConfig*'` passed 6/6; one post-push `gh pr checks 138` snapshot showed new head checks queued/in progress.
Pushed/Merged/Closed/Deleted: pushed `automation/local-editorconfig-override-tests` to PR #138 `https://github.com/ANcpLua/ANcpLua.NET.Sdk/pull/138`; renovate-config PR #21 `https://github.com/ANcpLua/renovate-config/pull/21` was merged at 2026-05-10T17:30:10Z.
Blocked: `pushed-checks-running` on ANcpLua.NET.Sdk PR #138 `https://github.com/ANcpLua/ANcpLua.NET.Sdk/pull/138` head `4c945d356f8a5de2a00ff8643b923e7cc74a3b85`; one post-push snapshot had `test (ubuntu-latest)`, `test (windows-latest)`, and `test (macos-latest)` still running under the `publish` workflow. Later live state showed PR #138 merged at 2026-05-10T17:45:46Z with those checks successful.

### 2026-05-10 06:22 CEST - Merge qyl path hygiene PR

Workflow: branch-three
Changed: merged qyl PR #302 and opened PR #303 with a follow-up analyzer test assertion for the close-typo suggestion message.
Evidence: PR #302 head `845729145e70c690c2868b80598cd35293381e5d`, merge commit `19b9be15d4ef1ea9dc17cf2efe73a25fe0309713`, all listed PR #302 checks successful before merge; PR #303 focused test passed locally.
Pushed/Merged/Closed/Deleted: PR #302 merged at 2026-05-10T04:20:43Z; PR #303 `https://github.com/Alexander-Nachtmann/qyl/pull/303` includes test commit `a502275edeea72f93494af3ece54c0141eafc0c5` plus this ledger update.
Blocked: PR #303 checks pending after the one allowed snapshot.

### 2026-05-09 18:41 CEST - Initialize automation workflow ledger

Workflow: documentation
Changed: created cron-runner workflow ledger for SemConv generator work and branch-three hygiene runs.
Evidence: indexed by `qyl.slnx`; referenced by `AGENTS.md`.
Pushed/Merged/Closed/Deleted: qyl main commit containing this entry.
Blocked: none.
