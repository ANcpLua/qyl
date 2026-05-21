#!/usr/bin/env bash
set -uo pipefail

RUN_DIR="${1:-research/arqio/results/manual-semconv}"
ARTIFACT_DIR="$RUN_DIR/semconv"
mkdir -p "$ARTIFACT_DIR"

run_step() {
  name="$1"
  shift
  "$@" > "$ARTIFACT_DIR/$name.log" 2>&1
  code=$?
  echo "$code" > "$ARTIFACT_DIR/$name.exit"
  return 0
}

if [ -x /Users/ancplua/RiderProjects/semconv-testbed/scripts/verify-semconv.sh ]; then
  run_step semconv-testbed bash /Users/ancplua/RiderProjects/semconv-testbed/scripts/verify-semconv.sh
else
  run_step semconv-testbed-missing false
fi

run_step qyl-semconv-stable dotnet build packages/Qyl.OpenTelemetry.SemanticConventions/Qyl.OpenTelemetry.SemanticConventions.csproj
run_step qyl-semconv-incubating dotnet build packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Qyl.OpenTelemetry.SemanticConventions.Incubating.csproj

if [ -d packages/Qyl.SemanticConventions ]; then
  run_step qyl-semconv-package dotnet build packages/Qyl.SemanticConventions/Qyl.SemanticConventions.csproj
fi

python3 "$(dirname "${BASH_SOURCE[0]}")/semconv-summary.py" "$ARTIFACT_DIR" "$RUN_DIR/semconv-summary.json"
