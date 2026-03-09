#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
worktrees_dir="$repo_root/.worktrees"

worktrees=(
  agent-collector
  agent-generator
  agent-schema
  agent-investigator
  agent-architect
  agent-gardener
)

role_contents_common='
Repository architecture constraints:
- Layer 1 (Schema generation): eng/build/SchemaGenerator.cs
- Layer 2 (Roslyn source generator): src/qyl.servicedefaults.generator/
- Layer 3 (Runtime instrumentation): src/qyl.servicedefaults/, src/qyl.collector/

These layers must never be conflated in scope, ownership, or edits.
'

get_agent_section() {
  local role=$1

  case "$role" in
    agent-collector)
      cat <<'EOF_AGENT'
## Worktree role: OTLP collector maintainer
- Scope: `src/qyl.collector`
- Focus: OTLP ingestion, DuckDB batching, SSE streaming lifecycle
- Rules:
  - Use minimal diffs.
  - Never modify generator or schema code.
  - Prefer normal scoped execution; escalate to heavy reasoning only for cross-layer incidents.
- Model routing:
  - Default: Claude Sonnet 4.6 (`high`)
  - Escalate to: Claude Opus 4.6 (`max`) only when investigation becomes cross-layer.
EOF_AGENT
      ;;
    agent-generator)
      cat <<'EOF_AGENT'
## Worktree role: Roslyn pipeline maintainer
- Scope: `src/qyl.servicedefaults.generator`
- Focus: incremental pipeline behavior, generator correctness, compile-time interception design
- Rules:
  - Preserve pipeline isolation.
  - Preserve incremental generator design.
  - Never touch `eng/build/SchemaGenerator.cs` unless explicitly required.
- Model routing:
  - Default: Claude Opus 4.6 (`max`)
  - Escalate to higher effort only for rare architectural ambiguity.
EOF_AGENT
      ;;
    agent-schema)
      cat <<'EOF_AGENT'
## Worktree role: schema generator maintainer
- Scope: `eng/build/SchemaGenerator.cs`
- Focus: OpenAPI to C# scalar/enum generation, DuckDB DDL emission
- Rules:
  - Maintain single-source-of-truth schema pipeline.
  - Avoid runtime or generator changes unless explicitly required.
- Model routing:
  - Default: Claude Opus 4.6 (`max`)
  - Escalate only for rare architectural ambiguity.
EOF_AGENT
      ;;
    agent-investigator)
      cat <<'EOF_AGENT'
## Worktree role: cross-layer debugging agent
- Allowed scope: generator, runtime, collector
- Tasks:
  - incident investigation
  - telemetry tracing
  - root cause analysis
  - cross-layer fault isolation
- Model routing:
  - Default: Claude Opus 4.6 (`max`)
  - Prefer high-confidence tracing and minimal surface changes.
EOF_AGENT
      ;;
    agent-architect)
      cat <<'EOF_AGENT'
## Worktree role: repository architect
- Focus: cross-module cleanup, architecture improvements, multi-file changes
- Tasks:
  - cross-module cleanup
  - architecture improvements
  - multi-file changes
  - cross-cutting concern reduction
  - polyglot or multi-boundary refactors when needed
- Model routing:
  - Default: Claude Opus 4.6 (`max`)
  - Use conservative refactors to minimize behavioral drift.
EOF_AGENT
      ;;
    agent-gardener)
      cat <<'EOF_AGENT'
## Worktree role: repository gardener
- Focus: small correctness fixes, comment cleanup, verification improvements, documentation accuracy
- Rules:
  - Keep changes small and low-risk.
  - Prefer correctness, readability, and testability.
- Model routing:
  - Default: Claude Sonnet 4.6 (`high`)
  - Escalate only for correctness ambiguities or architecture-impacting design questions.
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

Verification commands:
- dotnet build
- dotnet test

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
  branch="worktree/$name"

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
printf "%-22s %-9s %s\n" "WORKTREE" "STATUS" "ABSOLUTE_PATH"
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

  printf "%-22s %-9s %s\n" "$name" "$status" "$path"
  printf "  branch: %s\n" "$branch"
  printf "  AGENTS.md: %s\n" "$exists_agents"
  printf "  CLAUDE.md: %s\n" "$exists_claude"
  printf "  .codex/agent.md: %s\n\n" "$exists_codex"
done

echo "git worktree list (worktrees only):"
git worktree list --porcelain | awk '$1 == "worktree" {print $2}'
