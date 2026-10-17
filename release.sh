#!/usr/bin/env bash
# Publish and release qyl's NuGet packages (the qyl tool, its six runtime packages and
# Qyl.Api.Sdk and Qyl.Orchestration) from this machine with the key registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            pack, smoke, publish, tag, GitHub release, verify, refresh; exit 0 only
#                                   when the complete release is VERIFIED
#   ./release.sh <x.y.z> --dry-run  every check, the real pack and smoke, and a NuGet key probe for each of
#                                   the nine package ids; nothing is pushed, registry and git stay unchanged.
#                                   Checks the real run enforces (tag, already on NuGet) only warn. Prints
#                                   what verification would do; the result is DRY_RUN, never VERIFIED
#   ./release.sh --verify <x.y.z>   verify uploaded packages and finish a matching GitHub draft; no NuGet pushes
#   ./release.sh --status <x.y.z>   print the last attempt's JSON status and return its recorded exit code
#                                   (4 = still running; 1 = interrupted without a final result)
#
# Order: the six runtime packages are pushed and must be indexed (up to 20 min, in the foreground)
# before the tool pointer qyl and Qyl.Api.Sdk are pushed; then the tag names the commit the checks
# and the pack ran on. Verification runs in the same invocation:
# `./eng/build.sh VerifyRelease --published-version <x.y.z>` (waits until nuget.org serves all nine,
# up to 45 min, then installs the published tool from nuget.org and runs QylToolSmoke against it),
# then qyl.at's scripts/refresh-published-versions.sh. Every attempt keeps its own log and JSON status
# under artifacts/release-<version>-<attempt>/; artifacts/release-<version>.status.json names the latest.
# Nonzero exits stop the release immediately; exit 3 with PENDING means registry indexing is unresolved.
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
RELEASE_NAME="qyl + qyl.<rid> + Qyl.Api.Sdk + Qyl.Orchestration"
VERIFY_COMMAND="./eng/build.sh VerifyRelease"
VERIFY_WHAT="nuget.org index of all nine up to 45 min, then the published tool through QylToolSmoke"
QYL_AT_DIR=${QYL_AT_DIR:-$(cd .. && pwd)/qyl.at}

mode=release
case ${1:-} in --verify | --status) mode=${1#--}; shift ;; esac
version=${1:-}
dry=0; [[ $mode == release && ${2:-} == --dry-run ]] && dry=1
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <x.y.z> [--dry-run] | --verify <x.y.z> | --status <x.y.z>" >&2; exit 2; }
say() { printf '\n== %s\n' "$*"; }
die() { echo "release: $*" >&2; exit 1; }
utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }
latest_status="$PWD/artifacts/release-$version.status.json"
index_status="$PWD/artifacts/release-$version-index.status"  # "waiting|indexed|timed-out elapsed=<s>s at=<utc>"
index_state() { [[ -f $index_status ]] && cut -d' ' -f1 "$index_status"; }
index_elapsed() { sed -n 's/.* elapsed=\([^ ]*\) .*/\1/p' "$index_status" 2>/dev/null; }

if [[ $mode == status ]]; then
  python3 -I - "$latest_status" <<'PY'
import json, os, sys
try:
    with open(sys.argv[1]) as stream:
        status = json.load(stream)
except (OSError, ValueError) as error:
    print(f"release: no readable result: {error}", file=sys.stderr)
    sys.exit(1)
if status["state"] == "RUNNING":
    try:
        os.kill(status["pid"], 0)
    except ProcessLookupError:
        status["state"] = "INTERRUPTED"
        code = 1
    else:
        code = 4
else:
    code = status["exit_code"]
print(json.dumps(status, indent=2))
sys.exit(code)
PY
  exit
fi

# Preserve each attempt, including pre-publication failures. A later successful verification never
# erases the failed run. The latest status is atomic and carries the script's actual exit code.
if [[ -f $latest_status ]]; then
  previous_pid=$(python3 -I - "$latest_status" <<'PY'
import json, sys
with open(sys.argv[1]) as stream:
    status = json.load(stream)
if status["state"] == "RUNNING":
    print(status["pid"])
PY
  )
  [[ -z $previous_pid ]] || ! kill -0 "$previous_pid" 2>/dev/null ||
    die "a release attempt is still running (pid $previous_pid); ./release.sh --status $version"
fi
mkdir -p artifacts
attempt_dir=$(mktemp -d "$PWD/artifacts/release-$version-XXXXXXXX")
run_log="$attempt_dir/release.log"
status_file="$attempt_dir/status.json"
phases_log="$attempt_dir/phases.jsonl"
diagnostics_dir="$attempt_dir/diagnostics"
mkdir -p "$diagnostics_dir"
started=$(utc)
step=start
phase_started=
release_commit=unknown
result=FAILED
write_status() {
  python3 -I - "$status_file" "$latest_status" "$version" "$mode" "$step" "$1" "$2" "$release_commit" "$$" "$started" "$run_log" "$SECONDS" "${phase_started:-0}" "$phases_log" "$diagnostics_dir" <<'PY'
import datetime, json, os, sys
attempt, latest, version, mode, phase, state, code, commit, pid, started, log, elapsed, phase_start, phases_log, diagnostics = sys.argv[1:]
status = dict(version=version, mode=mode, phase=phase, state=state,
              exit_code=None if code == "-1" else int(code), commit=commit, pid=int(pid),
              started=started, updated=datetime.datetime.now(datetime.timezone.utc).isoformat(), log=log,
              elapsed_seconds=int(elapsed), phase_elapsed_seconds=max(0, int(elapsed)-int(phase_start)),
              phases_log=phases_log, diagnostics=diagnostics)
for path in (attempt, latest):
    temporary = path + ".tmp"
    with open(temporary, "w") as stream:
        json.dump(status, stream, indent=2)
        stream.write("\n")
    os.replace(temporary, path)
PY
}
record_phase() {
  [[ -n $phase_started ]] || return 0
  python3 -I - "$phases_log" "$step" "$phase_started" "$SECONDS" "$1" "$2" <<'PY'
import datetime, json, sys
path, phase, start, end, state, code = sys.argv[1:]
with open(path, "a") as stream:
    stream.write(json.dumps(dict(phase=phase, state=state, exit_code=int(code),
        elapsed_seconds=max(0, int(end)-int(start)),
        finished=datetime.datetime.now(datetime.timezone.utc).isoformat())) + "\n")
PY
}
finish() {
  local code=$?
  trap - EXIT
  if (( code != 0 )); then
    [[ $result == PENDING ]] || result=FAILED
  elif [[ $result != VERIFIED && $result != DRY_RUN ]]; then
    code=1
    result=FAILED
  fi
  if ! record_phase "$result" "$code"; then
    result=FAILED
    code=1
    echo "release: could not record phase diagnostics" >&2
  fi
  if ! write_status "$result" "$code"; then
    result=FAILED
    code=1
    echo "release: could not record the final result" >&2
  fi
  printf '%s %s %s %s: phase=%s exit=%s elapsed=%ss log=%s\n' "$result" "$RELEASE_NAME" "$version" "$(utc)" "$step" "$code" "$SECONDS" "$run_log"
  printf 'Phase timings: %s\nCompiler diagnostics: %s\n' "$phases_log" "$diagnostics_dir"
  exit "$code"
}
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
exec > >(tee "$run_log") 2>&1
phase() {
  record_phase PASSED 0 || return $?
  step=$1
  phase_started=$SECONDS
  write_status RUNNING -1 || return $?
  say "$step"
}
phase start

verify_published() {
  local code=0 draft
  phase VerifyRelease
  rm -f "$index_status"
  say "$VERIFY_COMMAND --published-version $version ($VERIFY_WHAT)"
  $VERIFY_COMMAND --published-version "$version" || code=$?
  if (( code != 0 )); then
    if [[ $(index_state) == timed-out ]]; then
      echo "release: indexing pending after $(index_elapsed); resume with ./release.sh --verify $version"
      result=PENDING
      exit 3
    fi
    echo "release: published verification failed; inspect this attempt's log. Retry transient failures with ./release.sh --verify $version; a broken published package needs a patch release."
    exit "$code"
  fi
  [[ $(index_state) == indexed ]] || die "VerifyRelease passed without indexed status; snapshot not refreshed"
  phase "publish GitHub release"
  draft=$(gh release view "v$version" --json isDraft --jq '.isDraft')
  if [[ $draft == true ]]; then
    python3 eng/scripts/verify-release-ci.py "$release_commit"
    gh release edit "v$version" --draft=false
    draft=$(gh release view "v$version" --json isDraft --jq '.isDraft')
  fi
  [[ $draft == false ]] || die "GitHub release is still a draft; snapshot not refreshed"
  phase "snapshot refresh"
  "$QYL_AT_DIR/scripts/refresh-published-versions.sh" --expect-nuget "$version" "${package_ids[@]}"
  result=VERIFIED
}

lower() { tr '[:upper:]' '[:lower:]' <<<"$1"; }
package_index() {
  curl -fsSL --connect-timeout 10 --max-time 30 -H 'Cache-Control: no-cache' \
    "$NUGET_FLAT/$(lower "$1")/index.json?qyl-release=$(date +%s)-$$"
}
indexed() { package_index "$1" 2>/dev/null | grep -q "\"$2\""; }
latest() { package_index "$1" 2>/dev/null | grep -o '"[0-9][^"]*"' | tail -1 | tr -d '"'; }

if [[ $mode == verify ]]; then
  phase "check published release"
  [[ -n $(git ls-remote --tags origin "refs/tags/v$version") ]] || die "tag v$version is not on origin; no packages will be published by --verify"
  assets=$(gh release view "v$version" --json assets --jq '.assets[].name')
  git fetch -q origin tag "v$version"
  release_commit=$(git rev-parse "v$version^{commit}")
  rids=$(sed -n 's:.*<QylToolRuntimeIdentifiers>\([^<]*\)</QylToolRuntimeIdentifiers>.*:\1:p' packages/Qyl.Cli/Qyl.Cli.csproj | tr ';' ' ')
  [[ -n $rids ]] || die "QylToolRuntimeIdentifiers not found"
  package_ids=(qyl Qyl.Api.Sdk Qyl.Orchestration $(for r in $rids; do echo "qyl.$r"; done))
  expected=$(for id in "${package_ids[@]}"; do echo "$id.$version.nupkg"; done | sort)
  [[ $(sort <<<"$assets") == "$expected" ]] || die "GitHub release assets do not match the release set; verification cannot repair publication"
  # A manually created tag/release is not proof that NuGet accepted any package. Do not start a
  # 45-minute verifier against an unpublished version. This mode never pushes packages.
  for id in "${package_ids[@]}"; do
    if ! indexed "$id" "$version"; then
      echo "release: $id $version is not available; retry ./release.sh --verify $version after indexing"
      result=PENDING
      exit 3
    fi
  done
  verify_published
  exit 0
fi
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
    NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$OUT/qyl.$runtime.$version.nupkg" --source "$NUGET_SOURCE" --skip-duplicate || return $?
  done
  phase "runtime package indexing" || return $?
  if ! wait_for_indexed "${runtime_ids[@]}"; then
    result=PENDING
    return 3
  fi
  phase "publish libraries and tool pointer" || return $?
  for package in Qyl.Orchestration Qyl.Api.Sdk qyl; do
    NUGET_API_KEY="$(<"$NUGET_KEY_FILE")" dotnet nuget push "$OUT/$package.$version.nupkg" --source "$NUGET_SOURCE" --skip-duplicate || return $?
  done
}

phase checks
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
for id in qyl Qyl.Api.Sdk Qyl.Orchestration; do
  if indexed "$id" "$version"; then (( dry )) || die "$id $version is already on NuGet"; echo "warning: $id $version is already on NuGet; the real run refuses"; fi
done
gh auth status >/dev/null 2>&1 || die "gh is not logged in"
rids=$(sed -n 's:.*<QylToolRuntimeIdentifiers>\([^<]*\)</QylToolRuntimeIdentifiers>.*:\1:p' packages/Qyl.Cli/Qyl.Cli.csproj | tr ';' ' ')
[[ -n $rids ]] || die "QylToolRuntimeIdentifiers not found in packages/Qyl.Cli/Qyl.Cli.csproj"
package_ids=(qyl Qyl.Api.Sdk Qyl.Orchestration $(for r in $rids; do echo "qyl.$r"; done))
expected=$(for id in "${package_ids[@]}"; do echo "$id.$version.nupkg"; done | sort)
echo "expected package set: ${#package_ids[@]} packages"

phase "full Linux CI for the release commit"
python3 eng/scripts/verify-release-ci.py "$release_commit"

phase "pack (Fallout PackRelease)"
./eng/build.sh PackRelease --Configuration Release --PackageVersion "$version" --RepositoryCommit "$release_commit" --PackDiagnosticDirectory "$diagnostics_dir"
actual=$(find "$OUT" -maxdepth 1 -name '*.nupkg' -exec basename {} \; | sort)
[[ $actual == "$expected" ]] || { echo "expected:"; echo "$expected"; echo "packed:"; echo "$actual"; die "package set mismatch"; }

phase "smoke the tool packages on this host"
dotnet run --project eng/tools/QylToolSmoke/QylToolSmoke.csproj --configuration Release --no-build -- "$OUT"
./eng/scripts/smoke-orchestration-package.sh "$version" "$PWD/$OUT"

[[ $(git rev-parse HEAD) == "$release_commit" ]] || die "HEAD changed during packing; rerun from the intended release commit"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "tracked files changed during packing; commit them and rerun"

phase "NuGet push"
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
  echo "The real run pushes the runtime packages, waits for their index, pushes Qyl.Orchestration, Qyl.Api.Sdk and qyl, tags"
  echo "$release_commit and creates the GitHub release, then runs $VERIFY_COMMAND --published-version $version ($VERIFY_WHAT),"
  echo "then $QYL_AT_DIR/scripts/refresh-published-versions.sh in the same invocation."
  result=DRY_RUN
  exit 0
fi
publish_code=0
publish_release_set || publish_code=$?
if (( publish_code != 0 )); then
  echo "release: package publication incomplete; no tag, GitHub release or verification will be started. Inspect the log; if only runtime packages were pushed, rerun ./release.sh $version to continue."
  exit "$publish_code"
fi

phase "tag and GitHub release"
[[ $(git rev-parse HEAD) == "$release_commit" ]] ||
  echo "warning: HEAD moved to $(git rev-parse --short HEAD) during the release; v$version tags the packed $release_commit"
git tag -a "v$version" "$release_commit" -m "v$version"
git push -q origin "v$version"
github_code=0
for attempt in 1 2 3; do
  github_code=0
  if gh release view "v$version" >/dev/null 2>&1; then
    gh release upload "v$version" "$OUT"/*.nupkg --clobber || github_code=$?
  else
    gh release create "v$version" --draft --title "v$version" --generate-notes "$OUT"/*.nupkg || github_code=$?
  fi
  (( github_code != 0 )) || break
  (( attempt == 3 )) || sleep 5
done
(( github_code == 0 )) || exit "$github_code"

verify_published
