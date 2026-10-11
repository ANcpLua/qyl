# qyl: release, pins and agent hand-offs

Who reads this: the one agent working on this repository's `main`. Agent, user and owner are
the same word. There is one agent per repository, six in all (qyl, qyl.mcp, qyl-api-schema,
Qyl.Telemetry.AutoInstrumentation, Qyl.Telemetry.SemanticConventions, qyl.at), each on its own
`main`. Commit and push to `main` directly: no pull requests, no CI, no branch rules. Builds,
tests and packs run through Fallout (`./eng/build.sh <Target>`) and `mise run check`.

## This repository

- Publishes to NuGet in one release set: the `qyl` tool, its runtime packages
  `qyl.linux-x64`, `qyl.linux-arm64`, `qyl.osx-x64`, `qyl.osx-arm64`, `qyl.win-x64`,
  `qyl.win-arm64`, and `Qyl.Api.Sdk`. Pushing `main` also deploys the Collector
  (`qyl-collector`, api.qyl.at, Railway).
- Version source: `Version.props` `QylVersion`. Bump it in its own commit before the first
  package-affecting change after a release; the tag names the commit that carries it.
- Release command: `./release.sh <x.y.z> --dry-run`, then `./release.sh <x.y.z>` (packs with
  Fallout `PackRelease`, smokes the tool packages, pushes the eight packages, tags, creates the
  GitHub release, waits for indexing).
- Key: `vitals keys` entry "qyl-dev manual publish and release (NuGet)", file
  `~/.config/vitals/nuget-qyl-manual-publish-key`. The script reads it; never print, paste,
  commit or log a key. If the entry is missing or its note's expiry date has passed, stop and
  tell the owner.
- Upstream pins this repo must keep fresh: `Version.props` `QylApiContractsVersion`
  (Qyl.Api.Contracts, from qyl-api-schema), `QylSemanticConventionsVersion`
  (Qyl.Telemetry.SemanticConventions), `QylTelemetryVersion` (Qyl.Telemetry.AutoInstrumentation);
  `services/qyl.dashboard/package.json` and its `bun.lock` (`@ancplua/qyl-api-schema`); the README
  package table.
- Consumers to notify after this repo's release is indexed: qyl.at (`src/data/site.ts` lists
  `qyl` and `Qyl.Api.Sdk`).

## Before any work: the stale-version guard

1. `git pull --ff-only origin main`.
2. `mise install`, then dependencies from the lockfile: bun `bun install --frozen-lockfile`;
   npm `npm ci`; pnpm `pnpm install --frozen-lockfile`; .NET `dotnet restore`. Prefer bun
   wherever a package has a `bun.lock`.
3. Compare every upstream pin above with the latest indexed version:
   npm `npm view <package> version`; NuGet
   `curl -fsSL https://api.nuget.org/v3-flatcontainer/<id, lowercase>/index.json` (last entry).
   If a newer version is indexed, re-pin first (section "When a release lands"), then start
   the actual work. Never build on a pin older than what is indexed.

## Releasing

1. Land the version-bump commit on `main` (`Version.props` `QylVersion`).
2. `./release.sh <x.y.z> --dry-run`; fix whatever it refuses.
3. `./release.sh <x.y.z>`.
4. Watch indexing: the script polls the flat-container index for `qyl` and `Qyl.Api.Sdk`. If it
   ends with "not indexed yet", keep polling those URLs every 30 s. Until both are indexed, do
   nothing else about this release.
5. The moment both are indexed: stop watching and notify each consumer (section "Notify").
6. Record the published version in
   `/Users/alexandernachtmann/Downloads/qyl-codex-claude/qyl-handoff-for-codex.md` and rewrite
   `/Users/alexandernachtmann/Downloads/qyl-codex-claude/qyl-published-versions.yaml` (the
   registries are the truth; this file is the snapshot every agent reads first).

## Notify

One message per consumer repo, to its running agent through the session's agent-messaging
tool when there is one (Claude Code: `ListAgents`, then `SendMessage`). If the consumer has
no running agent, append the same text to the handoff file above under "Pending re-pins";
the owner relays it. Text:

```
RELEASE <package ids> <version> indexed <UTC time>
repo: <consumer repo>
re-pin: <exact files>
then: regenerate the lockfile, run the gate, commit, push main
```

## When a release lands and you are the consumer

Act at once, before continuing your own work, so both agents keep moving:

1. Edit the pin files named in the message (the list above for this repo).
2. Regenerate the lockfile with that package's manager: `bun.lock` → `bun install` (not
   frozen: it rewrites the lock to the new pin); `package-lock.json` → `npm install`;
   `pnpm-lock.yaml` → `pnpm install`; .NET with `packages.lock.json` →
   `dotnet restore --force-evaluate`, otherwise `dotnet restore`.
3. Run the gate: `mise run check`.
4. Commit `chore: pin <package> <version>` and push `main` immediately. The push deploys the
   Collector.
5. If the pin changes what this repo ships, bump `QylVersion` and continue with "Releasing":
   one release per repo per wave.

## Order rules that never change

- A qyl-api-schema release that changes the contract revision: re-pin and push qyl first, wait
  until `https://api.qyl.at/health` serves the new revision, then qyl.mcp re-pins; mcp.qyl.at
  refuses a Collector on another revision.
- Deployment state, from this Mac: `railway deployment list --json` in this clone (linked to
  project qyl, service `qyl-collector`, environment `production`); the newest entry must show
  `status: SUCCESS` and `meta.commitHash` equal to the pushed commit. `curl -fsSL
  https://api.qyl.at/health` shows the served contract revision.
- SemanticConventions and AutoInstrumentation both feed this repo: wait until both are indexed
  and re-pin them in one commit.
- qyl.at lists a version only after the index confirms it.
