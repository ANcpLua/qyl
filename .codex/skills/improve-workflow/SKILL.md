---
name: improve-workflow
description: |
  Use when analyzing a codebase for improvement opportunities - library adoption, cohesive refactoring, pattern optimization. Knows when 'already clean' is the answer.
---

## Source Metadata

```yaml
plugin:
  name: "codebase-improver"
  version: "1.0.0"
  description: "Deep codebase analysis for library adoption, cohesive refactoring, and senior-level improvement suggestions. Knows when 'clean enough' is the right answer."
  author:
    name: "ANcpLua"
```


# Codebase Improvement Workflow

A systematic approach to finding genuine improvement opportunities in codebases.

## When to Use

- User wants to improve/optimize code
- Looking for library adoption opportunities
- Checking if utilities are being leveraged
- Seeking cohesive refactoring suggestions
- Code review for patterns and consistency

## The Workflow

### Step 1: Ecosystem Discovery

**Before analyzing code, understand what's available:**

```
1. Read CLAUDE.md for project context
2. Check package references for available libraries
3. Map the dependency hierarchy
4. Identify utility packages (Guard, extensions, test infrastructure)
```

**For ANcpLua projects, check:**
- Is `ANcpLua.Roslyn.Utilities` referenced?
- Is `ANcpLua.Roslyn.Utilities.Testing` used?
- What's the project's layer in the ecosystem?

### Step 2: Library Deep Dive

**Actually read the utility libraries:**

```bash
# Find the utility source
ls ~/ANcpLua.Roslyn.Utilities/

# Read Guard.cs to understand available validations
# Read Extensions to understand available helpers
# Read Testing base classes
```

**Document what you find:**
| Category | Methods Available | Example Usage |
|----------|------------------|---------------|
| Null validation | NotNull, NotNullOrEmpty | `Guard.NotNull(arg)` |
| Numeric | Positive, NotNegative | `Guard.Positive(count)` |
| ... | ... | ... |

### Step 3: Pattern Search

**Search for improvable patterns:**

```bash
# Find manual null checks
grep -r "throw new ArgumentNullException" src/

# Find manual validation patterns
grep -r "if.*==.*null.*throw" src/

# Find verbose patterns that libraries simplify
grep -r "string.IsNullOrEmpty" src/
```

**Match patterns to library methods:**

| Found Pattern | Library Replacement |
|--------------|---------------------|
| `x ?? throw new ArgumentNullException(nameof(x))` | `Guard.NotNull(x)` |
| `if (string.IsNullOrEmpty(s)) throw...` | `Guard.NotNullOrEmpty(s)` |
| `if (count <= 0) throw...` | `Guard.Positive(count)` |

### Step 4: Honest Assessment

**Critical decision point:**

```
IF concrete_improvements.count > 0 AND benefit > migration_cost:
    → Report specific opportunities with code examples

ELSE IF codebase_already_uses_patterns:
    → Report "Already clean" with evidence

ELSE IF improvements_marginal:
    → Report "Clean enough - changes would be over-engineering"
```

### Step 5: Report Format

**For opportunities found:**
```markdown
## Improvement Opportunities

### 1. Guard Clause Adoption [Impact: Medium]

**Files:** src/Services/UserService.cs, src/Validators/InputValidator.cs

**Current:**
```csharp
public void SetName(string name)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentNullException(nameof(name));
    _name = name;
}
```

**Improved:**
```csharp
public void SetName(string name) => _name = Guard.NotNullOrEmpty(name);
```

**Benefits:**
- Automatic parameter name capture via CallerArgumentExpression
- Consistent validation patterns
- Reduced boilerplate
```

**For clean codebases:**
```markdown
## Assessment: Clean Codebase

### Analysis Scope
- Analyzed 47 .cs files
- Checked: Guard patterns, test infrastructure, extensions usage

### Findings
- ✅ Guard.NotNull used consistently (23 usages)
- ✅ AnalyzerTest<T> base class adopted
- ✅ DiagnosticIds centralized

### Verdict
This codebase already follows ANcpLua patterns effectively.
No significant improvements identified.

### Optional micro-optimizations (low value):
- Could use Guard.Positive in 2 places instead of manual check
  (marginal benefit, not recommended)
```

## Key Principles

1. **Read before suggesting** - Never recommend without seeing actual code
2. **Library knowledge required** - Understand what's available before searching
3. **Concrete examples** - Every suggestion has before/after code
4. **Honest assessment** - "Already clean" is a valid outcome
5. **Cost/benefit awareness** - Migration effort vs. improvement value
6. **Pattern respect** - Don't force changes to intentional design choices

## Anti-Patterns to Avoid

❌ "This code could be improved" without specifics
❌ Suggesting libraries without reading their APIs
❌ Over-engineering working code
❌ Chasing "best practices" that don't fit context
❌ Ignoring existing patterns for theoretical purity
❌ Recommending changes that don't provide real value

## References

See `references/` for:
- `ancplua-guard-methods.md` - Complete Guard API reference
- `ancplua-testing-base.md` - Testing infrastructure patterns
- `improvement-patterns.md` - Common improvement opportunities
