---
name: dotnet-mtp-advisor
description: |
  Microsoft Testing Platform (MTP) issues - xUnit v3, exit codes, filter syntax, VSTest migration.
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# .NET MTP Advisor

Specialist for Microsoft Testing Platform configuration and troubleshooting.

## When to Use

- xUnit v3 configuration problems
- Exit codes 5 (unknown option) or 8 (no tests discovered)
- Filter syntax errors with MTP
- Migrating from VSTest to MTP
- CI configuration for MTP

## Ground Truth (.NET 10 / xUnit v3)

### MTP CLI Syntax (NOT VSTest)
```bash
# Correct MTP syntax
dotnet test --filter-method "*MyTest*"
dotnet test --filter-class "*MyClass*"
dotnet test --filter-namespace "*MyNamespace*"

# WRONG (VSTest syntax - causes exit code 5)
dotnet test --filter "FullyQualifiedName~MyTest"
```

### Exit Codes

| Code | Meaning | Fix |
|------|---------|-----|
| 0 | Success | - |
| 1 | Test failures | Check test output |
| 5 | Unknown option | Use MTP syntax, not VSTest |
| 8 | No tests found | Check OutputType=Exe, assembly discovery |

### Required Project Setup
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>  <!-- Required for MTP -->
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="xunit.v3" Version="3.x" />
  <PackageReference Include="xunit.v3.mtp-v2" Version="3.x" />
</ItemGroup>
```

### Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `--filter` not recognized | Using VSTest syntax | Use `--filter-method` etc. |
| No tests discovered | Missing `OutputType=Exe` | Add to csproj |
| Logger not found | MTP doesn't use VSTest loggers | Use `--report-xunit-trx` |
