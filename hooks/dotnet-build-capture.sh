#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   dotnet-build-capture.sh "dotnet build ..." <exit_code>
# Intended for Claude PostToolUse hooks. If command failed, re-run with binlog capture,
# then send metadata to qyl.collector and fall back to local spool.

COMMAND_LINE="${1:-}"
EXIT_CODE="${2:-0}"

if [[ "${EXIT_CODE}" == "0" ]]; then
  exit 0
fi

if [[ -z "${COMMAND_LINE}" ]]; then
  exit 0
fi

if [[ "${COMMAND_LINE}" != dotnet\ build* && "${COMMAND_LINE}" != dotnet\ test* ]]; then
  exit 0
fi

ROOT_DIR="${PWD}"
BINLOG_DIR="${QYL_BINLOG_DIR:-${ROOT_DIR}/.qyl/binlogs}"
mkdir -p "${BINLOG_DIR}"

STAMP="$(date -u +%Y%m%d-%H%M%S)"
BINLOG_PATH="${BINLOG_DIR}/${STAMP}.binlog"

RERUN_CMD="${COMMAND_LINE} -bl:${BINLOG_PATH}"
MSBuildLogPropertyTracking=15 bash -lc "${RERUN_CMD}" >/dev/null 2>&1 || true

TARGET="build"
if [[ "${COMMAND_LINE}" == dotnet\ test* ]]; then
  TARGET="test"
fi

SUMMARY="Build command failed: ${COMMAND_LINE}"
if [[ ! -f "${BINLOG_PATH}" ]]; then
  SUMMARY="Build command failed and binlog capture did not produce a file"
fi

TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
PAYLOAD="$(cat <<JSON
{"timestamp":"${TIMESTAMP}","target":"${TARGET}","exitCode":${EXIT_CODE},"binlogPath":"${BINLOG_PATH}","errorSummary":"${SUMMARY}"}
JSON
)"

COLLECTOR_URL="${QYL_COLLECTOR_URL:-http://localhost:5100}"
TOKEN="${QYL_TOKEN:-}"
AUTH_HEADER=()
if [[ -n "${TOKEN}" ]]; then
  AUTH_HEADER=(-H "x-qyl-token: ${TOKEN}")
fi

if ! curl -fsS -X POST "${COLLECTOR_URL}/api/v1/build-failures" \
  -H "Content-Type: application/json" \
  "${AUTH_HEADER[@]}" \
  --data "${PAYLOAD}" >/dev/null 2>&1; then
  mkdir -p "${ROOT_DIR}/.qyl"
  printf '%s\n' "${PAYLOAD}" >> "${ROOT_DIR}/.qyl/build-failures-spool.jsonl"
fi
