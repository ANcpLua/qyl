#!/usr/bin/env bash
# Full-surface matrix runner: builds qyl.semconv.smoke.matrix under every TFM
# in the project's <TargetFrameworks>. Reports pass/fail per TFM so a
# portability regression is greppable in CI output.
#
# Exit code is 0 iff every TFM listed below builds clean. A non-zero count is
# printed so the failure mode is visible in build logs without re-reading
# scrollback.
#
# Run from anywhere — script cd's to its own directory first.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

TFMS=(net472 netstandard2.0 net6.0 net8.0 net9.0 net10.0)
PROJECT=qyl.semconv.smoke.matrix.csproj

pass=0
fail=0
failed_tfms=()

for tfm in "${TFMS[@]}"; do
    echo "=== Building $tfm ==="
    if dotnet build "$PROJECT" -c Release -f "$tfm" --nologo 2>&1 | tail -5; then
        # `dotnet build` returns 0 even when "Build succeeded" sits next to
        # nonzero error counts in some odd hook configurations; re-check the
        # exit code of the pipeline below explicitly via PIPESTATUS.
        if [ "${PIPESTATUS[0]}" -eq 0 ]; then
            pass=$((pass+1))
            echo "    -> $tfm OK"
        else
            fail=$((fail+1))
            failed_tfms+=("$tfm")
            echo "    -> $tfm FAILED (exit ${PIPESTATUS[0]})"
        fi
    else
        fail=$((fail+1))
        failed_tfms+=("$tfm")
        echo "    -> $tfm FAILED"
    fi
done

echo
echo "=== Matrix summary ==="
echo "pass=$pass fail=$fail of ${#TFMS[@]}"
if [ "$fail" -gt 0 ]; then
    echo "failed: ${failed_tfms[*]}"
    exit 1
fi
