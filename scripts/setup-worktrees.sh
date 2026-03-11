#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
worktrees_dir="$repo_root/.worktrees"

worktrees=(
  wt-backend
  wt-frontend
  wt-loom
  wt-mcp
)

role_contents_common='
Repository architecture constraints (3 layers — never conflate):
- Layer 1 (Schema generation): eng/build/SchemaGenerator.cs — NUKE build time
- Layer 2 (Roslyn source generation): src/qyl.instrumentation.generators/ — MSBuild compile time
- Layer 3 (Runtime + collector): src/qyl.servicedefaults/, src/qyl.collector/ — Application runtime

Non-negotiable:
- Do not confuse schema generation with Roslyn source generation.
- Do not treat compile-time interception as runtime reflection.
- Do not modify layers 1/2/3 unless the failing behavior is proven to originate there.
- Prefer fixes in feature/service layers first (dashboard, mcp, Loom services).
 
Verification: dotnet build only — do NOT run dotnet test or nuke test in worktrees.
'

get_agent_section() {
  local role=$1

  case "$role" in
    wt-backend)
      cat <<'EOF_AGENT'
## Worktree role: Backend
- Scope: `src/qyl.collector`, API handlers, DuckDB queries, persistence, backend Loom services
- Ownership:
  - Only touch `eng/build` if a backend contract bug is proven from generated schema/DDL.
  - Do not touch `src/qyl.instrumentation.generators` unless a compile-time interceptor bug directly causes the failing backend behavior.
- Model routing:
  - Default: Claude Sonnet 4.6 (`high`)
  - Escalate to: Claude Opus 4.6 (`max`) for cross-layer incidents or deep root cause analysis.
EOF_AGENT
      ;;
    wt-frontend)
      cat <<'EOF_AGENT'
## Worktree role: Frontend
- Scope: `src/qyl.dashboard`
- Ownership:
  - Never touch any generator layer.
  - If UI failures come from bad backend shape, document the contract mismatch and hand to backend.
- Model routing:
  - Default: Claude Sonnet 4.6 (`high`)
  - Escalate to: Claude Opus 4.6 (`max`) for complex state management or architecture decisions.
EOF_AGENT
      ;;
    wt-loom)
      cat <<'EOF_AGENT'
## Worktree role: Loom
- Scope: Loom-specific services and verification (Autofix, Triage, Code Review, Regression, Handoff)
- Ownership:
  - Treat generator and runtime layers as constrained deps.
  - If Autofix/Triage/Regression needs missing telemetry, prove whether it's collector ingestion, generated instrumentation, or runtime wiring before editing shared infra.
- Model routing:
  - Default: Claude Opus 4.6 (`max`)
  - Loom services cross multiple layers — default to heavy reasoning.
EOF_AGENT
      ;;
    wt-mcp)
      cat <<'EOF_AGENT'
## Worktree role: MCP
- Scope: `src/qyl.mcp` and protocol exposure
- Ownership:
  - Do not "fix" missing tool behavior by rewriting runtime instrumentation unless protocol validation proves root cause is telemetry availability.
- Model routing:
  - Default: Claude Sonnet 4.6 (`high`)
  - Escalate to: Claude Opus 4.6 (`max`) for protocol-level investigation.
EOF_AGENT
      ;;
    *)
      echo "Unknown role: $role" >&2
      exit 1
      ;;
  esac
}

write_common_files() {
  local path=$1
  local role=$2
  mkdir -p "$path/.codex"

  local tmp_agents tmp_claude tmp_codex
  tmp_agents="$(mktemp)"
  tmp_claude="$(mktemp)"
  tmp_codex="$(mktemp)"

  cat > "$tmp_agents" <<EOF_AGENTS
# AGENTS.md for ${role}

${role_contents_common}

$(get_agent_section "$role")

Operational rule:
- Do not modify existing repository source code outside your role scope.
- Only create or update worktree infrastructure files as directed.
EOF_AGENTS

  printf 'Read and follow AGENTS.md in this worktree.\n' > "$tmp_claude"
  printf 'Read and follow AGENTS.md in this worktree.\n' > "$tmp_codex"

  local changed=0

  if ! cmp -s "$tmp_agents" "$path/AGENTS.md" 2>/dev/null; then
    cat "$tmp_agents" > "$path/AGENTS.md"
    changed=1
  fi

  if ! cmp -s "$tmp_claude" "$path/CLAUDE.md" 2>/dev/null; then
    cat "$tmp_claude" > "$path/CLAUDE.md"
    changed=1
  fi

  if ! cmp -s "$tmp_codex" "$path/.codex/agent.md" 2>/dev/null; then
    cat "$tmp_codex" > "$path/.codex/agent.md"
    changed=1
  fi

  rm -f "$tmp_agents" "$tmp_claude" "$tmp_codex"
  printf "%s" "$changed"
}

is_registered_worktree() {
  local target=$1
  git worktree list --porcelain | awk -v target="$target" '
    $1 == "worktree" {path = $2}
    path == target {found = 1}
    END {exit found ? 0 : 1}
  '
}

mkdir -p "$worktrees_dir"

status_values=()

for name in "${worktrees[@]}"; do
  path="$worktrees_dir/$name"
  branch="dev/$name"

  if [[ -e "$path" ]]; then
    if ! is_registered_worktree "$path"; then
      echo "ERROR: path exists but is not a registered worktree: $path" >&2
      exit 1
    fi

    existing_branch=$(git -C "$path" rev-parse --abbrev-ref HEAD)
    if [[ "$existing_branch" != "$branch" ]]; then
      echo "ERROR: worktree $path uses branch '$existing_branch' but expected '$branch'" >&2
      exit 1
    fi

    changed="$(write_common_files "$path" "$name")"
    if [[ "$changed" == "0" ]]; then
      status_values+=("skipped")
    else
      status_values+=("reused")
    fi
    continue
  fi

  if ! git show-ref --verify --quiet "refs/heads/$branch"; then
    git branch "$branch" HEAD
  fi

  if git worktree list --porcelain | awk -v branch="refs/heads/$branch" '
      $1 == "branch" && $2 == branch {found = 1}
      END {exit found ? 0 : 1}
    '; then
    echo "ERROR: branch '$branch' is already attached to another worktree." >&2
    exit 1
  fi

  git worktree add "$path" "$branch"
  write_common_files "$path" "$name" >/dev/null
  status_values+=("created")
done

echo "Worktree setup summary:"
printf "%-16s %-9s %s\n" "WORKTREE" "STATUS" "ABSOLUTE_PATH"
printf -- '%.0s-' {1..80}; echo

for idx in "${!worktrees[@]}"; do
  name="${worktrees[$idx]}"
  path="$worktrees_dir/$name"
  branch="$(git -C "$path" rev-parse --abbrev-ref HEAD)"
  status="${status_values[$idx]}"
  exists_agents=false
  exists_claude=false
  exists_codex=false
  [[ -f "$path/AGENTS.md" ]] && exists_agents=true
  [[ -f "$path/CLAUDE.md" ]] && exists_claude=true
  [[ -f "$path/.codex/agent.md" ]] && exists_codex=true

  printf "%-16s %-9s %s\n" "$name" "$status" "$path"
  printf "  branch: %s\n" "$branch"
  printf "  AGENTS.md: %s\n" "$exists_agents"
  printf "  CLAUDE.md: %s\n" "$exists_claude"
  printf "  .codex/agent.md: %s\n\n" "$exists_codex"
done

echo "git worktree list (worktrees only):"
git worktree list --porcelain | awk '$1 == "worktree" {print $2}'
