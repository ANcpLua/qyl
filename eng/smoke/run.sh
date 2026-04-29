#!/usr/bin/env bash
# PRD #173 smoke gate — pre-commit/pre-push validation that the cost,
# activity-tracking, conversations, and inventory wiring all work end-to-end
# against a live qyl stack and a local Ollama.
#
# Designed to run from any agent CLI (Claude Code, Codex, aider, Gemini CLI):
#   - exits 0 on green, non-zero on any miss
#   - dumps the offending JSON on failure so the agent can self-correct
#   - idempotent: rerunnable without teardown
#
# Defaults assume OrbStack/Docker on macOS with `ollama` and the qyl
# compose stack reachable at the standard ports.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

# ── Configurable knobs ──────────────────────────────────────────────────────
OLLAMA_CONTAINER="${OLLAMA_CONTAINER:-qyl-smoke-ollama}"
OLLAMA_IMAGE="${OLLAMA_IMAGE:-ollama/ollama:latest}"
OLLAMA_PORT="${OLLAMA_PORT:-11434}"
OLLAMA_MODEL="${OLLAMA_MODEL:-qwen2.5:0.5b}"
COLLECTOR_URL="${QYL_COLLECTOR_URL:-http://localhost:5100}"
OTLP_ENDPOINT="${OTEL_EXPORTER_OTLP_ENDPOINT:-http://localhost:4318}"
LLM_BASE_URL="${QYL_LLM_BASE_URL:-http://localhost:${OLLAMA_PORT}/v1}"
TURNS="${QYL_SMOKE_TURNS:-2}"
CONVERSATION_ID="${QYL_SMOKE_CONVERSATION_ID:-smoke:$(date -u +%Y%m%d%H%M%S)}"

bold()  { printf '\033[1m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*" >&2; }
fail()  { red "✘ $*"; exit 1; }

require() {
  command -v "$1" >/dev/null 2>&1 || fail "missing dependency: $1"
}

# ── 0. Pre-flight ───────────────────────────────────────────────────────────
bold "── pre-flight"
require docker
require dotnet
require curl
require jq

docker info >/dev/null 2>&1 || fail "docker daemon not reachable (start OrbStack)"

# ── 1. Ollama up + model pulled ─────────────────────────────────────────────
bold "── ollama"
if ! docker ps --format '{{.Names}}' | grep -qx "$OLLAMA_CONTAINER"; then
  if docker ps -a --format '{{.Names}}' | grep -qx "$OLLAMA_CONTAINER"; then
    docker start "$OLLAMA_CONTAINER" >/dev/null
  else
    docker run -d \
      --name "$OLLAMA_CONTAINER" \
      --restart unless-stopped \
      -v "${OLLAMA_CONTAINER}-data:/root/.ollama" \
      -p "${OLLAMA_PORT}:11434" \
      "$OLLAMA_IMAGE" >/dev/null
  fi
fi

# Wait for the API to become responsive.
for _ in $(seq 1 30); do
  if curl -fs "http://localhost:${OLLAMA_PORT}/api/version" >/dev/null 2>&1; then break; fi
  sleep 1
done
curl -fs "http://localhost:${OLLAMA_PORT}/api/version" >/dev/null \
  || fail "ollama did not become reachable on :${OLLAMA_PORT}"

if ! docker exec "$OLLAMA_CONTAINER" ollama list 2>/dev/null | awk 'NR>1 {print $1}' | grep -qx "$OLLAMA_MODEL"; then
  echo "  pulling $OLLAMA_MODEL (one-time, ~400MB for qwen2.5:0.5b)…"
  docker exec "$OLLAMA_CONTAINER" ollama pull "$OLLAMA_MODEL"
fi
green "  ollama ready @ :${OLLAMA_PORT}, model $OLLAMA_MODEL"

# ── 2. qyl compose stack ────────────────────────────────────────────────────
bold "── qyl stack"
docker compose -f eng/compose.yaml up -d --remove-orphans >/dev/null

for _ in $(seq 1 60); do
  if curl -fs "${COLLECTOR_URL}/health" >/dev/null 2>&1; then break; fi
  sleep 1
done
curl -fs "${COLLECTOR_URL}/health" >/dev/null \
  || fail "qyl.collector did not become healthy at ${COLLECTOR_URL}"
green "  qyl.collector healthy @ ${COLLECTOR_URL}"

# ── 3. Run the smoke producer ───────────────────────────────────────────────
bold "── smoke producer"
QYL_LLM_BASE_URL="$LLM_BASE_URL" \
QYL_LLM_MODEL="$OLLAMA_MODEL" \
QYL_LLM_API_KEY="ollama" \
QYL_SMOKE_CONVERSATION_ID="$CONVERSATION_ID" \
QYL_SMOKE_TURNS="$TURNS" \
OTEL_EXPORTER_OTLP_ENDPOINT="$OTLP_ENDPOINT" \
  dotnet run --project eng/smoke/qyl.smoke -c Release --nologo

# Give the collector a moment to ingest the OTLP batch.
sleep 2

# ── 4. Assert ───────────────────────────────────────────────────────────────
bold "── assertions"

CONV_JSON="$(curl -fs "${COLLECTOR_URL}/api/v1/conversations/${CONVERSATION_ID}")" \
  || fail "GET /api/v1/conversations/${CONVERSATION_ID} failed"

SPAN_COUNT="$(jq -r '.spanCount // 0' <<<"$CONV_JSON")"
[ "$SPAN_COUNT" -ge 1 ] || { echo "$CONV_JSON" | jq .; fail "expected ≥1 span in conversation, got $SPAN_COUNT"; }

COST_SPANS="$(jq -r '[.spans[] | select(.costUsd != null)] | length' <<<"$CONV_JSON")"
[ "$COST_SPANS" -ge 1 ] || { echo "$CONV_JSON" | jq '.spans[] | {name, requestModel, costUsd}'; fail "QylGenAiCostProcessor did not emit cost on any span"; }

CAPTURE_FLAGS="$(jq -r '.captureFlags' <<<"$CONV_JSON")"
[ "$CAPTURE_FLAGS" != "null" ] || fail "captureFlags missing from conversation detail"

INV_JSON="$(curl -fs "${COLLECTOR_URL}/qyl/inventory/agents")" \
  || fail "GET /qyl/inventory/agents failed (auth gate? non-dev?)"

SMOKE_AGENT="$(jq '.items[] | select(.name == "SmokeAgent")' <<<"$INV_JSON")"
[ -n "$SMOKE_AGENT" ] || { echo "$INV_JSON" | jq '.items | map(.name)'; fail "SmokeAgent missing from inventory"; }

LAST_SEEN="$(jq -r '.lastSeenUtc // empty' <<<"$SMOKE_AGENT")"
[ -n "$LAST_SEEN" ] || { echo "$SMOKE_AGENT" | jq .; fail "lastSeenUtc not populated — QylAgentActivityProcessor missed the span"; }

CALLS_24H="$(jq -r '.callCount24h // 0' <<<"$SMOKE_AGENT")"
[ "$CALLS_24H" -ge 1 ] || { echo "$SMOKE_AGENT" | jq .; fail "callCount24h is 0 — activity tracking did not increment"; }

green "✔ conversation ${CONVERSATION_ID}: ${SPAN_COUNT} spans, ${COST_SPANS} with cost"
green "✔ inventory: SmokeAgent last_seen=${LAST_SEEN} calls_24h=${CALLS_24H}"
echo
bold "PASS — PRD #173 quality gate green"
echo "Open http://localhost:5100/conversations?sessionId=${CONVERSATION_ID}"
echo "      http://localhost:5100/agents"
