#!/usr/bin/env bash
# Release everything qyl publishes, at Version.props QylVersion: the qyl tool and its runtime
# packages qyl.<rid>, Qyl.Api.Sdk, Qyl.Orchestration and Qyl.Api.Contracts on nuget.org,
# qyl-mcp-server on npm.
#
#   ./release.sh
#
# 1. Pack once: Fallout PackRelease writes every artifact, the packed commit and SHA512SUMS into
#    artifacts/release/<version>/. A directory with SHA512SUMS is sealed and is never repacked.
# 2. Verify the sealed set against SHA512SUMS.
# 3. Publish what nuget.org and npm do not have yet: runtime packages before the tool pointer.
# 4. Tag v<version> on the packed commit, push the tag and create its GitHub release from the
#    version's CHANGELOG.md section.
# 5. Fast-forward the production branch to the packed commit. Railway deploys qyl-collector and
#    qyl-mcp from that branch only, so a release is what deploys; a push to main deploys nothing.
#
# Rerun after any failure; every step skips what is already done. The keys come from the Vitals
# registry files below and are passed to the publishers through their environment, never printed.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
NPM_TOKEN_FILE=$HOME/.config/vitals/npm-qyl-manual-publish-token  # vitals keys: "qyl-api-schema and qyl.mcp manual publish and release (npm)"
NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NPM_REGISTRY=https://registry.npmjs.org

die() { echo "release: $*" >&2; exit 1; }
say() { printf '\n== %s\n' "$*"; }

version=$(sed -n 's:.*<QylVersion>\([^<]*\)</QylVersion>.*:\1:p' Version.props)
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Version.props QylVersion is '$version', not x.y.z"
dir=artifacts/release/$version
[[ -r $NUGET_KEY_FILE ]] || die "NuGet key missing: $NUGET_KEY_FILE (vitals keys)"
[[ -r $NPM_TOKEN_FILE ]] || die "npm token missing: $NPM_TOKEN_FILE (vitals keys)"

if [[ ! -f $dir/SHA512SUMS ]]; then
  say "pack $version"
  [[ -z $(git status --porcelain --untracked-files=no) ]] || die "tracked files are modified; a release packs a commit"
  ./eng/build.sh PackRelease --Configuration Release --PackageVersion "$version" --RepositoryCommit "$(git rev-parse HEAD)"
fi

say "verify the sealed set in $dir"
(cd "$dir" && shasum -a 512 -c SHA512SUMS)
commit=$(<"$dir/commit")

say "publish to nuget.org"
lower() { tr '[:upper:]' '[:lower:]' <<<"$1"; }
on_nuget() { curl -fsS --max-time 30 "$NUGET_FLAT/$(lower "$1")/index.json?release=$$" 2>/dev/null | grep -q "\"$(lower "$2")\""; }
packages=()
for file in "$dir"/qyl.*.nupkg; do
  id=${file##*/}; id=${id%".$version.nupkg"}
  [[ $id == qyl ]] || packages+=("$id")
done
packages+=(Qyl.Api.Contracts Qyl.Orchestration Qyl.Api.Sdk qyl)
for id in "${packages[@]}"; do
  if on_nuget "$id" "$version"; then
    echo "$id $version is on nuget.org"
    continue
  fi
  # --skip-duplicate: a package pushed by an earlier run but not indexed yet is accepted as published.
  # --timeout 1800: a 43 MB runtime package took 313 s on 2026-10-20, past the 300 s default.
  NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$dir/$id.$version.nupkg" --source "$NUGET_SOURCE" --skip-duplicate --timeout 1800
done

say "publish to npm"
if [[ $(curl -s -o /dev/null -w '%{http_code}' --max-time 30 "$NPM_REGISTRY/qyl-mcp-server/$version") == 200 ]]; then
  echo "qyl-mcp-server $version is on npm"
else
  (cd services/qyl.mcp && NPM_CONFIG_TOKEN="$(<"$NPM_TOKEN_FILE")" bun publish "$OLDPWD/$dir/qyl-mcp-server-$version.tgz" --registry "$NPM_REGISTRY" --access public)
fi

say "tag v$version on $commit"
if git rev-parse -q --verify "refs/tags/v$version" >/dev/null; then
  [[ $(git rev-parse "v$version^{commit}") == "$commit" ]] || die "tag v$version exists on another commit"
else
  git tag -a "v$version" "$commit" -m "v$version"
fi
git push origin "refs/tags/v$version"

say "GitHub release v$version"
if gh release view "v$version" >/dev/null 2>&1; then
  echo "GitHub release v$version exists"
else
  notes=$(awk -v heading="## $version" '$0 == heading { found = 1; next } /^## / && found { exit } found' CHANGELOG.md)
  gh release create "v$version" --verify-tag --title "qyl $version" --notes "$notes"
fi
say "production -> $commit"
git fetch origin production
if [[ $(git rev-parse origin/production) == "$commit" ]]; then
  echo "production is at $commit"
else
  git merge-base --is-ancestor origin/production "$commit" || die "production is not an ancestor of $commit; it never moves backwards"
  git push origin "$commit:refs/heads/production"
fi
echo "released $version from $commit"
