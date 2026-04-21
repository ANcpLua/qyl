#!/usr/bin/env bash
# Generate qyl's semconv outputs into the final src/ destinations via Weaver.
#
# Pinned inputs:  open-telemetry/semantic-conventions v1.40.0 (cloned by bootstrap)
# Output targets:
#   - src/qyl.dashboard/src/lib/semconv.ts          (TypeScript const keys)
#   - src/qyl.collector/Storage/promoted-columns.g.sql  (DuckDB columns)
#
# Not emitted by Weaver yet (committed files stay as-is until templated):
#   - core/specs/generated/semconv.g.tsp            (TypeSpec — huge, future work)
#   - src/qyl.contracts/Attributes/*Attributes.cs   (hand-maintained facades)
#
# Bootstrap once per clone: ./eng/semconv/bootstrap-weaver.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEAVER_BIN="${REPO_ROOT}/.tools/weaver/weaver-aarch64-apple-darwin/weaver"
UPSTREAM_REGISTRY="${REPO_ROOT}/.tools/semconv-upstream/model"
TEMPLATES_ROOT="${REPO_ROOT}/eng/semconv/templates/registry"
STAGING_DIR="${REPO_ROOT}/eng/semconv/out"

TS_DEST="${REPO_ROOT}/src/qyl.dashboard/src/lib/semconv.ts"
SQL_DEST="${REPO_ROOT}/src/qyl.collector/Storage/promoted-columns.g.sql"

if [ ! -x "${WEAVER_BIN}" ] || [ ! -d "${UPSTREAM_REGISTRY}" ]; then
  echo "Weaver or upstream registry missing." >&2
  echo "Run: ./eng/semconv/bootstrap-weaver.sh" >&2
  exit 1
fi

rm -rf "${STAGING_DIR}"
"${WEAVER_BIN}" registry generate \
  --registry "${UPSTREAM_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  qyl \
  "${STAGING_DIR}"

install -m 0644 "${STAGING_DIR}/semconv.ts"               "${TS_DEST}"
install -m 0644 "${STAGING_DIR}/promoted-columns.g.sql"   "${SQL_DEST}"

echo ""
echo "Wrote:"
echo "  ${TS_DEST} ($(wc -l < "${TS_DEST}") lines)"
echo "  ${SQL_DEST} ($(wc -l < "${SQL_DEST}") lines)"
