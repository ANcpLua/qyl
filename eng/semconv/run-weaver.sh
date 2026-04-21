#!/usr/bin/env bash
# Run qyl's Weaver template pipeline against the upstream semconv v1.40.0 registry.
#
# Proof-of-pipeline invocation for the in-flight `generate-semconv.ts` → Weaver migration.
# Emits draft outputs to eng/semconv/out/ (gitignored) for side-by-side diff against
# the live outputs committed under src/.
#
# Prerequisites (one-time setup):
#   ./eng/semconv/bootstrap-weaver.sh    # downloads weaver + upstream semconv clone

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEAVER_BIN="${REPO_ROOT}/.tools/weaver/weaver-aarch64-apple-darwin/weaver"
UPSTREAM_REGISTRY="${REPO_ROOT}/.tools/semconv-upstream/model"
TEMPLATES_ROOT="${REPO_ROOT}/eng/semconv/templates/registry"
OUT_DIR="${REPO_ROOT}/eng/semconv/out"

if [ ! -x "${WEAVER_BIN}" ]; then
  echo "Weaver binary not found at ${WEAVER_BIN}" >&2
  echo "Run: ./eng/semconv/bootstrap-weaver.sh" >&2
  exit 1
fi

if [ ! -d "${UPSTREAM_REGISTRY}" ]; then
  echo "Upstream registry not found at ${UPSTREAM_REGISTRY}" >&2
  echo "Run: ./eng/semconv/bootstrap-weaver.sh" >&2
  exit 1
fi

rm -rf "${OUT_DIR}"
"${WEAVER_BIN}" registry generate \
  --registry "${UPSTREAM_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  qyl \
  "${OUT_DIR}"

echo ""
echo "Weaver outputs:"
ls -la "${OUT_DIR}"
