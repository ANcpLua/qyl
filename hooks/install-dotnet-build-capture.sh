#!/usr/bin/env bash
set -euo pipefail

HOOK_DIR="${HOME}/.claude/hooks/PostToolUse"
mkdir -p "${HOOK_DIR}"

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "${SRC_DIR}/dotnet-build-capture.sh" "${HOOK_DIR}/dotnet-build-capture.sh"
chmod +x "${HOOK_DIR}/dotnet-build-capture.sh"

echo "Installed hook at ${HOOK_DIR}/dotnet-build-capture.sh"
