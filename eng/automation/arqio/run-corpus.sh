#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "usage: run-corpus.sh <targets.json> [run-dir]" >&2
  exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN_DIR="$(python3 "$SCRIPT_DIR/run-corpus.py" "$@")"
python3 "$SCRIPT_DIR/aggregate-results.py" "$RUN_DIR" >/dev/null
python3 "$SCRIPT_DIR/render-thesis-tables.py" "$RUN_DIR"
echo "$RUN_DIR"
