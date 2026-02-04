# Common Improvement Patterns

Patterns to search for when analyzing codebases.

## Library Adoption Patterns

### Guard Clause Adoption

**High-value pattern replacements:**

| Pattern to Find | Grep Pattern | Replacement |
|----------------|--------------|-------------|
| Null-coalescing throw | `\?\? throw new ArgumentNullException` | `Guard.NotNull(x)` |
| Manual null check | `if.*==.*null.*throw` | `Guard.NotNull(x)` |
| IsNullOrEmpty check | `string\.IsNullOrEmpty.*throw` | `Guard.NotNullOrEmpty(x)` |
| IsNullOrWhiteSpace check | `string\.IsNullOrWhiteSpace.*throw` | `Guard.NotNullOrWhiteSpace(x)` |
| Positive check | `<=\s*0.*throw` | `Guard.Positive(x)` |
| Non-negative check | `<\s*0.*throw` | `Guard.NotNegative(x)` |
| Zero check | `==\s*0.*throw` | `Guard.NotZero(x)` |
| Guid.Empty check | `==\s*Guid\.Empty.*throw` | `Guard.NotEmpty(guid)` |
| Enum.IsDefined | `Enum\.IsDefined.*throw` | `Guard.DefinedEnum(x)` |
| Collection empty | `\.Count\s*==\s*0.*throw` | `Guard.NotNullOrEmpty(collection)` |

### Testing Infrastructure

| Pattern to Find | Improvement |
|----------------|-------------|
| Manual `CreateCompilation` | Use `AnalyzerTest<T>` base |
| `GetDiagnostics()` helpers | Use `VerifyAsync()` with markers |
| Custom workspace setup | Inherit from base classes |
| Assert diagnostic count | Use `[|...|]` span markers |

---

## Cohesive Refactoring Patterns

### Repeated Validation Blocks

**Before:**
```csharp
public void Method1(string name, int count)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentNullException(nameof(name));
    if (count <= 0)
        throw new ArgumentOutOfRangeException(nameof(count));
    // ...
}

public void Method2(string name, int count)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentNullException(nameof(name));
    if (count <= 0)
        throw new ArgumentOutOfRangeException(nameof(count));
    // ...
}
```

**After:**
```csharp
public void Method1(string name, int count)
{
    _name = Guard.NotNullOrEmpty(name);
    _count = Guard.Positive(count);
    // ...
}

public void Method2(string name, int count)
{
    _name = Guard.NotNullOrEmpty(name);
    _count = Guard.Positive(count);
    // ...
}
```

### Inconsistent Exception Messages

**Before:**
```csharp
throw new ArgumentNullException(nameof(value));
throw new ArgumentNullException("value cannot be null", nameof(value));
throw new ArgumentNullException(nameof(value), "Value is required");
```

**After:** (All consistent via Guard)
```csharp
Guard.NotNull(value);  // Consistent message format
```

---

## When NOT to Refactor

### Already Clean Indicators

✅ **Do NOT suggest changes when:**
- Guard.NotNull already used consistently
- Test base classes already adopted
- Patterns are intentionally different (documented reason)
- Change would be marginal (< 3 instances)

### Over-Engineering Indicators

⚠️ **Avoid suggesting:**
- New abstractions for single-use patterns
- Library adoption for 1-2 occurrences
- Consistency changes that break existing APIs
- Refactoring that increases complexity

---

## Search Commands

```bash
# Find null-coalescing throws
grep -rn "\?\? throw new ArgumentNullException" src/

# Find manual null checks
grep -rn "if.*==.*null.*throw" src/

# Find string validation patterns
grep -rn "IsNullOrEmpty\|IsNullOrWhiteSpace" src/

# Find numeric validations
grep -rn "<= 0.*throw\|< 0.*throw\|== 0.*throw" src/

# Find Guid.Empty checks
grep -rn "Guid\.Empty" src/

# Find Enum.IsDefined
grep -rn "Enum\.IsDefined" src/

# Count existing Guard usage
grep -rc "Guard\." src/ | grep -v ":0$"

# Find test files not using base classes
grep -rL "AnalyzerTest\|CodeFixTest\|RefactoringTest" tests/**/*Tests.cs
```

---

## Decision Framework

```
1. Count occurrences of improvable pattern
   → If < 3: Skip (not worth migration cost)
   → If >= 3: Continue analysis

2. Check if library is already referenced
   → If no: Consider if adding dep is worth it
   → If yes: Continue

3. Check existing usage of improved pattern
   → If already mixed: Standardize on one
   → If not used: Full adoption opportunity

4. Estimate migration effort
   → Simple find/replace: Low effort
   → Requires understanding context: Medium
   → Could break behavior: High (reconsider)

5. Final decision
   → Low effort + >= 3 occurrences: Recommend
   → Medium effort + >= 5 occurrences: Recommend
   → High effort: Only if critical benefit
```

---

## Output Templates

### Opportunity Found

```markdown
### [Number]. [Category] - [Impact: High/Medium/Low]

**Pattern:** [Description]
**Occurrences:** [Count] instances in [File count] files

**Example (from `path/to/file.cs:line`):**
```csharp
// Current
[actual code]

// Improved
[improved code]
```

**Files affected:**
- `path/to/file1.cs` (3 instances)
- `path/to/file2.cs` (2 instances)

**Migration notes:** [Any special considerations]
```

### No Opportunities

```markdown
## Assessment: Clean Codebase ✨

**Scope:** Analyzed [X] files, checked [Y] patterns

**Findings:**
- ✅ Guard patterns: [Status]
- ✅ Test infrastructure: [Status]
- ✅ Consistency: [Status]

**Verdict:** [Why it's clean / what's good]

**Optional micro-improvements:** [Only if truly helpful]
```
