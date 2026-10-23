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
# 3. Publish dependencies, runtime packages, the tool pointer and npm, in that order.
#    Each group must be visible in its registry and downloadable before the next starts.
# 4. Tag v<version> on the packed commit, push the tag and create its GitHub release. Its notes are
#    the commit subjects since the previous v* tag, grouped by their conventional prefix; the commit
#    texts are the change record, so there is no CHANGELOG file to keep in step.
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
READY_TIMEOUT=${QYL_RELEASE_READY_TIMEOUT:-1800}

die() { echo "release: $*" >&2; exit 1; }
# The commit subjects since the previous v* tag, grouped by conventional prefix; release commits are left out.
release_notes() {
  local previous range subjects group heading lines
  previous=$(git describe --tags --abbrev=0 --match 'v*' "$1^" 2>/dev/null || true)
  range=${previous:+$previous..}$1
  subjects=$(git log --no-merges --format='%s' "$range" | grep -v '^release: ' || true)
  [[ -n $previous ]] && printf 'Changes since %s.\n' "$previous"
  for group in 'feat:Features' 'fix:Fixes' 'perf:Performance' 'refactor:Refactoring' 'build:Build' 'test:Tests' 'docs:Documentation' 'chore:Maintenance'; do
    heading=${group#*:}
    lines=$(grep -E "^${group%%:*}(\([^)]*\))?!?: " <<<"$subjects" | sed -E 's/^[a-z]+(\([^)]*\))?!?: /- /' || true)
    [[ -n $lines ]] && printf '\n## %s\n\n%s\n' "$heading" "$lines"
  done
  lines=$(grep -vE '^(feat|fix|perf|refactor|build|test|docs|chore)(\([^)]*\))?!?: ' <<<"$subjects" | sed 's/^/- /' || true)
  [[ -n $lines ]] && printf '\n## Other\n\n%s\n' "$lines"
  return 0
}
say() { printf '\n== %s\n' "$*"; }

lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }

nuget_ready() {
  local id index
  id=$(lower "$1")
  index=$(curl -fsS --connect-timeout 10 --max-time 30 \
    -H 'Cache-Control: no-cache' "$NUGET_FLAT/$id/index.json?release=$$-$SECONDS" 2>/dev/null) || return 1
  jq -e --arg version "$version" '.versions | index($version) != null' <<<"$index" >/dev/null || return 1
  curl -fsSI --connect-timeout 10 --max-time 30 \
    "$NUGET_FLAT/$id/$version/$id.$version.nupkg" >/dev/null 2>&1
}

npm_metadata() {
  curl -fsS --connect-timeout 10 --max-time 30 \
    -H 'Cache-Control: no-cache' "$NPM_REGISTRY/qyl-mcp-server/$version?release=$$-$SECONDS" 2>/dev/null
}

npm_published() {
  npm_metadata | jq -e --arg version "$version" '.version == $version' >/dev/null
}

npm_ready() {
  local metadata tarball
  metadata=$(npm_metadata) || return 1
  tarball=$(jq -er --arg version "$version" 'select(.version == $version) | .dist.tarball' <<<"$metadata") || return 1
  curl -fsSI --connect-timeout 10 --max-time 30 "$tarball" >/dev/null 2>&1
}

wait_ready() {
  local label=$1 started=$SECONDS delay=2
  shift
  until "$@"; do
    (( SECONDS - started < READY_TIMEOUT )) || die "$label is not downloadable after ${READY_TIMEOUT}s; rerun to resume"
    printf 'Waiting for %s (%ss)\n' "$label" "$((SECONDS - started))"
    sleep "$delay"
    delay=$((delay < 15 ? delay * 2 : 30))
  done
  printf '%s is indexed and downloadable\n' "$label"
}

publish_nuget_group() {
  local label=$1 id
  shift
  say "$label"
  for id in "$@"; do
    if ! nuget_ready "$id"; then
      # A duplicate can still be awaiting validation: readiness is checked below.
      NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$dir/$id.$version.nupkg" \
        --source "$NUGET_SOURCE" --skip-duplicate --timeout 1800
    fi
  done
  for id in "$@"; do
    wait_ready "$id $version" nuget_ready "$id"
  done
}

version=$(sed -n 's:.*<QylVersion>\([^<]*\)</QylVersion>.*:\1:p' Version.props)
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Version.props QylVersion is '$version', not x.y.z"
[[ $READY_TIMEOUT =~ ^[1-9][0-9]*$ ]] || die "QYL_RELEASE_READY_TIMEOUT must be a positive number of seconds"
dir=artifacts/release/$version

if [[ ${1:-} == --plan && $# == 1 ]]; then
  printf 'Release %s from a sealed artifact set:\n' "$version"
  printf '%s\n' \
    '1. Pack once and verify SHA512SUMS.' \
    '2. Publish contracts, orchestration and API SDK; wait until downloadable.' \
    '3. Publish runtime packages; wait until downloadable.' \
    '4. Publish the qyl tool pointer; wait until downloadable.' \
    '5. Publish qyl-mcp-server; wait until downloadable.' \
    '6. Create the release and advance production to the packed commit.'
  exit 0
fi
[[ $# == 0 ]] || die "usage: ./release.sh [--plan]"
command -v jq >/dev/null || die "jq is required to read registry metadata"
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

runtime_packages=()
for file in "$dir"/qyl.*.nupkg; do
  id=${file##*/}; id=${id%".$version.nupkg"}
  case "$id" in
    qyl.linux-*|qyl.osx-*|qyl.win-*) runtime_packages+=("$id") ;;
  esac
done
(( ${#runtime_packages[@]} > 0 )) || die "the sealed set contains no runtime packages"

publish_nuget_group 'publish NuGet libraries' Qyl.Api.Contracts Qyl.Orchestration Qyl.Api.Sdk
publish_nuget_group 'publish runtime packages' "${runtime_packages[@]}"
publish_nuget_group 'publish the qyl tool pointer' qyl

say "publish to npm"
if npm_published; then
  echo "qyl-mcp-server $version is already uploaded; checking availability"
else
  (cd services/qyl.mcp && NPM_CONFIG_TOKEN="$(<"$NPM_TOKEN_FILE")" bun publish "$OLDPWD/$dir/qyl-mcp-server-$version.tgz" --registry "$NPM_REGISTRY" --access public)
fi
wait_ready "qyl-mcp-server $version" npm_ready

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
  notes=$(release_notes "$commit")
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
