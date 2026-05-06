#!/usr/bin/env bash
# Regression test for the SEMCONV_STAGING_DIR safety guard in run-weaver.sh.
#
# The guard rejects empty, root, and non-absolute STAGING_DIR values before
# any `rm -rf "${STAGING_DIR}/..."` runs. Without it, a misconfigured CI
# value could delete outside the workspace.
#
# Tests both negative paths (invalid values) and positive paths (valid absolute
# paths). The positive test stops before invoking Weaver (out of scope).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN="${SCRIPT_DIR}/run-weaver.sh"
failures=0

test_rejected() {
  local name="$1" value="$2" expect="$3" actual
  if actual="$(SEMCONV_STAGING_DIR="$value" bash "$RUN" 2>&1)"; then
    echo "FAIL: $name — expected non-zero exit, script succeeded"
    failures=$((failures + 1))
    return
  fi
  if ! grep -qF "$expect" <<<"$actual"; then
    echo "FAIL: $name — stderr missing expected substring"
    echo "       expected: $expect"
    echo "       got:"
    sed 's/^/        /' <<<"$actual"
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $name"
}

test_accepted() {
  local name="$1" value="$2" actual exit_code=0
  if ! actual="$(SEMCONV_STAGING_DIR="$value" bash "$RUN" 2>&1)"; then
    exit_code=$?
    echo "FAIL: $name — expected zero exit, script failed with exit code $exit_code"
    echo "       stderr:"
    sed 's/^/        /' <<<"$actual"
    failures=$((failures + 1))
    return
  fi
  echo "PASS: $name"
}

test_rejected "empty value"     ""          "must be a non-empty absolute path"
test_rejected "root path"       "/"         "must be a non-empty absolute path"
test_rejected "relative path"   "rel/path"  "must be absolute"
test_accepted "valid absolute path" "/tmp/valid"

if (( failures > 0 )); then
  echo
  echo "$failures test(s) failed."
  exit 1
fi
echo
echo "All SEMCONV_STAGING_DIR guard tests passed."
