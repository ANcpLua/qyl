#!/usr/bin/env bash
# Publish and release qyl's NuGet packages (the qyl tool, its six runtime packages and
# Qyl.Api.Sdk) from this machine with the key registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            pack, push the eight packages, tag v<x.y.z>, GitHub release
#   ./release.sh <x.y.z> --dry-run  every check, the real pack and smoke, a NuGet key probe;
#                                   registry and git stay unchanged
#
# Version source: Version.props QylVersion, bumped in its own commit before the first
# package-affecting change after a release. The tag names HEAD, whose Version.props must
# carry <x.y.z>; the script never edits the version.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
OUT=Artifacts/nuget

version=${1:-}
dry=0; [[ ${2:-} == --dry-run ]] && dry=1
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <x.y.z> [--dry-run]" >&2; exit 2; }
say() { printf '\n== %s\n' "$*"; }
die() { echo "release: $*" >&2; exit 1; }
indexed() { curl -fsSL "$NUGET_FLAT/$1/index.json" 2>/dev/null | grep -q "\"$2\""; }

say "checks"
[[ -r $NUGET_KEY_FILE ]] || die "NuGet key missing: $NUGET_KEY_FILE (vitals keys)"
[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "working tree not clean"
git fetch -q origin main
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main differs from origin/main"
[[ -z $(git ls-remote --tags origin "refs/tags/v$version") ]] || die "tag v$version already on origin"
git rev-parse -q --verify "refs/tags/v$version" >/dev/null && die "tag v$version already exists locally"
qv=$(sed -n 's:.*<QylVersion>\([^<]*\)</QylVersion>.*:\1:p' Version.props)
[[ $qv == "$version" ]] || die "Version.props QylVersion is $qv, not $version; bump it in its own commit first"
for id in qyl qyl.api.sdk; do
  if indexed "$id" "$version"; then (( dry )) || die "$id $version is already on NuGet"; echo "warning: $id $version is already on NuGet; the real run refuses"; fi
done
gh auth status >/dev/null 2>&1 || die "gh is not logged in"
rids=$(sed -n 's:.*<QylToolRuntimeIdentifiers>\([^<]*\)</QylToolRuntimeIdentifiers>.*:\1:p' packages/Qyl.Cli/Qyl.Cli.csproj | tr ';' ' ')
[[ -n $rids ]] || die "QylToolRuntimeIdentifiers not found in packages/Qyl.Cli/Qyl.Cli.csproj"
expected=$(printf '%s\n' "qyl.$version.nupkg" "Qyl.Api.Sdk.$version.nupkg" $(for r in $rids; do echo "qyl.$r.$version.nupkg"; done) | sort)
echo "expected package set: $(echo "$expected" | wc -l | tr -d ' ') packages"

say "pack (Fallout PackRelease)"
./eng/build.sh PackRelease --Configuration Release --PackageVersion "$version" --RepositoryCommit "$(git rev-parse HEAD)"
actual=$(find "$OUT" -maxdepth 1 -name '*.nupkg' -exec basename {} \; | sort)
[[ $actual == "$expected" ]] || { echo "expected:"; echo "$expected"; echo "packed:"; echo "$actual"; die "package set mismatch"; }

say "smoke the tool packages on this host"
dotnet run --project eng/tools/QylToolSmoke/QylToolSmoke.csproj --configuration Release -- "$OUT"

say "NuGet push"
probe_dir=$(mktemp -d); trap 'rm -rf "$probe_dir"' EXIT
if (( dry )); then
  latest=$(curl -fsSL "$NUGET_FLAT/qyl/index.json" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["versions"][-1])')
  curl -fsSL -o "$probe_dir/probe.nupkg" "$NUGET_FLAT/qyl/$latest/qyl.$latest.nupkg"
  echo "dry run: pushing the published qyl $latest again with --skip-duplicate to prove the key (expect 'already exists')"
  dotnet nuget push "$probe_dir/probe.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE" --skip-duplicate
  say "dry run finished: nothing published, nothing tagged"
  git status --short --untracked-files=no
  exit 0
fi
for p in "$OUT"/*.nupkg; do
  dotnet nuget push "$p" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE"
done

say "tag and GitHub release"
git tag -a "v$version" -m "v$version"
git push -q origin "v$version"
gh release create "v$version" --title "v$version" --generate-notes "$OUT"/*.nupkg

say "verify indexing (qyl and Qyl.Api.Sdk)"
for _ in $(seq 1 20); do
  indexed qyl "$version" && indexed qyl.api.sdk "$version" && { echo "indexed: qyl $version, Qyl.Api.Sdk $version"; break; }
  sleep 30
done
indexed qyl "$version" || echo "qyl $version pushed, not indexed yet"
indexed qyl.api.sdk "$version" || echo "Qyl.Api.Sdk $version pushed, not indexed yet"

say "next (SKILL.md): notify qyl.at once indexed (src/data/site.ts); pushing main already deployed api.qyl.at"
