#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: collect-dotnet-evidence.sh <solution-or-project> <artifact-dir>" >&2
  exit 2
fi

INPUT_PATH="$1"
ARTIFACT_DIR="$2"
mkdir -p "$ARTIFACT_DIR"
dotnet restore "$INPUT_PATH" > "$ARTIFACT_DIR/dotnet-restore.log" 2>&1
dotnet build "$INPUT_PATH" --no-restore > "$ARTIFACT_DIR/dotnet-build.log" 2>&1
dotnet test "$INPUT_PATH" --no-build --logger "trx" --results-directory "$ARTIFACT_DIR/TestResults" > "$ARTIFACT_DIR/dotnet-test.log" 2>&1
