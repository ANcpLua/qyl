# Feature: CLAUDE.md Auto-Generation via Nuke

> **Status:** Ready
> **Effort:** ~3h
> **Backend:** No (build tooling)
> **Priority:** P2

---

## Problem

Current CLAUDE.md generation via MSBuild leaves metadata fields empty (`Layer`, `Workflow`, `Test Coverage`). Manual
population is tedious and becomes stale.

## Solution

Nuke build target that scrapes project metadata automatically and regenerates CLAUDE.md files with accurate, up-to-date
information.

---

## Context

### Current State

```
qyl.collector/CLAUDE.md:
| Property | Value |
|----------|-------|
| Layer |  |              <-- empty
| Framework | net10.0 |
| Workflow |  |            <-- empty
| Test Coverage | % |      <-- empty
```

### Data Sources

| Field         | Source             | Method                   |
|---------------|--------------------|--------------------------|
| Layer         | Project references | Analyze dependency graph |
| Framework     | csproj             | Already works            |
| Workflow      | Code analysis      | Grep for key patterns    |
| Test Coverage | Coverage reports   | Parse Cobertura XML      |
| Dependencies  | PackageReference   | Extract from csproj      |

---

## Files

| File                                   | Action | What                                     |
|----------------------------------------|--------|------------------------------------------|
| `eng/build/Components/IClaudeBrain.cs` | Create | Nuke component                           |
| `eng/build/Build.cs`                   | Modify | Add IClaudeBrain                         |
| `eng/MSBuild/Shared.targets`           | Modify | Remove InjectClaudeBrain (moved to Nuke) |

---

## Implementation

### Step 1: Create IClaudeBrain Component

**File:** `eng/build/Components/IClaudeBrain.cs`

```csharp
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using System.Text;
using System.Xml.Linq;

public interface IClaudeBrain : INukeBuild
{
    [Parameter] string ClaudeBrainProject => TryGetValue(() => ClaudeBrainProject);

    Target GenerateClaudeMd => _ => _
        .Description("Generate CLAUDE.md files with scraped metadata")
        .Executes(() =>
        {
            var projects = ClaudeBrainProject is not null
                ? [Solution.GetProject(ClaudeBrainProject)]
                : Solution.AllProjects.Where(p => p.Path.Parent.Contains("src"));

            foreach (var project in projects)
            {
                var metadata = ScrapeProjectMetadata(project);
                var content = GenerateClaudeMdContent(project, metadata);
                var outputPath = project.Path.Parent / "CLAUDE.md";

                outputPath.WriteAllText(content);
                Log.Information("Generated: {Path}", outputPath);
            }
        });

    Target ClaudeBrainInfo => _ => _
        .Description("Show CLAUDE.md generation status")
        .Executes(() =>
        {
            foreach (var project in Solution.AllProjects.Where(p => p.Path.Parent.Contains("src")))
            {
                var claudeMd = project.Path.Parent / "CLAUDE.md";
                var exists = claudeMd.Exists();
                Log.Information("{Project}: {Status}",
                    project.Name,
                    exists ? "Has CLAUDE.md" : "Missing");
            }
        });

    private ProjectMetadata ScrapeProjectMetadata(Project project)
    {
        var csproj = XDocument.Load(project.Path);

        return new ProjectMetadata
        {
            Framework = csproj.Descendants("TargetFramework").FirstOrDefault()?.Value ?? "unknown",
            Layer = InferLayer(project),
            Workflow = InferWorkflow(project),
            TestCoverage = GetTestCoverage(project),
            Dependencies = GetDependencies(csproj)
        };
    }

    private string InferLayer(Project project)
    {
        var refs = project.GetItems("ProjectReference").Select(r => r.EvaluatedInclude);

        if (!refs.Any()) return "Leaf";
        if (refs.Any(r => r.Contains("protocol"))) return "Backend";
        return "Application";
    }

    private string InferWorkflow(Project project)
    {
        var srcDir = project.Path.Parent;
        var patterns = new Dictionary<string, string>
        {
            ["OTLP"] = "OtlpExporter|TraceService",
            ["DuckDB"] = "DuckDbStore|DuckDbSchema",
            ["REST"] = "MapGet|MapPost|MinimalApi",
            ["SSE"] = "ServerSentEvents|SseHub",
            ["MCP"] = "McpServer|McpTool"
        };

        var found = new List<string>();
        foreach (var (name, pattern) in patterns)
        {
            if (srcDir.GlobFiles("**/*.cs").Any(f =>
                System.Text.RegularExpressions.Regex.IsMatch(f.ReadAllText(), pattern)))
            {
                found.Add(name);
            }
        }

        return string.Join(" -> ", found);
    }

    private int GetTestCoverage(Project project)
    {
        var coverageFile = RootDirectory / "artifacts" / "coverage" / $"{project.Name}.cobertura.xml";
        if (!coverageFile.Exists()) return 0;

        var doc = XDocument.Load(coverageFile);
        var lineRate = doc.Root?.Attribute("line-rate")?.Value;
        return lineRate is not null ? (int)(double.Parse(lineRate) * 100) : 0;
    }

    private IReadOnlyList<string> GetDependencies(XDocument csproj)
    {
        return csproj.Descendants("PackageReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => p is not null)
            .ToList()!;
    }

    private string GenerateClaudeMdContent(Project project, ProjectMetadata meta)
    {
        var sb = new StringBuilder();
        var rootPath = project.Path.Parent.GetRelativePathTo(RootDirectory / "CLAUDE.md");

        sb.AppendLine($"# {project.Name}");
        sb.AppendLine();
        sb.AppendLine($"@import \"{rootPath}\"");
        sb.AppendLine();
        sb.AppendLine("## Scope");
        sb.AppendLine();
        sb.AppendLine(GetProjectPurpose(project));
        sb.AppendLine();
        sb.AppendLine("## Project Info");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| Layer | {meta.Layer} |");
        sb.AppendLine($"| Framework | {meta.Framework} |");
        sb.AppendLine($"| Workflow | {meta.Workflow} |");
        sb.AppendLine($"| Test Coverage | {meta.TestCoverage}% |");

        // Add Critical Files, Anti-Patterns, Required Patterns from csproj items
        // ... (similar to existing MSBuild generation)

        return sb.ToString();
    }

    private string GetProjectPurpose(Project project)
    {
        // Read from ClaudePurpose MSBuild property or infer from project name
        return project.Name switch
        {
            "qyl.collector" => "Backend: OTLP receiver, DuckDB storage, REST/SSE APIs",
            "qyl.protocol" => "Shared types and contracts (LEAF - no dependencies)",
            "qyl.mcp" => "MCP server: Claude integration via stdio transport",
            "qyl.dashboard" => "React frontend: telemetry visualization",
            _ => ""
        };
    }
}

record ProjectMetadata
{
    public string Framework { get; init; } = "";
    public string Layer { get; init; } = "";
    public string Workflow { get; init; } = "";
    public int TestCoverage { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}
```

### Step 2: Add to Build.cs

```csharp
class Build : NukeBuild, IClaudeBrain
{
    // existing code...
}
```

---

## Test

```bash
# Generate all CLAUDE.md files
nuke GenerateClaudeMd

# Generate for single project
nuke GenerateClaudeMd --ClaudeBrainProject qyl.collector

# Show status
nuke ClaudeBrainInfo
```

**Verify:**

- [ ] Layer field populated correctly
- [ ] Workflow shows actual flow (OTLP -> DuckDB -> REST)
- [ ] Test coverage shows percentage from coverage report
- [ ] Critical files, anti-patterns preserved from csproj items

---

## Done When

- [ ] IClaudeBrain component created
- [ ] All src/ projects have accurate CLAUDE.md
- [ ] Coverage percentage auto-populated
- [ ] Workflow inferred from code analysis

---

*Template v3*
