#!/usr/bin/env bash
set -euo pipefail

THESIS_FILE="${1:-research/thesis/thesis.tex}"
OUT_DIR="${2:-reports/thesis-quality}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/check_thesis_style.sh" "$THESIS_FILE"
python3 "$SCRIPT_DIR/tools/thesis_feedback_gate.py" "$THESIS_FILE" --out "$OUT_DIR"
if command -v opa >/dev/null 2>&1; then
  opa eval -d "$SCRIPT_DIR/policies/bachelor_rubric_semantic_expanded.rego" -i "$OUT_DIR/rubric_input.generated.json" "data.bachelor.rubric.report"
fi
