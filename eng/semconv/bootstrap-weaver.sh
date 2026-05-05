#!/usr/bin/env bash
# Prepare the Weaver-based semconv pipeline.
#
# semconv-upstream: git submodule at .tools/semconv-upstream pinned to a semconv
#   tag. Initialize with `git submodule update --init .tools/semconv-upstream`
#   (or clone with `--recurse-submodules`). This script refuses to proceed if
#   the submodule is absent — never shell out to `git clone`.
# Weaver binary: downloaded per platform into .tools/weaver/ (gitignored).
#   CI caches this path via `actions/cache` keyed on this script's hash.
#
# Version bump:
#   cd .tools/semconv-upstream && git fetch && git checkout vX.Y.Z
#   cd ../.. && sed -i '' 's/semconv_version: "..*"/semconv_version: "X.Y.Z"/' \
#       eng/semconv/templates/registry/qyl/weaver.yaml
#   git add .tools/semconv-upstream eng/semconv/templates/registry/qyl/weaver.yaml
#   git commit -m "chore(semconv): bump to vX.Y.Z"

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_DIR="${REPO_ROOT}/.tools"
WEAVER_DIR="${TOOLS_DIR}/weaver"
UPSTREAM_DIR="${TOOLS_DIR}/semconv-upstream"
WEAVER_YAML="${REPO_ROOT}/eng/semconv/templates/registry/qyl/weaver.yaml"

WEAVER_VERSION="v0.23.0"
SEMCONV_VERSION="$(sed -n 's/^[[:space:]]*semconv_version:[[:space:]]*"\(.*\)"/\1/p' "${WEAVER_YAML}")"

if [ -z "${SEMCONV_VERSION}" ]; then
  echo "Could not read semconv_version from ${WEAVER_YAML}" >&2
  exit 1
fi

# ── semconv-upstream (submodule) ────────────────────────────────────────────

if [ ! -f "${UPSTREAM_DIR}/model/attributes/registry.yaml" ] && [ ! -d "${UPSTREAM_DIR}/model" ]; then
  echo "semconv-upstream submodule missing at ${UPSTREAM_DIR}" >&2
  echo "Run: git submodule update --init .tools/semconv-upstream" >&2
  exit 1
fi

# ── Weaver binary (downloaded, gitignored, CI-cached) ───────────────────────

UNAME_S="$(uname -s)"
UNAME_M="$(uname -m)"
case "${UNAME_S}:${UNAME_M}" in
  Darwin:arm64|Darwin:aarch64) WEAVER_ARCH="aarch64-apple-darwin" ;;
  Darwin:x86_64)               WEAVER_ARCH="x86_64-apple-darwin" ;;
  Linux:x86_64)                WEAVER_ARCH="x86_64-unknown-linux-gnu" ;;
  *) echo "Unsupported platform: ${UNAME_S}/${UNAME_M}" >&2; exit 1 ;;
esac

WEAVER_BIN="${WEAVER_DIR}/weaver-${WEAVER_ARCH}/weaver"

mkdir -p "${WEAVER_DIR}"
if [ ! -x "${WEAVER_BIN}" ]; then
  echo "Downloading Weaver ${WEAVER_VERSION} (${WEAVER_ARCH})..."
  curl -sL \
    "https://github.com/open-telemetry/weaver/releases/download/${WEAVER_VERSION}/weaver-${WEAVER_ARCH}.tar.xz" \
    -o "${WEAVER_DIR}/weaver.tar.xz"
  tar -xf "${WEAVER_DIR}/weaver.tar.xz" -C "${WEAVER_DIR}"
  rm "${WEAVER_DIR}/weaver.tar.xz"
fi

echo ""
echo "Weaver:   ${WEAVER_BIN} ($("${WEAVER_BIN}" --version))"
echo "Upstream: ${UPSTREAM_DIR} (semconv v${SEMCONV_VERSION})"
echo ""
echo "Next: ./eng/semconv/run-weaver.sh"
