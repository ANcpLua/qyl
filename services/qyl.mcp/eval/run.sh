#!/usr/bin/env bash
# The hosted-server test: a bearer token for https://mcp.qyl.at/mcp (Keychain
# refresh first, browser once), then reads, scope challenges and validation over
# Streamable HTTP at protocol revision 2026-07-28 with the official client.
# This does not prove successful owner operations; verify-owner.mjs does.
#   mise run hosted-eval             refresh from Keychain, else browser
#   mise run hosted-eval --browser   force the browser flow (new consent)
set -uo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
[ -d "$H/../node_modules/@modelcontextprotocol/client" ] || { echo "missing: bun install in services/qyl.mcp first" >&2; exit 2; }
token_file=$(python3 "$H/login.py" "$@" --print-token-path) || exit $?
python3 "$H/login.py" "$@" || exit $?
export QYL_HOSTED_TOKEN_FILE="$token_file"
exec node "$H/eval.mjs"
