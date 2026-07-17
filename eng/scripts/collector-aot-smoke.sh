#!/usr/bin/env bash
# Native AOT deployment-image smoke for the collector.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HOST_PORT="${QYL_SMOKE_PORT:-5199}"
CONTAINER_PORT="${QYL_SMOKE_CONTAINER_PORT:-5119}"
BASE="http://localhost:$HOST_PORT"
SMOKE_PLATFORM="${QYL_SMOKE_PLATFORM:-linux/amd64}"
SMOKE_ID="$$-${RANDOM}"
IMAGE_NAME="qyl-collector-aot-smoke:$SMOKE_ID"
CONTAINER_NAME="qyl-collector-aot-smoke-$SMOKE_ID"
VOLUME_NAME="qyl-collector-aot-smoke-$SMOKE_ID"

cleanup() {
  local status=$?
  set +e

  if ((status != 0)) && docker container inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
    echo "[smoke] Docker container state:"
    docker container inspect --format '{{json .State}}' "$CONTAINER_NAME" 2>&1
    echo "[smoke] Collector container logs:"
    docker logs "$CONTAINER_NAME" 2>&1
  fi

  docker container rm --force "$CONTAINER_NAME" >/dev/null 2>&1
  docker image rm --force "$IMAGE_NAME" >/dev/null 2>&1
  docker volume rm --force "$VOLUME_NAME" >/dev/null 2>&1
  exit "$status"
}
trap cleanup EXIT

fail() {
  echo "[smoke] FAIL: $*" >&2
  exit 1
}

wait_for_ready() {
  local health_status="starting"
  local running

  for ((attempt = 0; attempt < 120; attempt++)); do
    running="$(docker container inspect --format '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null)" \
      || fail "collector container disappeared during startup"
    [[ "$running" == "true" ]] || fail "collector container exited during startup"

    health_status="$(docker container inspect --format '{{.State.Health.Status}}' "$CONTAINER_NAME" 2>/dev/null)" \
      || fail "collector Docker health state is unavailable"
    if [[ "$health_status" == "healthy" ]] \
      && curl -fsS --max-time 2 "$BASE/health" >/dev/null 2>&1; then
      echo "[smoke] Docker HEALTHCHECK + HTTP health OK"
      return
    fi

    sleep 0.5
  done

  fail "/health never became ready (Docker health: $health_status)"
}

assert_runtime_identity() {
  local data_owner_uid
  local pid_gid
  local pid_uid
  local qyl_gid
  local qyl_uid

  pid_uid="$(docker exec "$CONTAINER_NAME" awk '/^Uid:/ { print $2; exit }' /proc/1/status)"
  pid_gid="$(docker exec "$CONTAINER_NAME" awk '/^Gid:/ { print $2; exit }' /proc/1/status)"
  qyl_uid="$(docker exec "$CONTAINER_NAME" id -u qyl)"
  qyl_gid="$(docker exec "$CONTAINER_NAME" id -g qyl)"
  [[ "$pid_uid" != "0" && "$pid_uid" == "$qyl_uid" ]] \
    || fail "collector PID 1 UID is $pid_uid, expected qyl UID $qyl_uid"
  [[ "$pid_gid" == "$qyl_gid" ]] \
    || fail "collector PID 1 GID is $pid_gid, expected qyl GID $qyl_gid"

  data_owner_uid="$(docker exec "$CONTAINER_NAME" stat -c '%u' /data)"
  [[ "$data_owner_uid" == "$qyl_uid" ]] \
    || fail "/data owner UID is $data_owner_uid, expected qyl UID $qyl_uid"
  echo "[smoke] PID 1 runs as qyl and owns /data"
}

stop_cleanly() {
  local phase="$1"
  local exit_code
  local running

  docker stop --time 60 "$CONTAINER_NAME" >/dev/null \
    || fail "$phase docker stop failed"
  running="$(docker container inspect --format '{{.State.Running}}' "$CONTAINER_NAME")"
  exit_code="$(docker container inspect --format '{{.State.ExitCode}}' "$CONTAINER_NAME")"
  [[ "$running" == "false" ]] || fail "$phase left the collector running"
  [[ "$exit_code" == "0" ]] || fail "$phase exited with status $exit_code"
  echo "[smoke] $phase completed with exit status 0"
}

# A stale listener would answer the readiness probe and turn the smoke into a false pass.
if curl -fsS --max-time 2 "$BASE/health" >/dev/null 2>&1; then
  fail "something already listens on $BASE; pick another QYL_SMOKE_PORT or stop it"
fi

echo "[smoke] Building collector deployment image..."
docker build \
  --progress=plain \
  --no-cache-filter build \
  --platform "$SMOKE_PLATFORM" \
  --file "$REPO_ROOT/services/qyl.collector/Dockerfile" \
  --tag "$IMAGE_NAME" \
  "$REPO_ROOT"

docker volume create "$VOLUME_NAME" >/dev/null
docker run --rm \
  --platform "$SMOKE_PLATFORM" \
  --entrypoint /bin/sh \
  --mount "type=volume,source=$VOLUME_NAME,target=/data" \
  "$IMAGE_NAME" \
  -c 'chown 0:0 /data && chmod 0755 /data && test "$(stat -c "%u:%g:%a" /data)" = "0:0:755"'
echo "[smoke] Root-owned 0755 deployment volume initialized"

echo "[smoke] Starting collector image on host :$HOST_PORT, container :$CONTAINER_PORT..."
docker run --detach \
  --platform "$SMOKE_PLATFORM" \
  --name "$CONTAINER_NAME" \
  --publish "127.0.0.1:$HOST_PORT:$CONTAINER_PORT" \
  --mount "type=volume,source=$VOLUME_NAME,target=/data" \
  --env "PORT=$CONTAINER_PORT" \
  --env QYL_OTLP_AUTH_MODE=Unsecured \
  --env QYL_GRPC_PORT=0 \
  --env QYL_OTLP_PORT=0 \
  "$IMAGE_NAME" >/dev/null

wait_for_ready
assert_runtime_identity

DASHBOARD="$(curl -fsS --max-time 5 "$BASE/")" || fail "embedded dashboard request failed"
grep -q '<title>QYL Dashboard</title>' <<<"$DASHBOARD" \
  || fail "/ did not serve the embedded dashboard"
echo "[smoke] Embedded dashboard OK"

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
curl -fsS --max-time 5 -X POST "$BASE/v1/traces" \
  -H "Content-Type: application/json" \
  -d "$OTLP_PAYLOAD" >/dev/null \
  || fail "OTLP ingest rejected"
echo "[smoke] OTLP span ingested"

sleep 1
TRACES="$(curl -fsS --max-time 5 "$BASE/api/v1/traces")" || fail "trace list request failed"
grep -q "$TRACE_ID" <<<"$TRACES" \
  || fail "ingested trace not in /api/v1/traces: $TRACES"
echo "[smoke] Trace readback OK"

TRACE_DETAIL="$(curl -fsS --max-time 5 "$BASE/api/v1/traces/$TRACE_ID")" \
  || fail "trace detail request failed"
grep -q "$SPAN_ID" <<<"$TRACE_DETAIL" || fail "span not in trace detail"
echo "[smoke] Span detail OK"

# This aggregate exercises DuckDB LIST(VARCHAR) materialization under Native AOT.
SESSIONS="$(curl -fsS --max-time 5 "$BASE/api/v1/sessions")" || fail "sessions request failed"
grep -q '"services":\["aot-smoke"\]' <<<"$SESSIONS" \
  || fail "services LIST aggregate missing: $SESSIONS"
grep -q '"models_used":\["claude-fable-5"\]' <<<"$SESSIONS" \
  || fail "models_used LIST aggregate missing: $SESSIONS"
docker exec "$CONTAINER_NAME" test -s /data/qyl.duckdb \
  || fail "collector did not persist its DuckDB file on /data"
echo "[smoke] Sessions (LIST materialization) + volume persistence OK"

stop_cleanly "Initial graceful stop"

docker start "$CONTAINER_NAME" >/dev/null
wait_for_ready
assert_runtime_identity

TRACES="$(curl -fsS --max-time 5 "$BASE/api/v1/traces")" || fail "trace list after restart failed"
grep -q "$TRACE_ID" <<<"$TRACES" \
  || fail "persisted trace missing after restart: $TRACES"
TRACE_DETAIL="$(curl -fsS --max-time 5 "$BASE/api/v1/traces/$TRACE_ID")" \
  || fail "trace detail after restart failed"
grep -q "$SPAN_ID" <<<"$TRACE_DETAIL" || fail "persisted span missing after restart"
echo "[smoke] Existing trace persisted across container restart"

stop_cleanly "Final graceful stop"
echo "[smoke] PASS: non-root deployment image persists data and shuts down cleanly"
