#!/usr/bin/env bash
# Generate qyl's semconv outputs into the final src/ destinations via Weaver.
#
# Pinned inputs:  open-telemetry/semantic-conventions v1.40.0 (cloned by bootstrap)
# Output targets:
#   - services/qyl.dashboard/src/lib/semconv.ts          (TypeScript const keys)
#   - services/qyl.collector/Storage/promoted-columns.g.sql  (DuckDB columns)
#
# Still hand-maintained:
#   - packages/Qyl.Contracts/Attributes/*Attributes.cs   (facades with qyl extensions)
#
# Bootstrap once per clone: ./eng/semconv/bootstrap-weaver.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

UNAME_S="$(uname -s)"
UNAME_M="$(uname -m)"
case "${UNAME_S}:${UNAME_M}" in
  Darwin:arm64|Darwin:aarch64) WEAVER_ARCH="aarch64-apple-darwin" ;;
  Darwin:x86_64)               WEAVER_ARCH="x86_64-apple-darwin" ;;
  Linux:x86_64)                WEAVER_ARCH="x86_64-unknown-linux-gnu" ;;
  *) echo "Unsupported platform: ${UNAME_S}/${UNAME_M}" >&2; exit 1 ;;
esac

WEAVER_BIN="${REPO_ROOT}/.tools/weaver/weaver-${WEAVER_ARCH}/weaver"
UPSTREAM_REGISTRY="${REPO_ROOT}/.tools/semconv-upstream/model"
TEMPLATES_ROOT="${REPO_ROOT}/eng/semconv/templates/registry"
STAGING_DIR="${REPO_ROOT}/eng/semconv/out"

TS_DEST="${REPO_ROOT}/services/qyl.dashboard/src/lib/semconv.ts"
SQL_DEST="${REPO_ROOT}/services/qyl.collector/Storage/promoted-columns.g.sql"

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
