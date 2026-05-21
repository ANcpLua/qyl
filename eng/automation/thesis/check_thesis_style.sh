#!/usr/bin/env bash
set -o pipefail

DEFAULT_FILE="thesis.tex"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FILE="${1:-$DEFAULT_FILE}"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

if [ ! -f "$FILE" ]; then
  echo -e "${RED}Error:${NC} File '$FILE' not found."
  echo "Usage: $0 [filename.tex|filename.md|filename.txt]"
  exit 1
fi

if command -v python3 >/dev/null 2>&1; then PYTHON=python3; else PYTHON=python; fi

echo -e "${YELLOW}=== CSAM Thesis Style Gate ===${NC}"
echo -e "Target file: ${YELLOW}$FILE${NC}\n"

echo -e "${YELLOW}1. Lexical trap checker${NC}"
"$PYTHON" "$SCRIPT_DIR/tools/lexical_trap_checker.py" "$FILE"
LEX_EXIT=$?

echo -e "\n${YELLOW}2. Vale style check${NC}"
if command -v vale >/dev/null 2>&1; then
  vale --config "$SCRIPT_DIR/.vale.ini" "$FILE"
  VALE_EXIT=$?
else
  echo -e "${YELLOW}Vale not installed; skipped.${NC}"
  VALE_EXIT=0
fi

if [ "$LEX_EXIT" -eq 0 ] && [ "$VALE_EXIT" -eq 0 ]; then
  echo -e "\n${GREEN}Style gate passed.${NC}"
  exit 0
fi

echo -e "\n${RED}Style gate failed.${NC}"
exit 1
