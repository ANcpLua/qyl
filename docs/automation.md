# CI Automation

How the qyl repo handles pull requests end-to-end without human
intervention, and how to opt new repos in.

## Workflow Inventory

| Workflow                          | Trigger                                                                                                  | What it does                                                                                                                                  |
|-----------------------------------|----------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `.github/workflows/anti-slop.yml` | `pull_request_target: opened,reopened,ready_for_review,edited`                                            | `peakoss/anti-slop@v0.3.0` scans every PR for slop signals. Closes the PR when `max-failures` checks trip.                                    |
| `.github/workflows/refix.yml`     | `workflow_dispatch`, `pull_request_target: labeled`, `issue_comment` (OWNER / MEMBER / COLLABORATOR only) | `HappyOnigiri/Refix@v1.4.0` runs Claude on CI to fix CodeRabbit feedback, CI failures, and merge conflicts. Label-gated and manually invoked. |
| `.github/workflows/auto-merge.yml`| `pull_request_target: opened,synchronize,reopened,ready_for_review`, `pull_request_review: submitted`    | Native GitHub auto-merge via `GITHUB_TOKEN` (the 2026 pattern — no App, no extra secrets). Enables native auto-merge on PR open for AI-agent branches (`claude/` / `copilot/` / `jules/`), owner PRs, and CodeRabbit-approved PRs. Renovate PRs flip auto-merge themselves via `platformAutomerge: true` in the shared preset. |

There is **no** destructive admin auto-merge tier. PR merging is handled
by GitHub's native auto-merge: Renovate PRs flip it on at open via
`platformAutomerge: true` in the shared preset; AI-agent / owner /
CodeRabbit-approved PRs flip it on via `auto-merge.yml`; everything else
sits open until someone clicks "Enable auto-merge".

## Per-PR Flow

1. **PR opens** (`pull_request_target: opened`).
2. anti-slop runs in <15 s. If it passes, the PR stays open. If it trips
   `max-failures` checks, anti-slop closes it (`close-pr: true`).
3. CodeRabbit reviews and posts comments. The config in `.coderabbit.yaml`
   sets `request_changes_workflow: false`, so CodeRabbit never submits a
   formal `CHANGES_REQUESTED` review — it advises, it does not block.
4. Branch-protection required checks run as normal.
5. Native auto-merge is enabled by `platformAutomerge: true` (Renovate
   PRs), by `auto-merge.yml` (AI-agent / owner / CodeRabbit-approved
   PRs), or by a manual click (other human PRs). The PR merges the
   moment branch protection is satisfied.

Human action between step 1 and step 5: zero for Renovate PRs, AI-agent
PRs (`claude/` / `copilot/` / `jules/`), owner PRs, and CodeRabbit-approved
PRs; one click for human PRs not in those tiers.

## Renovate dependency PRs

Renovate's shared baseline lives in
[`ANcpLua/renovate-config`](https://github.com/ANcpLua/renovate-config) — qyl
extends it. `platformAutomerge: true` is set at the root of `default.json`,
so every Renovate PR enables GitHub native auto-merge when opened. Patch
bumps, npm devDependency minors, digest pins, lockfile-maintenance updates,
ANcpLua first-party packages, and Microsoft.Extensions groups all carry
`automerge: true` as well — they merge as soon as branch protection's
required checks pass.

The block-list inside `default.json` filters out unstable channels
(`alpha`/`beta`/`rc`/`preview`/`dev`/`canary`/`next`/`nightly`) by default.
Renovate doesn't open PRs for those unless an explicit per-package
exception resets `allowedVersions: '*'`.

## anti-slop Configuration

Tuned in `anti-slop.yml`:

| Knob                          | Value             | Rationale                                                          |
|-------------------------------|-------------------|--------------------------------------------------------------------|
| `max-failures`                | 5                 | Trip threshold across the 34 default checks                        |
| `blocked-source-branches`     | `main` `master`   | Reject PRs whose source branch is `main` or `master`               |
| `max-changed-files`           | 500               | `nuke Generate` regen PRs cross the upstream default of 50         |
| `max-changed-lines`           | 25000             | Same regen reason                                                  |
| `require-conventional-title`  | true              | Mirrors `.coderabbit.yaml`'s pre-merge title check                 |
| `require-description`         | true              | Block empty-body drive-by PRs                                      |
| `max-description-length`      | 4000              | qyl PR bodies cite many files                                      |
| `max-emoji-count`             | 4                 | Allows bot footers (`✅`/`❌`/`🤖`/`🪄`); 5+ is a slop signal       |
| `max-code-references`         | 40                | Hard upper bound enforced by the action — 41+ rejected at preflight|

Maintainer exemption (`exempt-author-association: OWNER,MEMBER,COLLABORATOR`)
is left at the default, so owner-authored PRs pass straight through.

## Refix Triggers

`refix.yml` runs Claude on CI to fix things the regular flow can't
auto-resolve. It is **never event-driven on every PR** — only on explicit
signal:

| Trigger source                                            | Effect                                                                  |
|-----------------------------------------------------------|-------------------------------------------------------------------------|
| `workflow_dispatch` with `pr-number` input                | Manually invoke against any open PR                                     |
| `pull_request_target: labeled` with `refix:requested`     | Maintainer applies the label; Refix processes the PR                    |
| `issue_comment` from OWNER / MEMBER / COLLABORATOR        | A privileged commenter can ask Refix to step in (e.g. `@codesmith refix`) |

Refix consumes a classic PAT (`REFIX_CLASSIC_PAT`) plus
`CLAUDE_CODE_OAUTH_TOKEN`. Drive-by comments cannot dispatch Refix —
`author_association` is verified at the workflow `if:` level.

## Enterprise / Org Prerequisites

These are one-time admin actions. Once set, no maintenance required.

1. **Actions policy (enterprise level).**
   `E-ANcppLua` runs `allowed_actions: "all"` (set 2026-05-17 via
   `PUT enterprises/E-ANcppLua/actions/permissions`). Any action, any
   owner, no allow-list to curate — adding new third-party actions just
   works. Trade-off explicitly accepted as part of the maximum-permissive
   posture this repo's autonomy goal requires. Previously curated
   patterns (`ANcpLua/renovate-config/*`, `peakoss/anti-slop@*`,
   `HappyOnigiri/Refix@*`) are stored but dormant under "all".

2. **Repo settings.** `delete_branch_on_merge: true` and
   `allow_auto_merge: true` are patched fleet-wide by
   [`ANcpLua/github-settings-automation`](https://github.com/ANcpLua/github-settings-automation)
   (`enforce-repo-settings.yml`, weekly cron + dispatch).

3. **`REFIX_CLASSIC_PAT` + `CLAUDE_CODE_OAUTH_TOKEN` secrets.** Required
   only on repos that adopt `refix.yml`. The PAT is a classic token with
   `repo, workflow, read:org, read:discussion` scopes; the OAuth token
   comes from `claude setup-token`.

## Failure Modes

What can go wrong and how the system recovers.

| Symptom                                   | Cause                                                              | Recovery                                                                                          |
|-------------------------------------------|--------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| Workflow `startup_failure`                | Pinned action SHA missing upstream (force-pushed / repo deleted)   | Verify the SHA exists; re-pin to a live commit. (Allow-list is no longer a failure mode — policy is `allowed_actions: "all"`.) |
| anti-slop preflight rejects config        | Workflow has an out-of-range value (e.g. `max-code-references > 40`) | Author fixes the workflow file; close+reopen re-triggers the scan                                  |
| Native auto-merge waiting indefinitely    | Required status check stuck or failing                             | Inspect the failing check; fix the underlying issue. There is no `--admin` bypass on purpose.     |

## Adding the Same Stack to a New Repo

Most of this is now automated by `ANcpLua/github-settings-automation`:

1. The weekly `enforce-repo-settings.yml` cron PATCHes
   `delete_branch_on_merge: true` + `allow_auto_merge: true` and seeds
   `templates/coderabbit.yaml` → `.coderabbit.yaml` for any active repo
   that doesn't already carry one.
2. Add `extends: ["github>ANcpLua/renovate-config"]` to the repo's
   `renovate.json` (or let the org-level Renovate config inherit it) so
   `platformAutomerge: true` is in effect.
3. Copy `templates/anti-slop.yml` and `templates/refix.yml` from
   `github-settings-automation` into `.github/workflows/` if the repo
   wants those tiers. Set `REFIX_CLASSIC_PAT` and
   `CLAUDE_CODE_OAUTH_TOKEN` only if `refix.yml` is adopted.
4. Confirm the enterprise allow-list covers the action patterns — see
   the prerequisites section above.

A second test repo `ANcpLua/qyl-config-test-{public,private}` ports a
simpler variant of this stack and is used to validate the configuration's
event-driven behaviour in isolation. See those repos' READMEs for
test-specific setup.
