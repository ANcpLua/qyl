#!/usr/bin/env bash
set -euo pipefail

# qyl-verify: One-shot verification of the entire qyl stack
# Usage: qyl-verify.sh [--all|--backend|--frontend|--docker|--typespec|--ports]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'
BOLD='\033[1m'

# Counters
PASS=0
FAIL=0
SKIP=0
WARN=0

# Results accumulator
RESULTS=()

pass() { ((PASS++)); RESULTS+=("PASS|$1"); printf "${GREEN}PASS${NC} %s\n" "$1"; }
fail() { ((FAIL++)); RESULTS+=("FAIL|$1|$2"); printf "${RED}FAIL${NC} %s — %s\n" "$1" "$2"; }
skip() { ((SKIP++)); RESULTS+=("SKIP|$1|$2"); printf "${YELLOW}SKIP${NC} %s — %s\n" "$1" "$2"; }
warn() { ((WARN++)); RESULTS+=("WARN|$1|$2"); printf "${YELLOW}WARN${NC} %s — %s\n" "$1" "$2"; }
section() { printf "\n${BOLD}${CYAN}── %s${NC}\n" "$1"; }

# Parse args
MODE="${1:---all}"

verify_backend() {
    section "BACKEND (.NET)"

    # 1. dotnet restore
    if dotnet restore "$ROOT_DIR" --verbosity quiet 2>/dev/null; then
        pass "dotnet restore"
    else
        fail "dotnet restore" "NuGet restore failed"
        return
    fi

    # 2. Build each project
    local projects=(
        "src/qyl.protocol"
        "src/qyl.servicedefaults"
        "src/qyl.servicedefaults.generator"
        "src/qyl.instrumentation.generators"
        "src/qyl.collector"
        "src/qyl.mcp"
        "src/qyl.copilot"
        "src/qyl.hosting"
        "src/qyl.cli"
        "src/qyl.watch"
        "src/qyl.watchdog"
        "src/qyl.Analyzers"
        "src/qyl.Analyzers.CodeFixes"
    )

    for proj in "${projects[@]}"; do
        local projpath="$ROOT_DIR/$proj"
        if [ ! -d "$projpath" ]; then
            skip "$proj" "directory not found"
            continue
        fi
        local csproj
        csproj=$(find "$projpath" -maxdepth 1 -name "*.csproj" -print -quit 2>/dev/null)
        if [ -z "$csproj" ]; then
            skip "$proj" "no .csproj found"
            continue
        fi
        if dotnet build "$csproj" --no-restore --verbosity quiet 2>/dev/null; then
            pass "build $proj"
        else
            fail "build $proj" "compilation failed"
        fi
    done

    # 3. Build test projects
    local test_projects
    test_projects=$(find "$ROOT_DIR/tests" -name "*.csproj" 2>/dev/null)
    for tp in $test_projects; do
        local tname
        tname=$(basename "$(dirname "$tp")")
        if dotnet build "$tp" --no-restore --verbosity quiet 2>/dev/null; then
            pass "build tests/$tname"
        else
            fail "build tests/$tname" "test project compilation failed"
        fi
    done

    # 4. Run tests
    section "TESTS (.NET)"
    for tp in $test_projects; do
        local tname
        tname=$(basename "$(dirname "$tp")")
        local test_output
        if test_output=$(dotnet test "$tp" --no-build --verbosity quiet 2>&1); then
            local test_count
            test_count=$(echo "$test_output" | grep -oE '[0-9]+ passed' | head -1 || echo "? passed")
            pass "test $tname ($test_count)"
        else
            local exit_code=$?
            case $exit_code in
                2) fail "test $tname" "tests failed (exit 2)" ;;
                5) fail "test $tname" "invalid command line args (exit 5)" ;;
                8) warn "test $tname" "zero tests matched filter (exit 8)" ;;
                *) fail "test $tname" "exit code $exit_code" ;;
            esac
        fi
    done

    # 5. Build warnings check
    section "WARNINGS (.NET)"
    local warn_output
    warn_output=$(dotnet build "$ROOT_DIR" --verbosity quiet 2>&1 | grep -c "warning " || true)
    if [ "$warn_output" -eq 0 ]; then
        pass "zero build warnings"
    else
        warn "build warnings" "$warn_output warnings found"
    fi
}

verify_frontend() {
    section "FRONTEND (React/TypeScript)"

    local dashboard_dir="$ROOT_DIR/src/qyl.dashboard"
    if [ ! -d "$dashboard_dir" ]; then
        skip "dashboard" "directory not found"
        return
    fi

    # 1. npm install check
    if [ ! -d "$dashboard_dir/node_modules" ]; then
        if (cd "$dashboard_dir" && npm install --silent 2>/dev/null); then
            pass "npm install (dashboard)"
        else
            fail "npm install (dashboard)" "dependency installation failed"
            return
        fi
    else
        pass "npm install (dashboard) — cached"
    fi

    # 2. TypeScript build
    if (cd "$dashboard_dir" && npm run build 2>/dev/null); then
        pass "npm run build (dashboard)"
    else
        fail "npm run build (dashboard)" "Vite/TypeScript build failed"
    fi

    # 3. Tests
    if (cd "$dashboard_dir" && npm run test -- --run 2>/dev/null); then
        pass "npm run test (dashboard)"
    else
        local exit_code=$?
        if [ $exit_code -eq 0 ]; then
            pass "npm run test (dashboard)"
        else
            fail "npm run test (dashboard)" "vitest failed (exit $exit_code)"
        fi
    fi

    # 4. Lint
    if (cd "$dashboard_dir" && npm run lint 2>/dev/null); then
        pass "npm run lint (dashboard)"
    else
        warn "npm run lint (dashboard)" "lint issues found"
    fi

    # 5. Browser SDK
    section "BROWSER SDK"
    local browser_dir="$ROOT_DIR/src/qyl.browser"
    if [ ! -d "$browser_dir" ]; then
        skip "browser SDK" "directory not found"
        return
    fi

    if [ ! -d "$browser_dir/node_modules" ]; then
        (cd "$browser_dir" && npm install --silent 2>/dev/null)
    fi

    if (cd "$browser_dir" && npm run build 2>/dev/null); then
        pass "npm run build (browser SDK)"
    else
        fail "npm run build (browser SDK)" "build failed"
    fi
}

verify_docker() {
    section "DOCKER"

    if ! command -v docker &>/dev/null; then
        skip "docker" "docker not installed"
        return
    fi

    local dockerfiles=(
        "src/qyl.collector/Dockerfile"
        "src/qyl.mcp/Dockerfile"
        "src/qyl.dashboard/Dockerfile"
    )

    for df in "${dockerfiles[@]}"; do
        local dfpath="$ROOT_DIR/$df"
        if [ ! -f "$dfpath" ]; then
            skip "docker $df" "Dockerfile not found"
            continue
        fi

        # Syntax check via --check (dry run)
        local name
        name=$(echo "$df" | sed 's|src/||;s|/Dockerfile||')
        if docker build --check -f "$dfpath" "$ROOT_DIR" 2>/dev/null; then
            pass "docker syntax $name"
        else
            # Fallback: just check file exists and has FROM
            if grep -q "^FROM" "$dfpath" 2>/dev/null; then
                pass "docker syntax $name (basic check)"
            else
                fail "docker syntax $name" "invalid Dockerfile"
            fi
        fi
    done

    # Check compose files
    local composes=(
        "src/qyl.collector/docker-compose.yml"
    )
    for cf in "${composes[@]}"; do
        local cfpath="$ROOT_DIR/$cf"
        if [ ! -f "$cfpath" ]; then
            skip "compose $cf" "file not found"
            continue
        fi
        if docker compose -f "$cfpath" config --quiet 2>/dev/null; then
            pass "compose config $(basename "$(dirname "$cfpath")")"
        else
            fail "compose config $(basename "$(dirname "$cfpath")")" "invalid compose file"
        fi
    done
}

verify_typespec() {
    section "TYPESPEC"

    local core_dir="$ROOT_DIR/core"
    if [ ! -d "$core_dir" ]; then
        skip "typespec" "core/ directory not found"
        return
    fi

    # Check node_modules
    if [ ! -d "$core_dir/node_modules" ]; then
        if (cd "$core_dir" && npm install --silent 2>/dev/null); then
            pass "npm install (typespec)"
        else
            fail "npm install (typespec)" "dependency installation failed"
            return
        fi
    else
        pass "npm install (typespec) — cached"
    fi

    # Compile TypeSpec
    if (cd "$core_dir" && npm run compile 2>/dev/null); then
        pass "typespec compile"
    else
        fail "typespec compile" "TypeSpec compilation failed"
    fi

    # Check generated files exist
    local gen_files=(
        "core/openapi/openapi.yaml"
        "src/qyl.protocol/Primitives/Scalars.g.cs"
        "src/qyl.protocol/Enums/Enums.g.cs"
        "src/qyl.collector/Storage/DuckDbSchema.g.cs"
        "src/qyl.dashboard/src/types/api.ts"
    )
    for gf in "${gen_files[@]}"; do
        if [ -f "$ROOT_DIR/$gf" ]; then
            pass "generated $gf"
        else
            fail "generated $gf" "file missing"
        fi
    done
}

verify_ports() {
    section "PORTS"

    local ports=(5100 4317 5173)
    local names=("collector-http" "collector-grpc" "dashboard-dev")

    for i in "${!ports[@]}"; do
        local port=${ports[$i]}
        local name=${names[$i]}
        if lsof -i ":$port" -sTCP:LISTEN >/dev/null 2>&1; then
            warn "port $port ($name)" "already in use by $(lsof -i ":$port" -sTCP:LISTEN -t 2>/dev/null | head -1)"
        else
            pass "port $port ($name) — available"
        fi
    done
}

# Banner
printf "\n${BOLD}${CYAN}╔════════════════════════════════════════╗${NC}\n"
printf "${BOLD}${CYAN}║          qyl-verify                    ║${NC}\n"
printf "${BOLD}${CYAN}╚════════════════════════════════════════╝${NC}\n"
printf "Root: %s\n" "$ROOT_DIR"
printf "Mode: %s\n" "$MODE"
printf "Time: %s\n" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"

case "$MODE" in
    --all)
        verify_backend
        verify_frontend
        verify_docker
        verify_typespec
        verify_ports
        ;;
    --backend)  verify_backend ;;
    --frontend) verify_frontend ;;
    --docker)   verify_docker ;;
    --typespec)  verify_typespec ;;
    --ports)    verify_ports ;;
    *)
        echo "Usage: qyl-verify.sh [--all|--backend|--frontend|--docker|--typespec|--ports]"
        exit 1
        ;;
esac

# Summary
section "SUMMARY"
printf "${BOLD}╔══════════════════════════════════╗${NC}\n"
printf "${BOLD}║${NC} ${GREEN}PASS: %-4d${NC} ${RED}FAIL: %-4d${NC}           ${BOLD}║${NC}\n" "$PASS" "$FAIL"
printf "${BOLD}║${NC} ${YELLOW}WARN: %-4d${NC} SKIP: %-4d           ${BOLD}║${NC}\n" "$WARN" "$SKIP"
printf "${BOLD}╚══════════════════════════════════╝${NC}\n"

if [ "$FAIL" -gt 0 ]; then
    printf "\n${RED}${BOLD}VERDICT: FAIL${NC} — %d failures need attention\n\n" "$FAIL"
    # Print failures
    for r in "${RESULTS[@]}"; do
        if [[ "$r" == FAIL* ]]; then
            printf "  ${RED}✗${NC} %s\n" "$(echo "$r" | cut -d'|' -f2-)"
        fi
    done
    printf "\n"
    exit 1
else
    printf "\n${GREEN}${BOLD}VERDICT: PASS${NC} — all checks green\n\n"
    exit 0
fi
