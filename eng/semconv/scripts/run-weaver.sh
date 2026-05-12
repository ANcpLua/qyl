#!/usr/bin/env bash
# Generate qyl's semconv outputs into their final destinations via Weaver.
#
# Assumes bootstrap already installed Weaver into .tools/weaver/ and initialized
# the pinned upstream OpenTelemetry semantic-conventions submodule at
# .tools/semconv-upstream.
#
# Pass 1: upstream OTel registry outputs.
# Pass 2: qyl-owned attribute registry outputs.
#
# TypeSpec consts for upstream OTel attribute keys are no longer generated here —
# they ship via the `@o-ancpplua/otel-conventions-api` npm package
# (`@o-ancpplua/otel-conventions-api/generated/otel-keys`). The producer
# (upstream-semconv -> TypeSpec) lives in ANcpLua/typespec-otel-semconv.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

UNAME_S="$(uname -s)"
UNAME_M="$(uname -m)"
case "${UNAME_S}:${UNAME_M}" in
  Darwin:arm64|Darwin:aarch64) WEAVER_ARCH="aarch64-apple-darwin" ;;
  Darwin:x86_64)               WEAVER_ARCH="x86_64-apple-darwin" ;;
  Linux:x86_64)                WEAVER_ARCH="x86_64-unknown-linux-gnu" ;;
  Linux:arm64|Linux:aarch64)   WEAVER_ARCH="aarch64-unknown-linux-gnu" ;;
  *) echo "Unsupported platform: ${UNAME_S}/${UNAME_M}" >&2; exit 1 ;;
esac

WEAVER_BIN="${REPO_ROOT}/.tools/weaver/weaver-${WEAVER_ARCH}/weaver"
UPSTREAM_REGISTRY="${REPO_ROOT}/.tools/semconv-upstream/model"
QYL_REGISTRY="${REPO_ROOT}/eng/semconv/model/qyl"
TEMPLATES_ROOT="${REPO_ROOT}/eng/semconv/templates/registry"
# Override with SEMCONV_STAGING_DIR for non-default workspace layouts (for example CI).
# Reject values that could make the cleanup below target outside the intended tree.
STAGING_DIR="${SEMCONV_STAGING_DIR-${REPO_ROOT}/Artifacts/semconv}"
case "${STAGING_DIR}" in
  ""|"/")
    echo "ERROR: STAGING_DIR must be a non-empty absolute path other than '/'" >&2
    echo "       Got: '${STAGING_DIR}' (set via SEMCONV_STAGING_DIR=${SEMCONV_STAGING_DIR-<unset>})" >&2
    exit 1 ;;
  /*) ;;
  *)
    echo "ERROR: STAGING_DIR must be absolute (start with '/')" >&2
    echo "       Got: '${STAGING_DIR}' (set via SEMCONV_STAGING_DIR=${SEMCONV_STAGING_DIR-<unset>})" >&2
    exit 1 ;;
esac

TS_DEST="${REPO_ROOT}/services/qyl.dashboard/src/lib/semconv.ts"
SQL_DEST="${REPO_ROOT}/services/qyl.collector/Storage/promoted-columns.g.sql"
REGISTRY_DEST="${REPO_ROOT}/core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json"
CS_DEST="${REPO_ROOT}/packages/Qyl.Telemetry/Conventions/Qyl.g.cs"
CONVENTIONS_TS_DEST="${REPO_ROOT}/packages/qyl-client/src/conventions.ts"
DOCS_DEST="${REPO_ROOT}/docs/attributes"

if [ ! -x "${WEAVER_BIN}" ] || [ ! -d "${UPSTREAM_REGISTRY}" ]; then
  echo "Weaver or upstream registry missing." >&2
  echo "Run: ./eng/semconv/bootstrap-weaver.sh (or .ps1 on Windows)" >&2
  exit 1
fi

STAGING_UPSTREAM="${STAGING_DIR}/upstream"
rm -rf "${STAGING_UPSTREAM}"
"${WEAVER_BIN}" registry generate \
  --registry "${UPSTREAM_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  qyl \
  "${STAGING_UPSTREAM}"

install -m 0644 "${STAGING_UPSTREAM}/semconv.ts" "${TS_DEST}"
install -m 0644 "${STAGING_UPSTREAM}/promoted-columns.g.sql" "${SQL_DEST}"
mkdir -p "$(dirname "${REGISTRY_DEST}")"
install -m 0644 "${STAGING_UPSTREAM}/otel-attribute-registry.json" "${REGISTRY_DEST}"

STAGING_QYL="${STAGING_DIR}/qyl"
rm -rf "${STAGING_QYL}"
"${WEAVER_BIN}" registry generate \
  --registry "${QYL_REGISTRY}" \
  --templates "${TEMPLATES_ROOT}" \
  qyl \
  "${STAGING_QYL}"

mkdir -p "$(dirname "${CS_DEST}")"
install -m 0644 "${STAGING_QYL}/Qyl.g.cs" "${CS_DEST}"

mkdir -p "$(dirname "${CONVENTIONS_TS_DEST}")"
install -m 0644 "${STAGING_QYL}/conventions.ts" "${CONVENTIONS_TS_DEST}"

mkdir -p "${DOCS_DEST}"
[ -f "${STAGING_QYL}/qyl.attrs.md" ] && install -m 0644 "${STAGING_QYL}/qyl.attrs.md" "${DOCS_DEST}/qyl.attrs.md"

echo ""
echo "Wrote (upstream OTel pass):"
echo "  ${TS_DEST} ($(wc -l < "${TS_DEST}") lines)"
echo "  ${SQL_DEST} ($(wc -l < "${SQL_DEST}") lines)"
echo "  ${REGISTRY_DEST} ($(jq 'length' "${REGISTRY_DEST}" 2>/dev/null || echo '?') attributes)"
echo ""
echo "Wrote (qyl attrs pass):"
echo "  ${CS_DEST} ($(wc -l < "${CS_DEST}") lines)"
echo "  ${CONVENTIONS_TS_DEST} ($(wc -l < "${CONVENTIONS_TS_DEST}") lines)"
echo "  ${DOCS_DEST}/qyl.attrs.md (exists: $([ -f "${DOCS_DEST}/qyl.attrs.md" ] && echo yes || echo no))"
echo ""
echo "TypeSpec consts for upstream OTel attribute keys are no longer generated locally."
echo "Consumers import @o-ancpplua/otel-conventions-api/generated/otel-keys."
