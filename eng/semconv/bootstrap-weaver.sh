#!/usr/bin/env bash
# One-time setup for the Weaver-based semconv pipeline.
# Downloads the Weaver CLI and clones the upstream semconv v1.40.0 registry.
# Artifacts land under .tools/ (gitignored).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_DIR="${REPO_ROOT}/.tools"
WEAVER_DIR="${TOOLS_DIR}/weaver"
UPSTREAM_DIR="${TOOLS_DIR}/semconv-upstream"

WEAVER_VERSION="v0.22.1"
SEMCONV_TAG="v1.40.0"

UNAME_S="$(uname -s)"
UNAME_M="$(uname -m)"
case "${UNAME_S}:${UNAME_M}" in
  Darwin:arm64|Darwin:aarch64) WEAVER_ARCH="aarch64-apple-darwin" ;;
  Darwin:x86_64)               WEAVER_ARCH="x86_64-apple-darwin" ;;
  Linux:x86_64)                WEAVER_ARCH="x86_64-unknown-linux-gnu" ;;
  *) echo "Unsupported platform: ${UNAME_S}/${UNAME_M}" >&2; exit 1 ;;
esac

mkdir -p "${WEAVER_DIR}"
if [ ! -x "${WEAVER_DIR}/weaver-${WEAVER_ARCH}/weaver" ]; then
  echo "Downloading Weaver ${WEAVER_VERSION} (${WEAVER_ARCH})..."
  curl -sL \
    "https://github.com/open-telemetry/weaver/releases/download/${WEAVER_VERSION}/weaver-${WEAVER_ARCH}.tar.xz" \
    -o "${WEAVER_DIR}/weaver.tar.xz"
  tar -xf "${WEAVER_DIR}/weaver.tar.xz" -C "${WEAVER_DIR}"
  rm "${WEAVER_DIR}/weaver.tar.xz"
fi

if [ ! -d "${UPSTREAM_DIR}" ]; then
  echo "Cloning open-telemetry/semantic-conventions@${SEMCONV_TAG}..."
  git clone --depth 1 --branch "${SEMCONV_TAG}" \
    https://github.com/open-telemetry/semantic-conventions.git "${UPSTREAM_DIR}"
fi

echo ""
echo "Weaver:   ${WEAVER_DIR}/weaver-${WEAVER_ARCH}/weaver ($(${WEAVER_DIR}/weaver-${WEAVER_ARCH}/weaver --version))"
echo "Upstream: ${UPSTREAM_DIR} (semconv ${SEMCONV_TAG})"
echo ""
echo "Next: ./eng/semconv/run-weaver.sh"
