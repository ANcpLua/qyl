# CI Automation

How the qyl repo handles pull requests end-to-end without human intervention,
and how to opt new repos in.

## Workflow Inventory

| Workflow                                         | Trigger                                                    | What it does                                                                                                |
|--------------------------------------------------|------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------|
| `.github/workflows/anti-slop.yml`                | `pull_request_target: opened,reopened,ready_for_review,edited` | `peakoss/anti-slop@v0.3.0` scans every PR for slop signals. Closes the PR when at least `max-failures: 5` of its 34 checks trip. |
| `.github/workflows/destructive-auto-merge.yml`   | `pull_request_target`, `pull_request_review`, `check_suite: completed`, `workflow_dispatch` | Auto-approves and `gh pr merge --admin --squash --delete-branch` for any PR authored by a trusted identity. Bypasses required reviews and required checks. |
| `.github/workflows/refix.yml`                    | `workflow_dispatch`, `pull_request_target: labeled`, `issue_comment` (OWNER/MEMBER/COLLABORATOR only) | `HappyOnigiri/Refix@v1.4.0` runs Claude on CI to fix CodeRabbit feedback, CI failures, and merge conflicts. Manually invoked. |

## Per-PR Flow

1. **PR opens** (`pull_request_target: opened`).
2. anti-slop runs in <15 s. If it passes, the PR stays open. If it trips
   `max-failures` checks, anti-slop closes it (`close-pr: true`) and the
   destructive tier never touches it.
3. The destructive tier fires on the same `pull_request_target` event. If
   the author is in the trusted-identity allow-list and the PR is not a
   draft, it auto-approves and runs `gh pr merge --admin --squash
   --delete-branch`. With `--admin` the merge is unconditional — required
   reviews, required checks, and branch-protection rules are bypassed.
   The fallback chain
   `gh pr merge --admin || gh pr view` handles racing tier merges by
   short-circuiting cleanly when state is already `MERGED` or `CLOSED`.
4. The PR is merged and its branch deleted, or it is closed by anti-slop.
   No further events fire from this repository's side.

Human action between step 1 and step 4: none.

## Trusted-Author Allow-List

Defined inline in `destructive-auto-merge.yml`. The maintainer + every known
automation identity that opens PRs against this repo:

| Author identity          | Why                                                |
|--------------------------|----------------------------------------------------|
| `ANcpLua`                | maintainer                                         |
| `renovate[bot]`          | dependency updates                                 |
| `dependabot[bot]`        | dependency updates                                 |
| `copilot[bot]`           | autonomous Copilot fix PRs                         |
| `jules[bot]`             | autonomous Jules fix PRs                           |
| `claude-code[bot]`       | autonomous Claude Code fix PRs                     |
| `github-actions[bot]`    | governance + automation runner                     |
| `coderabbitai[bot]`      | autofix PRs raised by CodeRabbit                   |

Authors **outside** the list are not auto-merged. anti-slop still scans them
and will close the PR if it trips `max-failures` checks; otherwise the PR
sits awaiting human review.

## Dependency-Bot Gating

The destructive tier preserves one safety: PRs from `renovate[bot]` or
`dependabot[bot]` whose title, labels, or body match
`major|alpha|beta|preview|rc` are skipped — these are reckless to
admin-merge regardless of trust. All other dependency updates are merged
immediately. Maintainer, AI-agent, and governance PRs are not version-bump
shaped and skip the gate entirely.

## anti-slop Configuration

Tuned in `anti-slop.yml`:

| Knob                          | Value     | Rationale                                                                |
|-------------------------------|-----------|--------------------------------------------------------------------------|
| `max-failures`                | 5         | Trip threshold across the 34 default checks                              |
| `blocked-source-branches`     | `main` `master` | Reject PRs whose source branch is `main` or `master`               |
| `max-changed-files`           | 500       | qyl's `nuke Generate` regen PRs cross the upstream default of 50         |
| `max-changed-lines`           | 25000     | Same regen reason                                                        |
| `require-conventional-title`  | true      | Mirrors `.coderabbit.yaml`'s pre-merge title check                       |
| `require-description`         | true      | Block empty-body drive-by PRs                                            |
| `max-description-length`      | 4000      | qyl PR bodies cite many files                                            |
| `max-emoji-count`             | 4         | Allows bot footers (`✅`/`❌`/`🤖`/`🪄`); 5+ is a slop signal              |
| `max-code-references`         | 40        | Hard upper bound enforced by the action — 41+ is rejected at preflight   |

Maintainer exemption (`exempt-author-association: OWNER,MEMBER,COLLABORATOR`)
is left at the default, so owner-authored PRs pass straight through.

## Refix Triggers

`refix.yml` runs Claude on CI to fix things the regular flow can't auto-resolve.
It is **never event-driven on every PR** — only on explicit signal:

| Trigger source                                   | Effect                                                       |
|--------------------------------------------------|--------------------------------------------------------------|
| `workflow_dispatch` with `pr-number` input       | Manually invoke against any open PR                          |
| `pull_request_target: labeled` with `refix:requested` label | Maintainer applies the label; Refix processes the PR  |
| `issue_comment` from OWNER / MEMBER / COLLABORATOR | A privileged commenter can ask Refix to step in (e.g. `@codesmith refix`) |

Refix mints a short-lived AUTOMERGE_APP installation token (~1 h TTL) instead
of consuming a long-lived classic PAT. Drive-by comments cannot dispatch
Refix — `author_association` is verified at the workflow `if:` level.

## Enterprise / Org Prerequisites

These are one-time admin actions. Once set, no maintenance required.

1. **Actions allow-list (enterprise level).**
   `enterprises/<slug>/actions/permissions/selected-actions` must include
   `patterns_allowed` covering every external action this repo uses:

   ```text
   ANcpLua/renovate-config/*
   peakoss/anti-slop@*
   HappyOnigiri/Refix@*
   ```

   Without these patterns, the corresponding workflow runs die at
   `startup_failure` with no logs.

2. **AUTOMERGE_APP installation.**
   The destructive tier and Refix both mint installation tokens via the
   AUTOMERGE_APP GitHub App. Install it on the org or repo and configure
   the secrets:

   - `AUTOMERGE_APP_ID`
   - `AUTOMERGE_APP_PRIVATE_KEY`

   The App needs `contents: write`, `pull-requests: write`, and the admin
   permission required for `gh pr merge --admin` to actually bypass
   protection rules.

3. **CLAUDE_CODE_OAUTH_TOKEN secret.**
   Required by Refix. Generated with the `claude setup-token` CLI command.

## Failure Modes

What can go wrong and how the system recovers.

| Symptom                                   | Cause                                                              | How the agent self-resolves                                                                        |
|-------------------------------------------|--------------------------------------------------------------------|----------------------------------------------------------------------------------------------------|
| Auto-merge workflow `startup_failure`     | Enterprise allow-list does not include the pinned external action  | Admin patches the allow-list; subsequent events fire cleanly                                       |
| anti-slop preflight rejects config        | Workflow has an out-of-range value (e.g. `max-code-references > 40`) | Author fixes the workflow file; close+reopen re-triggers the scan                                  |
| Sticky `CHANGES_REQUESTED` from a bot     | Reviewer issued a formal change-request before the latest push     | Destructive tier auto-approves before merging, overriding the sticky verdict                       |
| Rate limit / 5xx during `gh pr merge`     | Transient GitHub API issue                                         | Exponential-backoff retry: 5 s → 10 s → 20 s → 40 s → 80 s (≈155 s total tolerance)                |
| App installation token expired mid-run    | Token TTL is ~1 h, run is short-lived                              | Tokens are minted fresh per job step; expiration cannot span a job                                 |
| Two tiers race on the same PR             | `pull_request_target` and `pull_request_review` overlap            | Whichever wins the merge succeeds; the loser sees `state == MERGED` and exits cleanly              |

Genuine workflow bugs (config typo, logic error) are the only human-step
case. Anti-slop crashing on its own input validation is the canonical
example — fix the value, push, the next `pull_request_target` event re-runs
the scan unaided.

## Adding the Same Stack to a New Repo

Minimum viable port:

1. Copy `.github/workflows/{anti-slop,destructive-auto-merge,refix}.yml`
   from this repo. Adjust the trusted-author allow-list inside
   `destructive-auto-merge.yml` if the new repo has a different
   maintainer identity.
2. Add `AUTOMERGE_APP_ID` and `AUTOMERGE_APP_PRIVATE_KEY` as repo or org
   secrets, and `CLAUDE_CODE_OAUTH_TOKEN` if Refix is wanted.
3. Confirm the enterprise allow-list (above) covers the action patterns —
   one-time admin action.
4. Push a trivial PR from a maintainer identity to verify: anti-slop
   should pass, destructive tier should admin-squash within seconds.

A second test repo `ANcpLua/qyl-config-test-{public,private}` ports a
simpler variant of this stack (no AUTOMERGE_APP — `GITHUB_TOKEN`-only
squash) and is used to validate the configuration's event-driven behaviour
in isolation. See those repos' READMEs for the test-specific setup.
