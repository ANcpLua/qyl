#!/usr/bin/env bash
# Native AOT runtime smoke for the collector: the executable owner of the QylAot publish lane.
#
# Publishes with -p:QylAot=true (unless SKIP_PUBLISH=1 and a binary exists), boots the native
# binary, ingests a real OTLP/JSON span, and reads it back through the product API — including
# the sessions surface, whose MIN(DISTINCT …) aggregates exercise DuckDB LIST(VARCHAR)
# materialization, the provider's most reflection-dependent read path under Native AOT.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/qyl.collector/release"
BINARY="$PUBLISH_DIR/qyl.collector"
PORT="${QYL_SMOKE_PORT:-5199}"
BASE="http://localhost:$PORT"
DB_DIR="$(mktemp -d)"
PUBLISH_LOG="$DB_DIR/publish.log"
COLLECTOR_PID=""
trap '[[ -n "$COLLECTOR_PID" ]] && kill "$COLLECTOR_PID" 2>/dev/null || true; rm -rf "$DB_DIR"' EXIT

# A stale listener on the port would answer the readiness probe and turn the whole smoke
# into a false PASS for a binary that never even started — refuse to run against one.
if curl -sf --max-time 2 "$BASE/health" >/dev/null 2>&1; then
  echo "[smoke] FAIL: something already listens on $BASE — pick another QYL_SMOKE_PORT or kill it"; exit 1
fi

if [[ "${SKIP_PUBLISH:-0}" != "1" || ! -x "$BINARY" ]]; then
  echo "[smoke] Publishing collector with QylAot=true..."
  dotnet publish "$REPO_ROOT/services/qyl.collector/qyl.collector.csproj" \
    -c Release -p:QylAot=true -p:QylEmbedDashboard=false 2>&1 | tee "$PUBLISH_LOG"

  IL_WARNING_COUNT="$(grep -Ec 'warning IL[0-9]{4}:' "$PUBLISH_LOG" || true)"
  EXPECTED_DUCKDB_IL2104="duckdb\.net\.data\.full[/\\\\]1\.5\.3[/\\\\]lib[/\\\\]net10\.0[/\\\\]DuckDB\.NET\.Data\.dll : warning IL2104: Assembly 'DuckDB\.NET\.Data' produced trim warnings"
  EXPECTED_DUCKDB_IL3053="duckdb\.net\.data\.full[/\\\\]1\.5\.3[/\\\\]lib[/\\\\]net10\.0[/\\\\]DuckDB\.NET\.Data\.dll : warning IL3053: Assembly 'DuckDB\.NET\.Data' produced AOT analysis warnings"
  UNEXPECTED_IL_WARNINGS="$(grep -E 'warning IL[0-9]{4}:' "$PUBLISH_LOG" | grep -Ev "(${EXPECTED_DUCKDB_IL2104}|${EXPECTED_DUCKDB_IL3053})" || true)"
  if [[ "$IL_WARNING_COUNT" != "2" ]] ||
     ! grep -Eq "$EXPECTED_DUCKDB_IL2104" "$PUBLISH_LOG" ||
     ! grep -Eq "$EXPECTED_DUCKDB_IL3053" "$PUBLISH_LOG" ||
     [[ -n "$UNEXPECTED_IL_WARNINGS" ]]; then
    echo "[smoke] FAIL: NativeAOT diagnostics changed; only the reviewed DuckDB.NET.Data 1.5.3 IL2104/IL3053 rollups are allowed"
    [[ -z "$UNEXPECTED_IL_WARNINGS" ]] || echo "$UNEXPECTED_IL_WARNINGS"
    exit 1
  fi
  echo "[smoke] NativeAOT diagnostics limited to reviewed DuckDB.NET.Data 1.5.3 rollups"
fi

file "$BINARY" | grep -qE "Mach-O|ELF" || { echo "[smoke] FAIL: $BINARY is not a native executable"; exit 1; }
! ls "$PUBLISH_DIR"/qyl.collector.dll 2>/dev/null || { echo "[smoke] FAIL: managed qyl.collector.dll beside the binary — JIT publish snuck in"; exit 1; }
# A self-contained single-file JIT bundle is also Mach-O/ELF with no .dll beside it, but its
# apphost loads hostfxr; an ILC-compiled binary never references it. Heuristic, but it
# distinguishes exactly the two publish shapes this gate can produce.
if strings "$BINARY" | grep -qE "hostfxr|DOTNET_ROOT_"; then
  echo "[smoke] FAIL: binary references hostfxr — this is a single-file JIT bundle, not Native AOT"; exit 1
fi
echo "[smoke] Native binary confirmed: $(file "$BINARY" | cut -d: -f2 | xargs)"

echo "[smoke] Starting native collector on :$PORT (db in $DB_DIR)..."
QYL_PORT="$PORT" QYL_OTLP_PORT=0 QYL_GRPC_PORT=0 QYL_DATA_PATH="$DB_DIR/smoke.duckdb" ASPNETCORE_ENVIRONMENT=Development \
  "$BINARY" >"$DB_DIR/collector.log" 2>&1 &
COLLECTOR_PID=$!

for ((attempt = 0; attempt < 60; attempt++)); do
  curl -sf "$BASE/health" >/dev/null 2>&1 && break
  kill -0 "$COLLECTOR_PID" 2>/dev/null || { echo "[smoke] FAIL: collector exited during startup"; tail -40 "$DB_DIR/collector.log"; exit 1; }
  sleep 0.5
done
curl -sf "$BASE/health" >/dev/null || { echo "[smoke] FAIL: /health never became ready"; tail -40 "$DB_DIR/collector.log"; exit 1; }
echo "[smoke] Health OK"

TRACE_ID="0af7651916cd43dd8448eb211c80319c"
SPAN_ID="b7ad6b7169203331"
NOW_NS="$(($(date +%s) * 1000000000))"
OTLP_PAYLOAD=$(cat <<JSON
{"resourceSpans":[{"resource":{"attributes":[
  {"key":"service.name","value":{"stringValue":"aot-smoke"}},
  {"key":"session.id","value":{"stringValue":"aot-smoke-session"}},
  {"key":"gen_ai.provider.name","value":{"stringValue":"anthropic"}}]},
 "scopeSpans":[{"scope":{"name":"aot-smoke"},"spans":[{
   "traceId":"$TRACE_ID","spanId":"$SPAN_ID",
   "name":"aot-smoke-span","kind":1,
   "startTimeUnixNano":"$NOW_NS","endTimeUnixNano":"$((NOW_NS + 1000000))",
   "attributes":[{"key":"gen_ai.request.model","value":{"stringValue":"claude-fable-5"}}],
   "status":{"code":1}}]}]}]}
JSON
)
curl -sf -X POST "$BASE/v1/traces" -H "Content-Type: application/json" -d "$OTLP_PAYLOAD" >/dev/null \
  || { echo "[smoke] FAIL: OTLP ingest rejected"; tail -20 "$DB_DIR/collector.log"; exit 1; }
echo "[smoke] OTLP span ingested"

sleep 1
TRACES=$(curl -sf "$BASE/api/v1/traces")
echo "$TRACES" | grep -q "$TRACE_ID" || { echo "[smoke] FAIL: ingested trace not in /api/v1/traces: $TRACES"; exit 1; }
echo "[smoke] Trace readback OK"

curl -sf "$BASE/api/v1/traces/$TRACE_ID" | grep -q "$SPAN_ID" || { echo "[smoke] FAIL: span not in trace detail"; exit 1; }
echo "[smoke] Span detail OK"

# Sessions aggregate → DuckDB LIST(VARCHAR) columns → the provider's generic list
# materialization under Native AOT. This is the most reflection-dependent DuckDB read shape qyl uses.
SESSIONS=$(curl -sf "$BASE/api/v1/sessions")
echo "$SESSIONS" | grep -q '"services":\["aot-smoke"\]' || { echo "[smoke] FAIL: services LIST aggregate missing: $SESSIONS"; exit 1; }
echo "$SESSIONS" | grep -q '"models_used":\["claude-fable-5"\]' || { echo "[smoke] FAIL: models_used LIST aggregate missing: $SESSIONS"; exit 1; }
echo "[smoke] Sessions (LIST materialization) OK"

echo "[smoke] PASS: native collector serves OTLP ingest + product API end to end"
