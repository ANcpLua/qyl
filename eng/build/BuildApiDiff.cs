// =============================================================================
// qyl Build System - OpenAPI Schema Diff
// =============================================================================
// Detects API contract changes between the current TypeSpec-generated schema
// and a git baseline. Equivalent to Sentry's openapi-diff.ts — pure C#.
//
// What it compares:
//   paths          : added / removed endpoints
//   operations     : added / removed HTTP methods per endpoint
//   components     : added / removed schema models
//   schema fields  : added / removed properties and required-set changes
//
// Change classification:
//   BREAKING     : removed path, removed operation, removed property,
//                  required property added to request (new mandatory input)
//   NON-BREAKING : added path, added operation, added property
//
// Usage:
//   nuke ApiDiff                                          # diff vs. HEAD
//   nuke ApiDiff --iapidiff-base-ref main                 # diff vs. main
//   nuke ApiDiff --iapidiff-fail-on-breaking              # exit 1 if breaking
//   nuke VerifyApiUnchanged                               # CI gate: schema committed?
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Serilog;
using YamlDotNet.RepresentationModel;

// ════════════════════════════════════════════════════════════════════════════════
// IApiDiff - OpenAPI Schema Diff Interface
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Semantic diff between the current TypeSpec-generated OpenAPI schema and a
///     git baseline. Mirrors Sentry's <c>openapi-diff.ts</c> workflow, adapted
///     for qyl's NUKE build and YamlDotNet.
/// </summary>
[ParameterPrefix(nameof(IApiDiff))]
interface IApiDiff : IHazSourcePaths
{
    [Parameter("Git ref to compare against (default: HEAD)")]
    string? BaseRef => TryGetValue<string?>(() => BaseRef) ?? "HEAD";

    [Parameter("Fail the build when breaking changes are detected (default: true on CI)")]
    bool? FailOnBreaking => TryGetValue<bool?>(() => FailOnBreaking);

    AbsolutePath OpenApiSpec => RootDirectory / "core" / "openapi" / "openapi.yaml";

    // ════════════════════════════════════════════════════════════════════════
    // Targets
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Diff the current <c>openapi.yaml</c> against a git baseline.
    ///     Classifies each change as BREAKING or NON-BREAKING and reports a summary.
    /// </summary>
    Target ApiDiff => d => d
        .Description("Diff current OpenAPI schema vs. git baseline — classifies BREAKING / NON-BREAKING changes")
        .Executes(() =>
        {
            if (!OpenApiSpec.FileExists())
            {
                Log.Warning("openapi.yaml not found at {Path}. Run 'nuke TypeSpecCompile' first.", OpenApiSpec);
                return;
            }

            var baseRef = BaseRef ?? "HEAD";
            var relPath = RootDirectory.GetRelativePathTo(OpenApiSpec).ToString().Replace('\\', '/');

            var baselineYaml = TryReadFromGit(baseRef, relPath);
            if (baselineYaml is null)
            {
                Log.Information("No baseline found at git ref '{Ref}'. Nothing to compare.", baseRef);
                return;
            }

            var baseline = OpenApiDiffer.Parse(baselineYaml);
            var current  = OpenApiDiffer.Parse(File.ReadAllText(OpenApiSpec));
            var result   = OpenApiDiffer.Diff(baseline, current);

            OpenApiDiffReport.Print(result, baseRef);

            bool failOnBreaking = FailOnBreaking ?? IsServerBuild;
            if (result.HasBreaking && failOnBreaking)
                throw new InvalidOperationException(
                    $"{result.Breaking.Count} breaking API change(s) detected (see diff above). " +
                    "If intentional, update the schema version and document the change.");
        });

    /// <summary>
    ///     CI gate: regenerates the schema, then verifies that the result matches
    ///     the <c>openapi.yaml</c> already committed in git HEAD.
    ///     Fails if the TypeSpec source was changed but the schema was not regenerated
    ///     before committing.
    /// </summary>
    Target VerifyApiUnchanged => d => d
        .Description("Verify openapi.yaml matches HEAD (CI gate — catches unregistered schema drift)")
        .DependsOn<IPipeline>(static x => x.TypeSpecCompile)
        .Executes(() =>
        {
            var relPath = RootDirectory
                .GetRelativePathTo(OpenApiSpec).ToString().Replace('\\', '/');

            // git diff --name-only exits 0 whether or not there are diffs — safe to call without try/catch
            IReadOnlyCollection<Output> diffOutput = GitTasks.Git(
                $"diff --name-only HEAD -- {relPath}",
                RootDirectory, logOutput: false, logInvocation: false);

            bool changed = diffOutput.Any(static o =>
                o.Text.Contains("openapi.yaml", StringComparison.OrdinalIgnoreCase));

            if (changed)
            {
                Log.Error("openapi.yaml changed after regeneration but is not committed to HEAD.");
                Log.Error("Regenerate the schema and commit it: nuke TypeSpecCompile");
                throw new InvalidOperationException("openapi.yaml is out of sync with HEAD.");
            }

            Log.Information("API contract: openapi.yaml matches HEAD");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    sealed string? TryReadFromGit(string gitRef, string path)
    {
        // ProcessException is thrown when git exits non-zero, which happens when the
        // path does not exist at that ref (new file not yet committed) — expected, return null.
        // Any other exception (git not found, corrupted repo, etc.) should propagate.
        try
        {
            var output = GitTasks.Git(
                $"show {gitRef}:{path}",
                RootDirectory, logOutput: false, logInvocation: false);
            var text = string.Join("\n", output.Select(static o => o.Text));
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (ProcessException ex)
        {
            Log.Debug("'{Path}' not found at git ref '{Ref}' (exit {Code}): {Msg}",
                path, gitRef, ex.ExitCode, ex.Message);
            return null;
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Change model
// ════════════════════════════════════════════════════════════════════════════════

internal enum ApiChangeKind
{
    PathAdded,
    PathRemoved,
    OperationAdded,
    OperationRemoved,
    SchemaAdded,
    SchemaRemoved,
    PropertyAdded,
    PropertyRemoved,
    RequiredAdded,   // property became required → breaking for request bodies
    RequiredRemoved, // property relaxed to optional → non-breaking
}

internal sealed record ApiChange(
    ApiChangeKind Kind,
    string Subject,       // path, schema name, or "SchemaName.fieldName"
    string? Detail,       // e.g. HTTP method
    bool IsBreaking);

internal sealed record OpenApiDiffResult(IReadOnlyList<ApiChange> Changes)
{
    public IReadOnlyList<ApiChange> Breaking    => Changes.Where(static c => c.IsBreaking).ToList();
    public IReadOnlyList<ApiChange> NonBreaking => Changes.Where(static c => !c.IsBreaking).ToList();
    public bool HasBreaking => Changes.Any(static c => c.IsBreaking);
}

// ════════════════════════════════════════════════════════════════════════════════
// Differ
// ════════════════════════════════════════════════════════════════════════════════

internal static class OpenApiDiffer
{
    private static readonly HashSet<string> HttpMethods =
        new(["get", "post", "put", "patch", "delete", "head", "options", "trace"],
            StringComparer.OrdinalIgnoreCase);

    // ── Parse ────────────────────────────────────────────────────────────────

    internal static YamlMappingNode Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    // ── Diff ─────────────────────────────────────────────────────────────────

    internal static OpenApiDiffResult Diff(YamlMappingNode baseline, YamlMappingNode current)
    {
        var changes = new List<ApiChange>();

        DiffPaths(
            GetPaths(baseline),
            GetPaths(current),
            changes);

        DiffSchemas(
            GetSchemas(baseline),
            GetSchemas(current),
            changes);

        return new OpenApiDiffResult(changes);
    }

    // ── Path-level diff ──────────────────────────────────────────────────────

    private static void DiffPaths(
        Dictionary<string, YamlMappingNode> baseline,
        Dictionary<string, YamlMappingNode> current,
        List<ApiChange> changes)
    {
        foreach (var path in baseline.Keys.Except(current.Keys, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.PathRemoved, path, null, IsBreaking: true));

        foreach (var path in current.Keys.Except(baseline.Keys, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.PathAdded, path, null, IsBreaking: false));

        // Shared paths: diff operations
        foreach (var path in baseline.Keys.Intersect(current.Keys, StringComparer.Ordinal))
        {
            DiffOperations(path,
                GetOperations(baseline[path]),
                GetOperations(current[path]),
                changes);
        }
    }

    private static void DiffOperations(
        string path,
        HashSet<string> baseline,
        HashSet<string> current,
        List<ApiChange> changes)
    {
        foreach (var method in baseline.Except(current, StringComparer.OrdinalIgnoreCase))
            changes.Add(new(ApiChangeKind.OperationRemoved, path,
                method.ToUpperInvariant(), IsBreaking: true));

        foreach (var method in current.Except(baseline, StringComparer.OrdinalIgnoreCase))
            changes.Add(new(ApiChangeKind.OperationAdded, path,
                method.ToUpperInvariant(), IsBreaking: false));
    }

    // ── Schema-level diff ────────────────────────────────────────────────────

    private static void DiffSchemas(
        Dictionary<string, YamlMappingNode> baseline,
        Dictionary<string, YamlMappingNode> current,
        List<ApiChange> changes)
    {
        foreach (var name in baseline.Keys.Except(current.Keys, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.SchemaRemoved, name, null, IsBreaking: true));

        foreach (var name in current.Keys.Except(baseline.Keys, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.SchemaAdded, name, null, IsBreaking: false));

        foreach (var name in baseline.Keys.Intersect(current.Keys, StringComparer.Ordinal))
            DiffSchemaFields(name, baseline[name], current[name], changes);
    }

    private static void DiffSchemaFields(
        string schemaName,
        YamlMappingNode baseline,
        YamlMappingNode current,
        List<ApiChange> changes)
    {
        var baseProps     = GetPropertyNames(baseline);
        var currProps     = GetPropertyNames(current);
        var baseRequired  = GetRequired(baseline);
        var currRequired  = GetRequired(current);

        // Properties removed → breaking (consumers depend on response fields)
        foreach (var prop in baseProps.Except(currProps, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.PropertyRemoved,
                $"{schemaName}.{prop}", null, IsBreaking: true));

        // Properties added → non-breaking
        foreach (var prop in currProps.Except(baseProps, StringComparer.Ordinal))
            changes.Add(new(ApiChangeKind.PropertyAdded,
                $"{schemaName}.{prop}", null, IsBreaking: false));

        // Required set tightened → breaking (callers must now supply the field)
        foreach (var prop in currRequired.Except(baseRequired, StringComparer.OrdinalIgnoreCase))
            changes.Add(new(ApiChangeKind.RequiredAdded,
                $"{schemaName}.{prop}", null, IsBreaking: true));

        // Required set relaxed → non-breaking
        foreach (var prop in baseRequired.Except(currRequired, StringComparer.OrdinalIgnoreCase))
            changes.Add(new(ApiChangeKind.RequiredRemoved,
                $"{schemaName}.{prop}", null, IsBreaking: false));
    }

    // ── Node helpers ─────────────────────────────────────────────────────────

    private static Dictionary<string, YamlMappingNode> GetPaths(YamlMappingNode root)
    {
        if (!root.Children.TryGetValue("paths", out var node) ||
            node is not YamlMappingNode pathsNode)
            return new();

        var result = new Dictionary<string, YamlMappingNode>(StringComparer.Ordinal);
        foreach (var (key, value) in pathsNode.Children)
        {
            if (key is YamlScalarNode keyScalar && keyScalar.Value is { } k &&
                value is YamlMappingNode v)
                result[k] = v;
        }
        return result;
    }

    private static Dictionary<string, YamlMappingNode> GetSchemas(YamlMappingNode root)
    {
        if (!root.Children.TryGetValue("components", out var comp) ||
            comp is not YamlMappingNode compNode)
            return new();

        if (!compNode.Children.TryGetValue("schemas", out var schemas) ||
            schemas is not YamlMappingNode schemasNode)
            return new();

        var result = new Dictionary<string, YamlMappingNode>(StringComparer.Ordinal);
        foreach (var (key, value) in schemasNode.Children)
        {
            if (key is YamlScalarNode keyScalar && keyScalar.Value is { } k &&
                value is YamlMappingNode v)
                result[k] = v;
        }
        return result;
    }

    private static HashSet<string> GetOperations(YamlMappingNode pathNode)
    {
        var ops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, _) in pathNode.Children)
        {
            if (key is YamlScalarNode { Value: { } method } &&
                HttpMethods.Contains(method))
                ops.Add(method);
        }
        return ops;
    }

    private static HashSet<string> GetPropertyNames(YamlMappingNode schema)
    {
        if (!schema.Children.TryGetValue("properties", out var props) ||
            props is not YamlMappingNode propsNode)
            return [];

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, _) in propsNode.Children)
        {
            if (key is YamlScalarNode { Value: { } name })
                result.Add(name);
        }
        return result;
    }

    private static HashSet<string> GetRequired(YamlMappingNode schema)
    {
        if (!schema.Children.TryGetValue("required", out var req) ||
            req is not YamlSequenceNode seqNode)
            return [];

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in seqNode.Children.OfType<YamlScalarNode>())
        {
            if (item.Value is { } v)
                result.Add(v);
        }
        return result;
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Reporter
// ════════════════════════════════════════════════════════════════════════════════

internal static class OpenApiDiffReport
{
    internal static void Print(OpenApiDiffResult result, string baseRef)
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("  OpenAPI Schema Diff vs. {Ref}", baseRef);
        Log.Information("═══════════════════════════════════════════════════════════════");

        if (result.Changes.Count == 0)
        {
            Log.Information("  No changes detected.");
            Log.Information("═══════════════════════════════════════════════════════════════");
            return;
        }

        if (result.Breaking.Count > 0)
        {
            Log.Warning("");
            Log.Warning("  ⚠  BREAKING ({Count}):", result.Breaking.Count);
            foreach (var c in result.Breaking)
                Log.Warning("    [-] {Kind,-22} {Subject}{Detail}",
                    c.Kind, c.Subject, c.Detail is null ? "" : $"  [{c.Detail}]");
        }

        if (result.NonBreaking.Count > 0)
        {
            Log.Information("");
            Log.Information("  +  NON-BREAKING ({Count}):", result.NonBreaking.Count);
            foreach (var c in result.NonBreaking)
                Log.Information("    [+] {Kind,-22} {Subject}{Detail}",
                    c.Kind, c.Subject, c.Detail is null ? "" : $"  [{c.Detail}]");
        }

        Log.Information("");
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("  Total: {Total} change(s) — {B} breaking, {NB} non-breaking",
            result.Changes.Count, result.Breaking.Count, result.NonBreaking.Count);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }
}
