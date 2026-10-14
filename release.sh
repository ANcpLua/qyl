#!/usr/bin/env bash
# Publish and release qyl's NuGet packages (the qyl tool, its six runtime packages and
# Qyl.Api.Sdk) from this machine with the key registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            pack, push the eight packages, tag v<x.y.z>, GitHub release; returns once
#                                   NuGet accepted the push and the tag and the GitHub release exist, and leaves
#                                   the verification below running detached
#   ./release.sh <x.y.z> --dry-run  every check, the real pack and smoke, and a NuGet key probe for each of
#                                   the eight package ids; nothing is pushed, registry and git stay unchanged.
#                                   Checks the real run enforces (tag, already on NuGet) only warn. Prints
#                                   what the detached job would do
#   ./release.sh --wait <x.y.z>     blocks until artifacts/release-<x.y.z>-verify.log ends in VERIFIED (exit 0)
#                                   or FAILED (exit 1; exit 3 when not indexed in time), printing the log
#   ./release.sh --verify <x.y.z>   starts the detached verification again for a released version
#
# Order: the six runtime packages are pushed and must be indexed (up to 20 min, in the foreground)
# before the tool pointer qyl and Qyl.Api.Sdk are pushed; then the tag names the commit the checks
# and the pack ran on. The detached verification (own session, survives the caller's shell) runs
# `./eng/build.sh VerifyRelease --published-version <x.y.z>` (waits until nuget.org serves all eight,
# up to 45 min, then installs the published tool from nuget.org and runs QylToolSmoke against it),
# then qyl.at's scripts/refresh-published-versions.sh, and ends its log with VERIFIED or FAILED.
# Registry versions are immutable: a failed smoke after the fact is a patch release, and the log
# says so.
#
# Version source: Version.props QylVersion, bumped in its own commit before the first
# package-affecting change after a release. The tag names the release commit, whose Version.props
# must carry <x.y.z>; the script never edits the version.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NUGET_GALLERY=https://www.nuget.org/api/v2
NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
OUT=artifacts/nuget
RELEASE_NAME="qyl + qyl.<rid> + Qyl.Api.Sdk"
VERIFY_COMMAND="./eng/build.sh VerifyRelease"
VERIFY_WHAT="nuget.org index of all eight up to 45 min, then the published tool through QylToolSmoke"
QYL_AT_DIR=${QYL_AT_DIR:-$(cd .. && pwd)/qyl.at}

mode=release
case ${1:-} in --wait | --verify | __verify) mode=${1#--}; shift ;; esac
version=${1:-}
dry=0; [[ $mode == release && ${2:-} == --dry-run ]] && dry=1
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <x.y.z> [--dry-run] | --wait <x.y.z> | --verify <x.y.z>" >&2; exit 2; }
say() { printf '\n== %s\n' "$*"; }
die() { echo "release: $*" >&2; exit 1; }
utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }
verify_log="$PWD/artifacts/release-$version-verify.log"
index_status="$PWD/artifacts/release-$version-index.status"  # "waiting|indexed|timed-out elapsed=<s>s at=<utc>"
verify_pid() { sed -n '1s/.* pid \([0-9][0-9]*\) .*/\1/p' "$verify_log"; }
index_state() { [[ -f $index_status ]] && cut -d' ' -f1 "$index_status"; }
index_elapsed() { sed -n 's/.* elapsed=\([^ ]*\) .*/\1/p' "$index_status" 2>/dev/null; }

# The detached job: VerifyRelease (index wait, then the registry consumers), then the snapshot
# refresh. Its first log line names its pid, its last line is the verdict; exit 3 = not indexed in time.
verify_published() {
  local step="start" next_patch="${version%.*}.$(( ${version##*.} + 1 ))"
  printf 'verify %s %s pid %s started %s\n' "$RELEASE_NAME" "$version" "$$" "$(utc)"
  trap 'rc=$?; (( rc == 0 )) || printf "FAILED %s %s %s: %s stopped (exit %s); fix the cause, then ./release.sh --verify %s\n" "$RELEASE_NAME" "$version" "$(utc)" "$step" "$rc" "$version"' EXIT
  fail() { local code=$1; shift; printf 'FAILED %s %s %s: %s\n' "$RELEASE_NAME" "$version" "$(utc)" "$*"; trap - EXIT; exit "$code"; }
  rm -f "$index_status"

  step="VerifyRelease"
  say "$VERIFY_COMMAND --published-version $version ($VERIFY_WHAT)"
  if ! $VERIFY_COMMAND --published-version "$version"; then
    [[ $(index_state) == timed-out ]] &&
      fail 3 "not indexed after $(index_elapsed); nothing is wrong with the release yet: ./release.sh --verify $version checks again"
    fail 1 "a published package failed VerifyRelease (above; full log under artifacts/logs/); $version is immutable: fix the cause and ship $next_patch as a patch release (a transient network failure: ./release.sh --verify $version)"
  fi

  step="snapshot refresh"
  [[ $(index_state) == indexed ]] || fail 1 "VerifyRelease passed but $index_status does not say indexed; the snapshot stays as it is"
  say "refresh the published-versions snapshot in qyl.at"
  "$QYL_AT_DIR/scripts/refresh-published-versions.sh" ||
    fail 1 "$version is published and verified, but the snapshot refresh failed (above); ./release.sh --verify $version retries it"

  trap - EXIT
  printf 'VERIFIED %s %s %s: indexed after %s, registry consumers passed, snapshot refreshed\n' "$RELEASE_NAME" "$version" "$(utc)" "$(index_elapsed)"
}

# A new session (python3's setsid; macOS has no setsid command) without a controlling terminal: neither
# the end of the caller's shell nor a signal to its process group reaches the job. SIGINT, SIGQUIT and
# SIGHUP go back to their defaults first: `&` ignores SIGINT and SIGQUIT for the child, an ignored
# signal stays ignored across exec, and a smoke that stops its tool with Ctrl-C (qyl's) would hang.
# The job needs no registry credential.
start_verification() {
  local pid
  mkdir -p artifacts
  if [[ -f $verify_log ]] && ! tail -n 1 "$verify_log" | grep -Eq '^(VERIFIED|FAILED) '; then
    pid=$(verify_pid)
    [[ -n $pid ]] && kill -0 "$pid" 2>/dev/null && die "the verification of $version is still running (pid $pid): ./release.sh --wait $version"
  fi
  : >"$verify_log"
  env -u NPM_CONFIG_USERCONFIG -u NPM_CONFIG_TOKEN python3 -I -c 'import os, signal, sys
os.setsid()
for number in (signal.SIGINT, signal.SIGQUIT, signal.SIGHUP): signal.signal(number, signal.SIG_DFL)
os.execv(sys.argv[1], sys.argv[1:])' \
    "$PWD/release.sh" __verify "$version" </dev/null >>"$verify_log" 2>&1 &
  disown
  # Return only once the job has logged its pid, so a --wait right after this finds it.
  for _ in $(seq 1 50); do [[ -n $(verify_pid) ]] && break; sleep 0.2; done
  [[ -n $(verify_pid) ]] || die "the detached verification did not start; read $verify_log"
  say "verification runs detached (pid $!)"
  echo "log:  $verify_log"
  echo "wait: ./release.sh --wait $version   (or: tail -f $verify_log)"
}

wait_for_verification() {
  local printed=0 lines pid alive
  [[ -f $verify_log ]] || die "no verification log for $version at $verify_log; ./release.sh --verify $version starts one"
  while :; do
    pid=$(verify_pid); alive=0
    [[ -n $pid ]] && kill -0 "$pid" 2>/dev/null && alive=1
    lines=$(wc -l <"$verify_log" | tr -d ' ')
    (( lines > printed )) && { sed -n "$((printed + 1)),${lines}p" "$verify_log"; printed=$lines; }
    case $(tail -n 1 "$verify_log") in
      VERIFIED\ *) return 0 ;;
      FAILED\ *) [[ $(index_state) == timed-out ]] && return 3; return 1 ;;
    esac
    (( alive )) || die "the verification job (pid ${pid:-unknown}) ended without VERIFIED or FAILED; read $verify_log, then ./release.sh --verify $version"
    sleep 5
  done
}

case $mode in
  __verify) verify_published; exit 0 ;;
  wait) wait_for_verification; exit ;;
  verify)
    [[ -n $(git ls-remote --tags origin "refs/tags/v$version") ]] || die "tag v$version is not on origin; release it first"
    gh release view "v$version" >/dev/null 2>&1 || die "GitHub release v$version does not exist; release it first"
    start_verification
    exit 0 ;;
esac

lower() { tr '[:upper:]' '[:lower:]' <<<"$1"; }
indexed() { curl -fsSL "$NUGET_FLAT/$(lower "$1")/index.json" 2>/dev/null | grep -q "\"$2\""; }
latest() { curl -fsSL "$NUGET_FLAT/$(lower "$1")/index.json" 2>/dev/null | grep -o '"[0-9][^"]*"' | tail -1 | tr -d '"'; }
# The key probe publishes nothing: it is NuGet's push-symbols handshake. The key mints a one-use
# package verification key for <id> <version> (only a live key with push scope can), and that key
# verifies with 200 only when the key's account owns <id>; the verify call consumes it. The key
# travels in a curl config on stdin, never on a command line.
key_probe() {
  local id=$1 version=$2 minted temp
  minted=$(printf 'header = "X-NuGet-ApiKey: %s"\n' "$(<"$NUGET_KEY_FILE")" |
    curl -fsS -X POST -H 'Content-Length: 0' -K - "$NUGET_GALLERY/package/create-verification-key/$id/$version") || return 1
  temp=$(sed -n 's/.*"Key":"\([^"]*\)".*/\1/p' <<<"$minted")
  [[ -n $temp ]] || return 1
  [[ $(printf 'header = "X-NuGet-ApiKey: %s"\n' "$temp" |
    curl -sS -o /dev/null -w '%{http_code}' -K - "$NUGET_GALLERY/verifykey/$id/$version") == 200 ]]
}

# Up to 20 minutes: the 43 MB osx runtime packages of 12.2.0 took about 15 minutes to index.
# Polls back off from 5 s to 60 s.
wait_for_indexed() {
  local package ready started=$SECONDS delay=5
  while :; do
    ready=1
    for package in "$@"; do indexed "$package" "$version" || ready=0; done
    (( ready )) && { echo "indexed after $(( SECONDS - started )) s: $*"; return 0; }
    (( SECONDS - started + delay > 1200 )) && return 1
    sleep "$delay"; delay=$(( delay * 2 > 60 ? 60 : delay * 2 ))
  done
}

publish_release_set() {
  local runtime package
  local runtime_ids=()
  for runtime in $rids; do
    runtime_ids+=("qyl.$runtime")
    # --skip-duplicate here and below: a rerun after a partial push (pushed, not indexed yet) goes on.
    NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$OUT/qyl.$runtime.$version.nupkg" --source "$NUGET_SOURCE" --skip-duplicate || return 1
  done
  say "wait for all runtime package indexes before publishing the tool pointer"
  wait_for_indexed "${runtime_ids[@]}" || return 1
  for package in Qyl.Api.Sdk qyl; do
    NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$OUT/$package.$version.nupkg" --source "$NUGET_SOURCE" --skip-duplicate || return 1
  done
}

say "checks"
[[ -r $NUGET_KEY_FILE ]] || die "NuGet key missing: $NUGET_KEY_FILE (vitals keys)"
[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "working tree not clean"
git fetch -q origin main
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main differs from origin/main"
release_commit=$(git rev-parse HEAD)  # the commit the checks, the pack and the smoke run on; the tag names it
echo "release commit: $release_commit"
if [[ -n $(git ls-remote --tags origin "refs/tags/v$version") ]] || git rev-parse -q --verify "refs/tags/v$version" >/dev/null; then
  (( dry )) || die "tag v$version already exists"; echo "warning: tag v$version already exists; the real run refuses"
fi
qv=$(sed -n 's:.*<QylVersion>\([^<]*\)</QylVersion>.*:\1:p' Version.props)
[[ $qv == "$version" ]] || die "Version.props QylVersion is $qv, not $version; bump it in its own commit first"
for id in qyl Qyl.Api.Sdk; do
  if indexed "$id" "$version"; then (( dry )) || die "$id $version is already on NuGet"; echo "warning: $id $version is already on NuGet; the real run refuses"; fi
done
gh auth status >/dev/null 2>&1 || die "gh is not logged in"
rids=$(sed -n 's:.*<QylToolRuntimeIdentifiers>\([^<]*\)</QylToolRuntimeIdentifiers>.*:\1:p' packages/Qyl.Cli/Qyl.Cli.csproj | tr ';' ' ')
[[ -n $rids ]] || die "QylToolRuntimeIdentifiers not found in packages/Qyl.Cli/Qyl.Cli.csproj"
package_ids=(qyl Qyl.Api.Sdk $(for r in $rids; do echo "qyl.$r"; done))
expected=$(for id in "${package_ids[@]}"; do echo "$id.$version.nupkg"; done | sort)
echo "expected package set: ${#package_ids[@]} packages"

say "pack (Fallout PackRelease)"
./eng/build.sh PackRelease --Configuration Release --PackageVersion "$version" --RepositoryCommit "$release_commit"
actual=$(find "$OUT" -maxdepth 1 -name '*.nupkg' -exec basename {} \; | sort)
[[ $actual == "$expected" ]] || { echo "expected:"; echo "$expected"; echo "packed:"; echo "$actual"; die "package set mismatch"; }

say "smoke the tool packages on this host"
dotnet run --project eng/tools/QylToolSmoke/QylToolSmoke.csproj --configuration Release --no-build -- "$OUT"

[[ $(git rev-parse HEAD) == "$release_commit" ]] || die "HEAD changed during packing; rerun from the intended release commit"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "tracked files changed during packing; commit them and rerun"

say "NuGet push"
if (( dry )); then
  for id in "${package_ids[@]}"; do
    published=$(latest "$id" || true)
    if [[ -z $published ]]; then
      echo "warning: $id is not on NuGet yet; the key probe needs a published version, so its first push is the probe"
      continue
    fi
    # A rejected key fails every attempt; a dropped connection does not.
    probed=0
    for _ in 1 2 3; do key_probe "$id" "$published" && { probed=1; break; }; sleep 3; done
    (( probed )) || die "the NuGet key failed the probe for $id three times: expired, revoked, without push scope, or its account does not own $id (vitals keys)"
    echo "key probe: $id ok (verified against $published, nothing published)"
  done
  say "dry run finished: ${#package_ids[@]} packages at $version in $OUT; nothing published, nothing tagged"
  git status --short --untracked-files=no
  echo "The real run pushes the runtime packages, waits for their index, pushes Qyl.Api.Sdk and qyl, tags"
  echo "$release_commit and creates the GitHub release, then starts the detached verification ($verify_log,"
  echo "./release.sh --wait $version): $VERIFY_COMMAND --published-version $version ($VERIFY_WHAT),"
  echo "then $QYL_AT_DIR/scripts/refresh-published-versions.sh; the log ends with VERIFIED or FAILED."
  exit 0
fi
publish_release_set || die "package publication incomplete; runtime packages must be indexed before the tool pointer can publish; a rerun of ./release.sh $version skips what NuGet already holds and goes on"

say "tag and GitHub release"
[[ $(git rev-parse HEAD) == "$release_commit" ]] ||
  echo "warning: HEAD moved to $(git rev-parse --short HEAD) during the release; v$version tags the packed $release_commit"
git tag -a "v$version" "$release_commit" -m "v$version"
git push -q origin "v$version"
if gh release view "v$version" >/dev/null 2>&1; then
  gh release upload "v$version" "$OUT"/*.nupkg --clobber
else
  gh release create "v$version" --title "v$version" --generate-notes "$OUT"/*.nupkg
fi

start_verification
say "next (SKILL.md): ./release.sh --wait $version, then notify qyl.at (src/data/site.ts); pushing main already deployed api.qyl.at"
