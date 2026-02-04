---
name: codebase-cohesion
description: |
  Refactor repositories for exceptional maintainability. Use when cleaning up dead code, consolidating duplicates, or unifying patterns across ANcpLua repos. Breaking API changes are acceptable. Invoke via /codebase-cohesion [repos...] or when user mentions "refactor", "cohesion", "dead code", "duplicates", "consolidate".
---

## Source Metadata

```yaml
# none
```


# Codebase Cohesion Refactoring

## Goal

Transform target repositories into exceptionally well-maintained, cohesive codebases.

## Target Repositories

Default targets (if none specified):
- `/Users/ancplua/ANcpLua.Analyzers`
- `/Users/ancplua/qyl`
- `/Users/ancplua/ANcpLua.NET.Sdk`
- `/Users/ancplua/ErrorOrX`
- `/Users/ancplua/ServiceDefaults1`
- `/Users/ancplua/Template/Template/src`

Source of truth (check before adding utilities):
- `/Users/ancplua/ANcpLua.Roslyn.Utilities`

## Process

### Phase 1: Parallel Discovery

Deploy parallel agents to scan each repo for:
1. **Duplicate patterns** — Similar logic solving the same problem
2. **Dead code** — Unused classes, methods, commented blocks
3. **Antipatterns** — Patterns with simpler alternatives
4. **Subtle issues** — Logic that looks correct but has edge cases
5. **Missed abstractions** — Cases where starting fresh beats fixing

Focus on PATTERNS, not specific line numbers.

### Phase 2: Cross-Repo Analysis

Identify:
- Logic that exists in multiple repos (should be in Roslyn.Utilities?)
- Inconsistent implementations of the same concept
- Opportunities to share via SDK or upstream package

### Phase 3: Refactoring

For each finding:
1. Propose the cleanest fix (not the minimal fix)
2. Explain WHY refactored code is more reusable and cohesive
3. Note any subtle issues the original had
4. Implement the change
5. Verify build passes

## Rules

- **Breaking changes to public APIs are acceptable** — Prioritize code quality
- **Do NOT list specific line numbers** — Focus on patterns
- **Check Roslyn.Utilities first** — Does a utility already exist upstream?
- **Consider upstreaming** — Should this helper live in Roslyn.Utilities?
- **Explain the "why"** — Every refactor must justify itself

## Cohesion Principles

1. **Single Source of Truth** — Never duplicate logic that exists upstream
2. **Minimal Interface** — Use smallest API surface
3. **Compile-Time > Runtime** — Prefer generators over reflection
4. **Null = Empty** — Collections treat null as empty
5. **Fully Qualified Names** — Generated code uses `global::` prefix

## Output Format

For each refactor:

```
## [Pattern Name]

**Repos affected**: ErrorOrX, Analyzers
**Issue**: [Description of the pattern/problem found]
**Why it matters**: [Subtle issues or maintenance burden]
**Solution**: [The refactored approach]
**Why it's better**: [Reusability, expressiveness, cohesion gains]
**Files changed**: [List of files]
**Build verified**: ✅
```

## Verification

After all changes:
```bash
dotnet build *.slnx  # Each repo
dotnet test --solution *.slnx  # Each repo
```

## Executable Scripts

### Quick Dead Code Scan

```bash
#!/bin/bash
# find-dead-code.sh - Scan for potential dead code patterns
REPO="${1:-.}"

echo "=== Unused private methods (candidates) ==="
grep -rn "private.*\s\w\+(" "$REPO/src" --include="*.cs" | head -20

echo "=== Commented code blocks ==="
grep -rn "^\s*//\s*\(public\|private\|internal\|class\|var\|return\)" "$REPO/src" --include="*.cs" | head -20

echo "=== Empty catch blocks ==="
grep -rn "catch\s*{.*}" "$REPO/src" --include="*.cs" | head -10
```

### Find Duplicates Across Repos

```bash
#!/bin/bash
# find-duplicates.sh - Look for code duplicated across repos
PATTERN="${1:-Helper}"

echo "=== Searching for '$PATTERN' across ecosystem ==="
for repo in /Users/ancplua/{ANcpLua.Roslyn.Utilities,ANcpLua.Analyzers,ErrorOrX,qyl,ServiceDefaults1}; do
  echo "--- $(basename "$repo") ---"
  grep -rn "$PATTERN" "$repo/src" --include="*.cs" 2>/dev/null | head -5
done
```

### Verify All Repos After Refactor

```bash
#!/bin/bash
# verify-refactor.sh - Full verification after cross-repo changes
set -e
REPOS=(
  "/Users/ancplua/ANcpLua.Roslyn.Utilities"
  "/Users/ancplua/ANcpLua.Analyzers"
  "/Users/ancplua/ErrorOrX"
  "/Users/ancplua/qyl"
  "/Users/ancplua/ServiceDefaults1"
)

echo "=== Phase 1: Clean Build ==="
for repo in "${REPOS[@]}"; do
  cd "$repo"
  dotnet build *.slnx --no-restore -v q || exit 1
done

echo "=== Phase 2: Full Test Suite ==="
for repo in "${REPOS[@]}"; do
  cd "$repo"
  dotnet test --solution *.slnx --no-build || exit 1
done

echo "=== All verifications passed ==="
```

## Example Invocations

```
/codebase-cohesion
# Scans all default repos

/codebase-cohesion ErrorOrX Analyzers
# Scans only specified repos

/codebase-cohesion --focus duplicates
# Focus on duplicate code patterns only

/codebase-cohesion --focus dead-code
# Focus on unused code removal only
```

## Related Resources

- **Ecosystem context**: `/ancplua-ecosystem` skill
- **Specialized agents**:
  - `ancplua-analyzers-specialist`
  - `qyl-observability-specialist`
  - `ancplua-sdk-specialist`
  - `erroror-generator-specialist`
  - `servicedefaults-specialist`
  - `template-clean-arch-specialist`
