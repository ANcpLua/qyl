#!/usr/bin/env bash
# Regression test for the SEMCONV_STAGING_DIR safety guard in run-weaver.sh.
#
# The guard rejects empty, root, and non-absolute STAGING_DIR values before
# any `rm -rf "${STAGING_DIR}/..."` runs. Without it, a misconfigured CI
# value could delete outside the workspace.
#
# Tests both negative paths (rejected values) and positive path (valid absolute)
# by extracting and running only the guard check portion of run-weaver.sh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN="${SCRIPT_DIR}/run-weaver.sh"
failures=0

test_rejected() {
  local name="$1" value="$2" expect="$3" actual_stderr
  # Capture stderr only — `2>&1 >/dev/null` swaps fds so the subshell
  # captures fd 2 while fd 1 is discarded. Avoids a false positive if the
  # error were ever accidentally emitted on stdout.
  if actual_stderr="$(SEMCONV_STAGING_DIR="$value" bash "$RUN" 2>&1 >/dev/null)"; then
    echo "FAIL: $name — expected non-zero exit, script succeeded"
    failures=$((failures + 1))
    return
  fi
  if ! grep -qF "$expect" <<<"$actual_stderr"; then
    echo "FAIL: $name — stderr missing expected substring"
    echo "       expected: $expect"
    echo "       got:"
    sed 's/^/        /' <<<"$actual_stderr"
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $name"
}

test_rejected "empty value"     ""          "must be a non-empty absolute path"
test_rejected "root path"       "/"         "must be a non-empty absolute path"
test_rejected "relative path"   "rel/path"  "must be absolute"

# Test valid absolute path — should pass the guard check (will fail later at
# missing Weaver binary, but we only care that the guard accepts it).
test_accepted() {
  local name="$1" value="$2"
  # Run just the STAGING_DIR guard portion (up to line 48).
  # Extract and run only the safety guard check portion of run-weaver.sh.
  local guard_check
  guard_check=$(sed -n '16,48p' "$RUN")

  if ! (
    SEMCONV_STAGING_DIR="$value"
    eval "$guard_check" 2>/dev/null
  ); then
    echo "FAIL: $name — expected guard to accept value, but it rejected it"
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $name"
}

test_accepted "valid absolute path" "/tmp/semconv-test-staging"

if (( failures > 0 )); then
  echo
  echo "$failures test(s) failed."
  exit 1
fi
echo
echo "All SEMCONV_STAGING_DIR guard tests passed."
