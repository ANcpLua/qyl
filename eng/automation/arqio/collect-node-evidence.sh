#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: collect-node-evidence.sh <repo-path> <artifact-dir>" >&2
  exit 2
fi

REPO_PATH="$1"
ARTIFACT_DIR="$2"
mkdir -p "$ARTIFACT_DIR"
cd "$REPO_PATH"
if [ -f package-lock.json ]; then npm ci > "$ARTIFACT_DIR/npm-install.log" 2>&1; else npm install > "$ARTIFACT_DIR/npm-install.log" 2>&1; fi
npm run build --if-present > "$ARTIFACT_DIR/npm-build.log" 2>&1
npm test --if-present -- --watch=false > "$ARTIFACT_DIR/npm-test.log" 2>&1
