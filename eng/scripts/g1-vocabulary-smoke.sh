#!/usr/bin/env bash
# G1 vocabulary smoke (cheap CI cross-check, not the gate — QYL0200 is authoritative).
#
# Greps for '"qyl.' string literals in the telemetry-emitting projects of this
# repository: the collector process source (services/qyl.collector) and its
# service-defaults layer (internal/qyl.instrumentation). Build tooling (eng/,
# generator projects), tests, and generated files (*.g.cs) are outside the scope
# by the gate's construction — package IDs, directory names, executable names,
# and generator-owned defaults live there, never in hand-written runtime source.
#
# Expected result: zero hits. A hit means hand-written vocabulary — move the name
# into the registry (qyl-registry.json in the semconv repo) or a generator, and
# reference the generated constant. A missing scan scope is a failure, not a
# clean result: after a rename moves these directories, the scope list must move
# with them, or the smoke would go green over nothing.
set -euo pipefail
cd "$(dirname "$0")/../.."

scopes=(services/qyl.collector internal/qyl.instrumentation)
for scope in "${scopes[@]}"; do
  if [[ ! -d "$scope" ]]; then
    echo "G1 vocabulary smoke FAILED — scan scope missing: $scope (move this list with the rename)" >&2
    exit 1
  fi
done

# grep exits 1 on zero matches (the expected pass) and >1 on error; only the
# former may be treated as success.
set +e
raw=$(grep -rn '"qyl\.' --include='*.cs' "${scopes[@]}")
status=$?
set -e
if (( status > 1 )); then
  echo "G1 vocabulary smoke FAILED — grep exited $status (not a zero-match result)" >&2
  exit 1
fi

hits=$(printf '%s\n' "$raw" | grep -v '\.g\.cs:' | grep -v '/obj/\|/bin/' || true)

if [[ -n "$hits" ]]; then
  echo "G1 vocabulary smoke FAILED — hand-written \"qyl.* literals in telemetry-emitting scope:" >&2
  echo "$hits" >&2
  exit 1
fi

echo "G1 vocabulary smoke passed: 0 hits in scope."
