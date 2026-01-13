# Agent Task: Fix AgentsGateway Central Package Management Conflicts

**Task ID:** CPM-CONFLICT-002
**Complexity:** Medium
**Estimated Tokens:** ~80k for full analysis + fix
**Success Criteria:** `dotnet restore` and `dotnet build` pass with zero NU1008 warnings

---

## Your Mission

You are fixing **Central Package Management (CPM) violations** in the AgentsGateway example project. The project has inline package versions that conflict with the repository's CPM policy.

---

## Context You Need

### What is CPM?

Central Package Management means ALL package versions are defined in ONE file (`Directory.Packages.props`), and individual `.csproj` files reference packages WITHOUT versions:

```xml
<!-- Directory.Packages.props (CENTRAL) -->
<PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.1" />

<!-- SomeProject.csproj (NO VERSION) -->
<PackageReference Include="xunit.v3.mtp-v2" />
```

### Repository Structure
```
/Users/ancplua/qyl/
├── Directory.Packages.props        # CENTRAL versions (source of truth)
├── Directory.Build.props           # Enables CPM: ManagePackageVersionsCentrally=true
└── examples/
    └── AgentsGateway/
        ├── src/
        │   └── AgentsGateway/
        │       └── AgentsGateway.csproj    # May have inline versions
        └── test/
            └── AgentGateway.Tests/
                └── AgentGateway.Tests.csproj  # HAS inline versions (PROBLEM)
```

### The Problem
```
AgentGateway.Tests.csproj contains:
─────────────────────────────────────
<PackageReference Include="xunit.v3" Version="2.9.3" />           ← WRONG!
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />  ← WRONG!

Directory.Packages.props defines:
──────────────────────────────────
<PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.1" />      ← CORRECT package
(xunit.runner.visualstudio is NOT defined - not needed with MTP)
```

### Errors You're Fixing
```
NU1008: Projects that use central package version management should not
        define the version on the PackageReference items.
```

---

## Files to Read First

Read these files IN ORDER:

1. `/Users/ancplua/qyl/Directory.Packages.props` - See what packages ARE defined centrally
2. `/Users/ancplua/qyl/Directory.Build.props` - Confirm CPM is enabled
3. `/Users/ancplua/qyl/examples/AgentsGateway/test/AgentGateway.Tests/AgentGateway.Tests.csproj` - Find violations

Then scan for ALL violations:
```bash
grep -r "PackageReference.*Version=" /Users/ancplua/qyl/examples/AgentsGateway --include="*.csproj"
```

---

## Step-by-Step Fix

### Step 1: Inventory All Violations

Create a table of all inline versions found:

```markdown
| File | Package | Inline Version | CPM Version | Action |
|------|---------|----------------|-------------|--------|
| AgentGateway.Tests.csproj | xunit.v3 | 2.9.3 | N/A | Replace with xunit.v3.mtp-v2 |
| AgentGateway.Tests.csproj | xunit.runner.visualstudio | 3.1.4 | N/A | Remove (not needed) |
| ... | ... | ... | ... | ... |
```

### Step 2: Decide for Each Package

For each package with inline version:

**Case A: Package exists in Directory.Packages.props**
→ Remove `Version="..."` from csproj, keep PackageReference

**Case B: Package NOT in Directory.Packages.props but needed**
→ Add to Directory.Packages.props, then remove version from csproj

**Case C: Package not needed (obsolete)**
→ Remove entire PackageReference

### Step 3: Apply Fixes

**Fix AgentGateway.Tests.csproj:**

```xml
<!-- BEFORE -->
<ItemGroup>
    <PackageReference Include="xunit.v3" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
</ItemGroup>

<!-- AFTER -->
<ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" />
    <!-- xunit.runner.visualstudio removed - MTP v2 handles test execution -->
</ItemGroup>
```

**If packages need adding to Directory.Packages.props:**

```xml
<!-- Add ONLY if not already present -->
<PackageVersion Include="SomeNewPackage" Version="1.0.0" />
```

### Step 4: Special Case - xUnit Migration

AgentsGateway uses old xUnit v2 patterns. For .NET 10 + MTP:

| Old (xUnit v2) | New (xUnit v3 + MTP) |
|----------------|----------------------|
| `xunit` or `xunit.v3` | `xunit.v3.mtp-v2` |
| `xunit.runner.visualstudio` | Not needed |
| `Microsoft.NET.Test.Sdk` | Not needed |

### Step 5: Validate

```bash
# Check for remaining violations
grep -r "PackageReference.*Version=" /Users/ancplua/qyl/examples/AgentsGateway --include="*.csproj"

# Should return nothing

# Test restore
dotnet restore /Users/ancplua/qyl/examples/AgentsGateway/AgentsGateway.sln

# Test build
dotnet build /Users/ancplua/qyl/examples/AgentsGateway/AgentsGateway.sln
```

---

## Validation Checklist

Before declaring success:

- [ ] `grep -r "PackageReference.*Version=" examples/AgentsGateway --include="*.csproj"` returns empty
- [ ] `dotnet restore` shows no NU1008 warnings
- [ ] `dotnet build` passes
- [ ] All xunit references use `xunit.v3.mtp-v2` (not `xunit` or `xunit.v3`)
- [ ] No `xunit.runner.visualstudio` references remain
- [ ] No `Microsoft.NET.Test.Sdk` references remain

---

## Common Packages Mapping

If you find these, here's what to do:

| Found in csproj | Exists in CPM? | Action |
|-----------------|----------------|--------|
| `Microsoft.Extensions.*` | Yes (10.0.1) | Remove version |
| `Microsoft.Agents.AI.*` | Yes (preview) | Remove version |
| `xunit.v3` | No (use mtp-v2) | Replace package |
| `Swashbuckle.AspNetCore` | No | Add to CPM |
| `Microsoft.AspNetCore.*` | Some | Check CPM |

---

## If You Get Stuck

### Problem: Package not in Directory.Packages.props
**Solution:** Add it! Use latest stable version from nuget.org

### Problem: Not sure if package is needed
**Solution:** Remove it, try to build. If build fails, add back.

### Problem: AgentsGateway has its own Directory.Build.props
**Solution:** Check if it disables CPM. If so, that's a design decision - document it.

### Problem: Tests don't run after migration
**Solution:** Ensure `xunit.v3.mtp-v2` is used and project has:
```xml
<OutputType>Exe</OutputType>
<IsTestProject>true</IsTestProject>
```

---

## Output Format

When complete, provide:

1. **Summary:** How many violations fixed
2. **Packages Added to CPM:** List any new entries in Directory.Packages.props
3. **Packages Removed:** List obsolete packages deleted
4. **Files Changed:** List with brief description
5. **Build Result:** Output of `dotnet build`
