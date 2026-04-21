# Releasing qyl

qyl ships three coordinated artifact families from a single tag. Everything in
`.github/workflows/release.yml` fires on `git push --tags`; nothing publishes on
`main` pushes.

## Artifact families

| Family | Package(s) | Registry | Published by |
|--------|------------|----------|--------------|
| NuGet | `Qyl.Contracts`, `Qyl.OpenTelemetry.Extensions` (*`Qyl.Client` pending Commit 7*) | nuget.org | `pack-nuget` job |
| npm | `@qyl/client` | npmjs.org (`@qyl` scope, public) | `pack-npm` job |
| Binary | `qyl-collector-linux-x64-vX.Y.Z.tar.gz`, `qyl-dashboard-vX.Y.Z.tar.gz` + GitHub Release | GitHub Releases | `build-and-release` job |
| Docker | `ancplua/qyl:vX.Y.Z`, `ghcr.io/ancplua/qyl:vX.Y.Z`, `:latest` | Docker Hub + GHCR | `build-docker` job |

## Tag format

`vMAJOR.MINOR.PATCH[-prerelease]` — SemVer, leading `v`. Examples:

- `v1.0.0` — stable release
- `v1.1.0-rc.1` — release candidate (GitHub Release marked `prerelease`)
- `v0.3.0-alpha.4` — alpha; still publishes to all registries

The workflow derives the numeric version from the tag (strip leading `v`) and
passes it through `-p:Version=` for dotnet pack, `npm version` for npm.

## SemVer contract

Breaking contract = MAJOR bump. "Breaking" is defined against the TypeSpec
source in `core/specs/` and the hand-written surfaces:

| Change | Bump |
|--------|------|
| Remove/rename a model, enum, or operation in `core/specs/**/*.tsp` | **MAJOR** |
| Change a property's type or cardinality in `core/specs/**/*.tsp` | **MAJOR** |
| Remove a public member on `Qyl.OpenTelemetry.Extensions` | **MAJOR** |
| Add a new optional property / operation / model | **MINOR** |
| Add a new package under `packages/` | **MINOR** |
| Emitter-only delta (formatting, ordering, no ABI change) | **PATCH** |
| Fix without surface change (bugfix, doc, internal rewrite) | **PATCH** |

The `@qyl/typespec-qyl-semconv-lint` library (Commit 12) enforces the telemetry
surface; any rule violation is a MAJOR bump or a revert, never a silent
downgrade.

## Required GitHub secrets

Provision once per repo under **Settings → Secrets → Actions**:

| Secret | Purpose |
|--------|---------|
| `NUGET_API_KEY` | nuget.org API key for the publisher principal |
| `NPM_TOKEN` | npmjs.org automation token with publish rights on `@qyl/*` |
| `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` | Docker Hub publishing |

If `NUGET_API_KEY` or `NPM_TOKEN` is missing, the corresponding job fails and
the other jobs still complete — no retag needed once the secret lands.

## Release checklist

1. **Update Version.props** for any runtime dependency bumps. Commit.
2. **Regenerate emitter output**: `nuke Generate`. Confirm no drift in
   `packages/Qyl.Contracts/Generated/`, `services/qyl.collector/Storage/`.
3. **Verify locally**:
   ```sh
   dotnet build qyl.slnx --tl:off
   dotnet pack packages/Qyl.Contracts -c Release -o /tmp/nupkg
   dotnet pack packages/Qyl.OpenTelemetry.Extensions -c Release -o /tmp/nupkg
   ( cd packages/qyl-client && npm install && npm run build )
   ```
4. **Tag** the commit on `main`:
   ```sh
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```
5. **Watch** the `Release` workflow run — all four jobs must end green.
6. **Verify published artifacts**:
   - `https://www.nuget.org/packages/Qyl.Contracts/X.Y.Z`
   - `https://www.nuget.org/packages/Qyl.OpenTelemetry.Extensions/X.Y.Z`
   - `https://www.npmjs.com/package/@qyl/client/v/X.Y.Z`
   - `docker pull ancplua/qyl:vX.Y.Z`

## Rollback

Packages on NuGet and npm **cannot be deleted** after the first download; they
can only be **deprecated / unlisted**:

```sh
# Deprecate a broken NuGet release (users see warning on restore)
dotnet nuget delete Qyl.OpenTelemetry.Extensions X.Y.Z --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY

# Deprecate an npm release with a migration hint
npm deprecate @qyl/client@X.Y.Z "Do not use — upgrade to X.Y.(Z+1); see https://github.com/ancplua/qyl/releases"
```

Immediately publish a forward-fix patch (`vX.Y.(Z+1)`) rather than trying to
resurrect the old tag. Do **not** force-push over a release tag; consumers that
already pulled the artifact will have corrupted state.

GitHub Releases and Docker images can be deleted outright (`gh release delete
vX.Y.Z` / `docker image rm`) but anyone who already pulled them won't notice.

## Pre-1.0 policy

While qyl is pre-1.0, MINOR bumps may carry breaking changes. The release-notes
body (generated from commit messages via `softprops/action-gh-release`) must
call out any breaking change explicitly.

Post-1.0 the SemVer contract above applies without exception.
