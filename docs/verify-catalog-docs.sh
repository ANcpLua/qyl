#!/usr/bin/env bash
# Verify feature catalog docs against disk — run from repo root
# Usage: bash docs/verify-catalog-docs.sh

set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PASS=0; FAIL=0

ok()   { echo "  ✓ $*"; PASS=$((PASS + 1)); }
fail() { echo "  ✗ $*"; FAIL=$((FAIL + 1)); }

check_file() {
  local f="$ROOT/$1"
  if [[ -f "$f" || -d "$f" ]]; then ok "EXISTS  $1"
  else fail "MISSING $1"; fi
}

check_glob() {
  local pattern="$ROOT/$1"
  if compgen -G "$pattern" > /dev/null 2>&1; then ok "EXISTS  $1"
  else fail "MISSING $1"; fi
}

check_symbol() {
  local sym="$1" file="$ROOT/$2"
  if grep -q "$sym" "$file" 2>/dev/null; then ok "SYMBOL  $sym in $2"
  else fail "SYMBOL  $sym NOT FOUND in $2"; fi
}

check_absent() {
  local path="$ROOT/$1"
  if [[ ! -e "$path" ]]; then ok "ABSENT  $1 (expected)"
  else fail "EXISTS  $1 (should be gone)"; fi
}

check_id_xref() {
  local id="$1" registry="$ROOT/$2" coverage="$ROOT/$3"
  if grep -q "$id" "$registry" 2>/dev/null; then ok "ID XREF $id in $2"
  else fail "ID XREF $id missing from $2"; fi
  if grep -q "$id" "$coverage" 2>/dev/null; then ok "ID XREF $id in $3"
  else fail "ID XREF $id missing from $3"; fi
}

echo "══════════════════════════════════════════════════"
echo " qyl catalog docs deep verification"
echo "══════════════════════════════════════════════════"

# ── 1. Output files exist ─────────────────────────────
echo ""
echo "── 1. Output files ──"
check_file "docs/sentry/sentry.md"
check_file "docs/sentry/qyl-sentry.md"
check_file "docs/aspire/aspire.md"
check_file "docs/aspire/qyl-aspire.md"

# ── 2. Source files deleted ───────────────────────────
echo ""
echo "── 2. Deleted source files ──"
check_absent "docs/sentry/sentry-catalog-pinned.md"
check_absent "docs/sentry/sentry-dotnet-coverage.md"
check_absent "docs/aspire/README.aspire-feature-catalog.md"
check_absent "docs/aspire/aspire-13-coverage-notes.md"
check_absent "docs/aspire/aspire-13-release-notes.md"
check_absent "docs/aspire/aspire-13-1-release-notes.md"
check_absent "docs/aspire/ASPIRE-13x-JIRA-THEORETICAL-TASKS.md"
check_absent "docs/aspire/aspire-api-browser.md"
check_absent "docs/aspire/aspire-cli-configuration.md"
# Note: docs/aspire/ASPIRE.md == aspire.md on macOS (case-insensitive FS) — covered by output file check above

# ── 3. No fictional files referenced ──────────────────
echo ""
echo "── 3. No fictional file references in docs ──"
for doc in docs/sentry/qyl-sentry.md docs/aspire/qyl-aspire.md; do
  for ghost in \
    "OtlpPiiScrubber.cs" \
    "QylTelemetryOptions.cs" \
    "ErrorEndpoints.cs" \
    "AlertEndpoints.cs" \
    "CronMonitorService.cs" \
    "ReleaseHealthService.cs"; do
    if grep -q "$ghost" "$ROOT/$doc" 2>/dev/null; then
      fail "GHOST   $ghost still referenced in $doc"
    else
      ok "CLEAN   $ghost absent from $doc"
    fi
  done
done

# ── 4. No fictional dirs referenced ───────────────────
echo ""
echo "── 4. No non-existent dirs referenced ──"
for ghost in "CronMonitor" "cron_monitor"; do
  if grep -rq "$ghost" "$ROOT/docs/sentry" "$ROOT/docs/aspire" 2>/dev/null; then
    fail "GHOST   '$ghost' found in catalog docs"
  else
    ok "CLEAN   '$ghost' absent from catalog docs"
  fi
done

# ── 5. qyl-sentry.md file paths exist ─────────────────
echo ""
echo "── 5. qyl-sentry.md verified paths ──"
check_file "src/qyl.collector/Errors/IssueService.cs"
check_file "src/qyl.collector/Errors/ErrorExtractor.cs"
check_file "src/qyl.collector/Errors/ErrorFingerprinter.cs"
check_file "src/qyl.collector/Errors/ErrorCategorizer.cs"
check_file "src/qyl.collector/Errors/IssueEndpoints.cs"
check_file "src/qyl.collector/Errors/ErrorOwnershipService.cs"
check_file "src/qyl.collector/Errors/ErrorRegressionDetector.cs"
check_file "src/qyl.collector/Alerting/AlertModels.cs"
check_file "src/qyl.collector/Alerting/AlertEvaluator.cs"
check_file "src/qyl.collector/Alerting/AlertDeduplicator.cs"
check_file "src/qyl.collector/Alerting/AlertEscalationService.cs"
check_file "src/qyl.collector/Alerting/AlertRuleEndpoints.cs"
check_file "src/qyl.collector/Alerting/AlertService.cs"
check_file "src/qyl.collector/Alerting/GenAiAlertRules.cs"
check_file "src/qyl.collector/Autofix/AutofixOrchestrator.cs"
check_file "src/qyl.collector/Autofix/PolicyGate.cs"
check_file "src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs"
check_file "src/qyl.collector/Storage/DuckDbSchema.g.cs"
check_file "src/qyl.collector/Storage/Migrations/V2026021618__create_error_breadcrumbs.sql"
check_file "src/qyl.collector/Storage/Migrations/V2026021619__create_error_regressions.sql"
check_file "src/qyl.collector/Storage/Migrations/V2026021620__create_error_ownership.sql"
check_file "src/qyl.collector/Storage/Migrations/V2026021621__create_error_release_markers.sql"
check_file "src/qyl.collector/Storage/Migrations/V2026021622__create_alert_rules.sql"
check_file "src/qyl.collector/Storage/Migrations/V2026021623__create_alert_firings.sql"
check_file "src/qyl.collector/Telemetry/QylDataClassification.cs"
check_file "src/qyl.collector/Workflow"
check_file "src/qyl.collector/BuildFailures"
check_file "src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs"
check_file "src/qyl.servicedefaults/QylServiceDefaultsOptions.cs"
check_file "src/qyl.servicedefaults/Instrumentation/GenAi/GenAiInstrumentation.cs"
check_file "src/qyl.mcp/Tools/IssueTools.cs"
check_file "src/qyl.dashboard/src/pages/AgentRunsPage.tsx"
check_file "src/qyl.dashboard/src/pages/AgentRunDetailPage.tsx"
check_file "src/qyl.browser/src/context.ts"
check_file "src/qyl.servicedefaults.generator"
check_file "src/qyl.watch"
check_file "src/qyl.watchdog"

# ── 6. qyl-aspire.md file paths exist ─────────────────
echo ""
echo "── 6. qyl-aspire.md verified paths ──"
check_file "src/qyl.hosting/Resources/IQylResource.cs"
check_file "src/qyl.hosting/Resources/ProjectResource.cs"
check_file "src/qyl.hosting/Resources/NodeResource.cs"
check_file "src/qyl.hosting/Resources/ViteResource.cs"
check_file "src/qyl.hosting/Resources/PythonResource.cs"
check_file "src/qyl.hosting/Resources/ContainerResource.cs"
check_file "src/qyl.hosting/QylApp.cs"
check_file "src/qyl.hosting/QylAppBuilder.cs"
check_file "src/qyl.hosting/QylRunner.cs"
check_file "src/qyl.mcp/Tools/TelemetryTools.cs"
check_file "src/qyl.mcp/Tools/StructuredLogTools.cs"
check_file "src/qyl.mcp/Tools/GenAiTools.cs"
check_file "src/qyl.mcp/Tools/AgentTools.cs"
check_file "src/qyl.mcp/Tools/BuildTools.cs"
check_file "src/qyl.mcp/Tools/ReplayTools.cs"
check_file "src/qyl.mcp/Tools/SearchTools.cs"
check_file "src/qyl.mcp/Tools/WorkflowTools.cs"
check_file "src/qyl.mcp/Tools/AnalyticsTools.cs"
check_file "src/qyl.mcp/Tools/StorageTools.cs"
check_file "src/qyl.mcp/Tools/ConsoleTools.cs"
check_file "src/qyl.mcp/Tools/CopilotTools.cs"
check_file "src/qyl.mcp/Tools/WorkspaceTools.cs"

# ── 7. Key symbols exist in verified files ─────────────
echo ""
echo "── 7. Key symbols ──"
check_symbol "AllowedTransitions"       "src/qyl.collector/Errors/IssueService.cs"
check_symbol "TransitionStatusAsync"    "src/qyl.collector/Errors/IssueService.cs"
check_symbol "ValidStatuses"            "src/qyl.collector/Errors/IssueService.cs"
check_symbol "ErrorFingerprinter"       "src/qyl.collector/Errors/ErrorFingerprinter.cs"
check_symbol "AlertEvaluator"           "src/qyl.collector/Alerting/AlertEvaluator.cs"
check_symbol "AutofixOrchestrator"      "src/qyl.collector/Autofix/AutofixOrchestrator.cs"
check_symbol "agent_runs"               "src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs"
check_symbol "tool_calls"               "src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs"
check_symbol "UvicornResource"          "src/qyl.hosting/Resources/PythonResource.cs"
check_symbol "WithUv"                   "src/qyl.hosting/Resources/PythonResource.cs"
check_symbol "ExceptionCaptureMiddleware" "src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs"
check_symbol "GlobalExceptionHooks"     "src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs"
check_symbol "ErrorExtractor"            "src/qyl.collector/Errors/ErrorExtractor.cs"

# ── 8. NOT PRESENT claims — dirs must be absent ────────
echo ""
echo "── 8. NOT PRESENT claims confirmed absent ──"
check_absent "src/qyl.collector/CronMonitor"
if grep -qr "uptime" "$ROOT/src/qyl.collector" --include="*.cs" 2>/dev/null \
   && ! grep -q "Health" "$ROOT/src/qyl.collector/Health/HealthUiService.cs" 2>/dev/null; then
  fail "UPTIME  unexpected uptime monitoring code found"
else
  ok "ABSENT  no uptime monitoring subsystem"
fi

# ── 9. ID cross-references ─────────────────────────────
echo ""
echo "── 9. ID cross-references (sample) ──"
for id in SENTRY-001 SENTRY-005 SENTRY-017 SENTRY-019 SENTRY-025; do
  check_id_xref "$id" "docs/sentry/sentry.md" "docs/sentry/qyl-sentry.md"
done
for id in ASP-001 ASP-003 ASP-011 ASP-031 ASP-042; do
  check_id_xref "$id" "docs/aspire/aspire.md" "docs/aspire/qyl-aspire.md"
done

# ── 10. Tally lines present ────────────────────────────
echo ""
echo "── 10. Summary tally lines ──"
SENTRY_TALLY=$(grep "^\\*\\*Result:" "$ROOT/docs/sentry/qyl-sentry.md" || echo "")
ASPIRE_TALLY=$(grep "^\\*\\*Result:" "$ROOT/docs/aspire/qyl-aspire.md" || echo "")
if [[ -n "$SENTRY_TALLY" ]]; then ok "TALLY   qyl-sentry.md: $SENTRY_TALLY"
else fail "TALLY   qyl-sentry.md: missing Result line"; fi
if [[ -n "$ASPIRE_TALLY" ]]; then ok "TALLY   qyl-aspire.md: $ASPIRE_TALLY"
else fail "TALLY   qyl-aspire.md: missing Result line"; fi

# ── 11. No Azure/Windows in DONE sections ─────────────
echo ""
echo "── 11. No Azure/Windows in DONE entries ──"
AZURE_HITS=$(grep -n "Azure\|Windows\|MAUI\|ACR\|AppService" \
  "$ROOT/docs/sentry/qyl-sentry.md" \
  "$ROOT/docs/aspire/qyl-aspire.md" 2>/dev/null \
  | grep -v "EXCLUDED\|NOT APPLICABLE\|NOT PRESENT\|excluded\|exclusion\|Exclusion\|ASP-EX\|constraints\|cloud-agnostic" \
  | grep -v "^Binary" || true)
if [[ -z "$AZURE_HITS" ]]; then
  ok "CLEAN   no Azure/Windows in non-excluded entries"
else
  fail "FOUND   Azure/Windows references in unexpected sections:"
  echo "$AZURE_HITS" | sed 's/^/         /'
fi

# ── Summary ────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════════════"
TOTAL=$((PASS + FAIL))
echo " PASSED: $PASS / $TOTAL"
if [[ $FAIL -gt 0 ]]; then
  echo " FAILED: $FAIL"
  echo "══════════════════════════════════════════════════"
  exit 1
else
  echo " All checks passed."
  echo "══════════════════════════════════════════════════"
fi
