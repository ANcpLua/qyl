#!/usr/bin/env bash
set -euo pipefail

# Installs the toolchain mise.toml pins on a macOS machine: mise itself through Homebrew, then
# each repository's tools. Fallout cannot do this step because it runs on the toolchain it would
# install. Afterwards `mise run check` is the local gate.
# Usage: eng/scripts/toolchain-macos.sh [repository ...]   (default: this repository)

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if ! command -v mise >/dev/null 2>&1; then
  brew install mise
fi

for repository in "${@:-$REPO_ROOT}"; do
  (cd "$repository" && mise trust --yes && mise install)
done
