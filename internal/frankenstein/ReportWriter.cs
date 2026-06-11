using System.Text;

namespace Qyl.Frankenstein;

internal static class ReportWriter
{
    public static string WriteDoctorConsole(DoctorResult result)
    {
        var inspection = result.Inspection;
        var builder = new StringBuilder();
        builder.AppendLine(inspection.Broken ? "STATUS: BROKEN" : "STATUS: HEALTHY");
        builder.AppendLine();
        builder.AppendLine("Package:");
        builder.AppendLine($"  root: {Rel(inspection.PackageRoot)}");
        builder.AppendLine($"  pet.json: {(inspection.PetJsonFound ? "found" : "missing")}");
        builder.AppendLine($"  spritesheet.webp: {(inspection.SpritesheetFound ? "found" : "missing")}");
        builder.AppendLine();

        if (inspection.Atlas is not null)
        {
            builder.AppendLine("Contract:");
            builder.AppendLine($"  target: {inspection.Target}");
            builder.AppendLine($"  atlas: {inspection.Atlas.Columns}x{inspection.Atlas.Rows}");
            builder.AppendLine($"  cell: {inspection.Atlas.CellWidth}x{inspection.Atlas.CellHeight}");
            builder.AppendLine();
        }

        if (inspection.Asset is not null && inspection.Atlas is not null)
        {
            builder.AppendLine("Asset:");
            builder.AppendLine($"  actual spritesheet: {inspection.Asset.Width}x{inspection.Asset.Height}");
            builder.AppendLine($"  expected spritesheet: {inspection.Atlas.ExpectedWidth}x{inspection.Atlas.ExpectedHeight}");
            builder.AppendLine($"  grid cells: {inspection.Atlas.GridCells}");
            builder.AppendLine();
        }

        if (inspection.Errors.Count > 0)
        {
            foreach (var issue in inspection.Errors)
            {
                builder.AppendLine("Failure:");
                builder.AppendLine($"  {DescribeFailure(issue)}");
                builder.AppendLine();
                builder.AppendLine("Owner:");
                builder.AppendLine($"  {issue.Owner}");
                builder.AppendLine();
                if (issue.Proposed is not null)
                {
                    builder.AppendLine("Recommended fix:");
                    builder.AppendLine($"  {DescribeFix(issue)}");
                    builder.AppendLine($"  reason: {issue.Reason}");
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine("Next:");
        builder.AppendLine($"  run frankenstein plan {Rel(result.SourcePath)} --target {result.Target}");
        return builder.ToString();
    }

    public static string WriteDoctorReport(DoctorResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Frankenstein Doctor: {Rel(result.SourcePath)}");
        builder.AppendLine();
        builder.AppendLine($"## Status");
        builder.AppendLine(result.Inspection.Broken ? "BROKEN" : "HEALTHY");
        builder.AppendLine();
        builder.AppendLine("## Source Safety");
        builder.AppendLine($"- source hash before: `{result.SourceHashBefore}`");
        builder.AppendLine($"- source hash after: `{result.SourceHashAfter}`");
        builder.AppendLine($"- source mutated: `{YesNo(!string.Equals(result.SourceHashBefore, result.SourceHashAfter, StringComparison.Ordinal))}`");
        builder.AppendLine($"- quarantine: `{Rel(result.QuarantinePath)}`");
        builder.AppendLine();
        AppendProblems(builder, result);
        return builder.ToString();
    }

    public static string WriteRepairPlan(DoctorResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Repair Plan: {Rel(result.SourcePath)}");
        builder.AppendLine();
        builder.AppendLine("## Status");
        builder.AppendLine(result.Repairable ? "REPAIRABLE" : result.Inspection.Broken ? "NOT REPAIRABLE" : "NO REPAIR NEEDED");
        builder.AppendLine();
        builder.AppendLine("## Source Safety");
        builder.AppendLine("The source package will not be modified.");
        builder.AppendLine();
        AppendProblems(builder, result);
        builder.AppendLine("## Proposed Output");
        builder.AppendLine();
        builder.AppendLine("```txt");
        builder.AppendLine($".tmp/frankenstein/{Path.GetFileNameWithoutExtension(result.SourcePath)}-repaired/");
        builder.AppendLine("  pet.json");
        builder.AppendLine("  spritesheet.webp");
        builder.AppendLine("  frankenstein-repair-manifest.json");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Verification After Repair");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine($"frankenstein validate .tmp/frankenstein/{Path.GetFileNameWithoutExtension(result.SourcePath)}-repaired --target {result.Target}");
        builder.AppendLine($"frankenstein import .tmp/frankenstein/{Path.GetFileNameWithoutExtension(result.SourcePath)}-repaired --out .tmp/frankenstein/imported.json");
        builder.AppendLine($"frankenstein diff-normalized {Rel(result.SourcePath)} .tmp/frankenstein/{Path.GetFileNameWithoutExtension(result.SourcePath)}-repaired");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Requires Approval");
        builder.AppendLine();
        builder.AppendLine("YES");
        return builder.ToString();
    }

    public static string WriteValidationConsole(ValidationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Valid ? "VALIDATION: PASS" : "VALIDATION: FAILED");
        builder.AppendLine($"target: {result.Target}");
        if (result.Errors.Count > 0)
        {
            builder.AppendLine("errors:");
            foreach (var error in result.Errors)
            {
                builder.AppendLine($"  - {error}");
            }
        }

        return builder.ToString();
    }

    public static string WriteRoundTripConsole(RoundTripResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Pass ? "ROUNDTRIP: PASS" : "ROUNDTRIP: FAILED");
        builder.AppendLine();
        builder.AppendLine($"imported package valid: {YesNo(result.SourceValidation.Valid)}");
        builder.AppendLine($"exported package valid: {YesNo(result.ExportedValidation.Valid)}");
        builder.AppendLine($"re-imported package valid: {YesNo(result.ReimportedValidation.Valid)}");
        builder.AppendLine($"normalized diff: {(result.Diff.IsEmpty ? "empty" : "changed")}");
        builder.AppendLine($"source mutated: {YesNo(result.SourceMutated)}");
        return builder.ToString();
    }

    public static string WriteAbilitiesConsole(AbilityCheckResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Valid ? "ABILITIES: PASS" : "ABILITIES: FAILED");
        builder.AppendLine($"target: {result.Target}");
        foreach (var error in result.Errors)
        {
            builder.AppendLine($"  - {error}");
        }

        return builder.ToString();
    }

    public static string WriteAtlasConsole(AtlasInspection result, bool includeStates)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Valid ? "ATLAS: PASS" : "ATLAS: FAILED");
        if (result.Atlas is not null)
        {
            builder.AppendLine($"atlas: {result.Atlas.Columns}x{result.Atlas.Rows}");
            builder.AppendLine($"cell: {result.Atlas.CellWidth}x{result.Atlas.CellHeight}");
        }

        if (result.Image is not null)
        {
            builder.AppendLine($"spritesheet: {result.Image.Width}x{result.Image.Height}");
        }

        if (includeStates)
        {
            builder.AppendLine("states:");
            foreach (var (name, animation) in result.Animations)
            {
                builder.AppendLine($"  {name}: row {animation.Row}, frames {animation.Frames}");
            }
        }

        if (result.Rows.Count > 0)
        {
            builder.AppendLine("row evidence:");
            foreach (var row in result.Rows)
            {
                builder.AppendLine($"  row {row.Row}: {row.NonEmptyFrames} non-empty frames");
            }
        }

        return builder.ToString();
    }

    public static string WriteFinalReport(RoundTripResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Frankenstein Final Report");
        builder.AppendLine();
        builder.AppendLine("## Status");
        builder.AppendLine(result.Pass ? "DONE" : "PARTIAL");
        builder.AppendLine();
        builder.AppendLine("## Package");
        builder.AppendLine($"- source: `{Rel(result.SourcePath)}`");
        builder.AppendLine($"- target: `{result.Target}`");
        builder.AppendLine();
        builder.AppendLine("## Evidence");
        builder.AppendLine($"- imported package valid: `{YesNo(result.SourceValidation.Valid)}`");
        builder.AppendLine($"- exported package valid: `{YesNo(result.ExportedValidation.Valid)}`");
        builder.AppendLine($"- re-imported package valid: `{YesNo(result.ReimportedValidation.Valid)}`");
        builder.AppendLine($"- normalized diff: `{(result.Diff.IsEmpty ? "empty" : "changed")}`");
        builder.AppendLine($"- source mutated: `{YesNo(result.SourceMutated)}`");
        builder.AppendLine();
        builder.AppendLine("## Artifacts");
        builder.AppendLine($"- import: `{Rel(result.ImportedPath)}`");
        builder.AppendLine($"- export: `{Rel(result.ExportedPath)}`");
        builder.AppendLine($"- re-import: `{Rel(result.ReimportedPath)}`");
        builder.AppendLine();
        builder.AppendLine("## Verification Commands");
        builder.AppendLine("```bash");
        builder.AppendLine($"frankenstein validate {Rel(result.SourcePath)} --target {result.Target}");
        builder.AppendLine($"frankenstein roundtrip {Rel(result.SourcePath)} --target {result.Target}");
        builder.AppendLine("```");
        return builder.ToString();
    }

    public static void WriteAgentReports(string reportDirectory, RoundTripResult result)
    {
        Directory.CreateDirectory(reportDirectory);
        WriteAgent(reportDirectory, "igor", "intake, quarantine, hashes, source safety", result.SourceMutated ? "source mutation detected" : "source hash unchanged", result.Pass);
        WriteAgent(reportDirectory, "victor", "contract model, pet.json, schema, target abstraction", result.SourceValidation.Valid ? "contract validates for source package" : string.Join("; ", result.SourceValidation.Errors), result.SourceValidation.Valid);
        WriteAgent(reportDirectory, "atlas", "spritesheet, dimensions, grid, frame coordinates", result.ExportedValidation.Checks.SpritesheetDimensionsMatch ? "spritesheet dimensions match atlas geometry" : "spritesheet dimensions mismatch", result.ExportedValidation.Checks.SpritesheetDimensionsMatch);
        WriteAgent(reportDirectory, "prometheus", "ability system, positive traits, workflow effects", "ability metadata survived normalized import/export when present", result.Diff.IsEmpty);
        WriteAgent(reportDirectory, "hermes", "target adapters, Codex export, generic-agent export", result.ExportedValidation.Valid ? "target export validates" : string.Join("; ", result.ExportedValidation.Errors), result.ExportedValidation.Valid);
        WriteAgent(reportDirectory, "maat", "validation, evidence, final pass/fail", result.Pass ? "round-trip proof passed" : "round-trip proof failed", result.Pass);
        WriteAgent(reportDirectory, "janitor", "temp files, duplicate state, dead code, unsafe writes", result.SourceMutated ? "unsafe source write detected" : "source package was not mutated", !result.SourceMutated);
        WriteAgent(reportDirectory, "scribe", "final report", "wrote reports/frankenstein-final.md", result.Pass);
    }

    private static void AppendProblems(StringBuilder builder, DoctorResult result)
    {
        builder.AppendLine("## Problems Found");
        builder.AppendLine();

        if (result.Inspection.Errors.Count is 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        for (var index = 0; index < result.Inspection.Errors.Count; index++)
        {
            var issue = result.Inspection.Errors[index];
            builder.AppendLine($"### {index + 1}. {issue.Title}");
            builder.AppendLine($"- file: {issue.File}");
            if (issue.Path.Length > 0)
            {
                builder.AppendLine($"- field: {issue.Path}");
            }

            builder.AppendLine($"- current: {issue.Current}");
            builder.AppendLine($"- expected: {issue.Expected}");
            if (issue.Proposed is not null)
            {
                builder.AppendLine($"- proposed: {issue.Proposed}");
            }

            builder.AppendLine($"- owner: {issue.Owner}");
            builder.AppendLine();
        }
    }

    private static string DescribeFailure(PetIssue issue)
    {
        if (issue.Path.StartsWith("animations.", StringComparison.Ordinal))
        {
            var animation = issue.Path.Split('.')[1];
            return $"animation \"{animation}\" points to row {issue.Current}; valid row range is {issue.Expected}";
        }

        return $"{issue.Path} is {issue.Current}; expected {issue.Expected}";
    }

    private static string DescribeFix(PetIssue issue)
    {
        if (issue.Path.StartsWith("animations.", StringComparison.Ordinal))
        {
            var animation = issue.Path.Split('.')[1];
            return $"move \"{animation}\" from row {issue.Current} to row {issue.Proposed}";
        }

        return $"move {issue.Path} from {issue.Current} to {issue.Proposed}";
    }

    private static void WriteAgent(string directory, string agent, string scope, string confirmed, bool done)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Agent Report: {agent}");
        builder.AppendLine();
        builder.AppendLine("## Scope");
        builder.AppendLine(scope);
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        builder.AppendLine("Frankenstein normalized import, validation output, and round-trip diff.");
        builder.AppendLine();
        builder.AppendLine("## Confirmed");
        builder.AppendLine(confirmed);
        builder.AppendLine();
        builder.AppendLine("## Problems");
        builder.AppendLine(done ? "None." : "See final report.");
        builder.AppendLine();
        builder.AppendLine("## Proposed Fixes");
        builder.AppendLine(done ? "None." : "Repair the failing layer identified in the final report.");
        builder.AppendLine();
        builder.AppendLine("## Unsafe Assumptions");
        builder.AppendLine("None recorded.");
        builder.AppendLine();
        builder.AppendLine("## Done Status");
        builder.AppendLine(done ? "DONE" : "PARTIAL");
        File.WriteAllText(Path.Combine(directory, $"{agent}.md"), builder.ToString(), TextEncodings.Utf8NoBom);
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Rel(string path) => Path.GetRelativePath(Environment.CurrentDirectory, path);
}
