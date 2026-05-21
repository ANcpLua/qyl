#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"
eng/automation/arqio/run-corpus.sh research/arqio/corpus/targets.json
eng/automation/thesis/build-thesis.sh
eng/automation/thesis/check-thesis.sh research/thesis/thesis.tex reports/thesis-quality
