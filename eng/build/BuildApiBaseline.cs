using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.Git;
using Fallout.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// The public API of the assemblies qyl ships, as a report that never fails the gate. It reads the
/// surface of each Release assembly, rewrites the <c>PublicAPI.txt</c> beside its project to it, so
/// <c>git diff</c> shows an API change next to the code that caused it, and counts the lines added and
/// removed against the file in HEAD. The count becomes the target's summary note ("+N -M changed" or
/// "unchanged"), each line goes to the log, and a release reads the diff to choose its version bump.
/// </summary>
interface IApiBaseline : IHasSourcePaths, IHasConfiguration
{
    /// <summary>The qyl tool, orchestration library and two libraries Qyl.Api.Sdk packs under lib/net10.0;
    /// the SDK project itself compiles nothing.</summary>
    AbsolutePath[] ApiReportProjects =>
    [
        CliPackProject,
        OrchestrationProject,
        PackagesDirectory / "Qyl.Api.Sdk" / "Qyl.Xml" / "Qyl.Xml.csproj",
        PackagesDirectory / "Qyl.Api.Sdk" / "Qyl.Api" / "Qyl.Api.csproj"
    ];

    Target ApiBaseline => d => d
        .Description("Public API report: rewrites PublicAPI.txt beside the qyl tool, Qyl.Orchestration, Qyl.Xml and Qyl.Api to their Release surface and notes the change against HEAD; never fails")
        .DependsOn<ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            var change = ApiBaselineReport.Run(RootDirectory, ApiReportProjects, Configuration.ToString().ToLowerInvariant());
            ReportSummary(_ => new() { ["api"] = change });
        });
}

static class ApiBaselineReport
{
    /// <summary>The line format of Roslyn's PublicApiAnalyzers: one line per type and member,
    /// property accessors instead of properties, nullable annotations included.</summary>
    private static readonly SymbolDisplayFormat Format = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeConstantValue,
        parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier);

    /// <param name="configuration">The lower-case configuration: the artifacts layout's pivot.</param>
    public static string Run(AbsolutePath root, IReadOnlyCollection<AbsolutePath> projects, string configuration)
    {
        var artifacts = root / "artifacts";
        List<(string Baseline, string[] Surface, string[] Committed)> reports;
        try
        {
            reports = projects
                .Select(project => (Name: project.NameWithoutExtension, Baseline: root.GetUnixRelativePathTo(project.Parent / "PublicAPI.txt").ToString()))
                .Select(project => (project.Baseline, Surface(artifacts, configuration, project.Name), Committed(root, project.Baseline)))
                .ToList();
        }
        catch (Exception exception) // a report: an unreadable surface is a note, never a failed gate
        {
            Log.Warning("public API not computed: {Message}", exception.Message);
            return "not computed, see log";
        }

        int added = 0, removed = 0;
        foreach (var (baseline, surface, committed) in reports)
        {
            foreach (var line in committed.Except(surface, StringComparer.Ordinal))
            {
                Log.Information("api {File} - {Line}", baseline, line);
                removed++;
            }

            foreach (var line in surface.Except(committed, StringComparer.Ordinal))
            {
                Log.Information("api {File} + {Line}", baseline, line);
                added++;
            }

            var path = root / baseline;
            var text = string.Join('\n', surface) + "\n";
            if (!path.FileExists() || File.ReadAllText(path) != text)
                File.WriteAllText(path, text);
        }

        return added + removed == 0 ? "unchanged" : $"+{added} -{removed} changed";
    }

    private static string[] Committed(AbsolutePath root, string baseline)
    {
        var exitCode = 0;
        var output = GitTasks.Git($"show HEAD:{baseline}", root, logOutput: false, logInvocation: false,
            exitHandler: process => exitCode = process.ExitCode);
        return exitCode == 0
            ? output.Where(static line => line.Type == OutputType.Std).Select(static line => line.Text.Trim()).Where(static line => line.Length > 0).ToArray()
            : [];
    }

    private static string BinAssembly(AbsolutePath artifacts, string configuration, string project) =>
        artifacts / "bin" / project / configuration / $"{project}.dll";

    /// <summary>The public surface of the project's Release assembly, sorted by name so a type's
    /// members stay together whatever their modifiers.</summary>
    private static string[] Surface(AbsolutePath artifacts, string configuration, string project)
    {
        var assembly = MetadataReference.CreateFromFile(BinAssembly(artifacts, configuration, project));
        var compilation = CSharpCompilation.Create("public-api",
            references: [assembly, .. References(artifacts, configuration, project).Select(static path => MetadataReference.CreateFromFile(path))]);
        var symbol = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(assembly)!;

        var resolved = compilation.References.Select(compilation.GetAssemblyOrModuleSymbol).OfType<IAssemblySymbol>().Select(static reference => reference.Identity.Name);
        var unresolved = symbol.Modules.Single().ReferencedAssemblies.Select(static identity => identity.Name).Except(resolved, StringComparer.Ordinal).ToArray();
        if (unresolved.Length > 0)
            throw new InvalidOperationException($"{project}: unresolved references {string.Join(", ", unresolved)}");

        var lines = new List<string>();
        Collect(symbol.GlobalNamespace, lines);
        return lines.Distinct(StringComparer.Ordinal).OrderBy(SortKey, StringComparer.Ordinal).ThenBy(static line => line, StringComparer.Ordinal).ToArray();
    }

    private static void Collect(INamespaceOrTypeSymbol container, List<string> lines)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol nested)
            {
                Collect(nested, lines);
                continue;
            }

            // Roslyn gives every value type read from metadata a parameterless constructor; an enum's is
            // no API.
            if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)
                || member is IPropertySymbol
                    or IMethodSymbol { MethodKind: MethodKind.EventAdd or MethodKind.EventRemove }
                    or IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType.TypeKind: TypeKind.Enum })
                continue;

            var type = member switch { IMethodSymbol method => method.ReturnType, IFieldSymbol field => field.Type, IEventSymbol @event => @event.Type, _ => null };
            lines.Add(type is null ? member.ToDisplayString(Format) : $"{member.ToDisplayString(Format)} -> {type.ToDisplayString(Format)}");
            if (member is INamedTypeSymbol namedType)
                Collect(namedType, lines);
        }
    }

    /// <summary>The line without its leading modifiers (static, const, abstract, ...).</summary>
    private static string SortKey(string line)
    {
        var words = line.Split(' ');
        var first = Array.FindIndex(words, static word => !word.All(char.IsAsciiLetterLower));
        return string.Join(' ', words[Math.Max(first, 0)..]);
    }

    /// <summary>What the compiler referenced, from the project's project.assets.json: the compile assets
    /// of every package, the Release assemblies of the referenced projects and the targeting packs of
    /// the framework references. The RID-less target is the one the assembly was compiled for.</summary>
    private static List<string> References(AbsolutePath artifacts, string configuration, string project)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(artifacts / "obj" / project / "project.assets.json"));
        var root = assets.RootElement;
        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(static folder => folder.Name).ToArray();
        var target = root.GetProperty("targets").EnumerateObject().Single(static target => !target.Name.Contains('/', StringComparison.Ordinal));
        var references = new List<string>();
        foreach (var library in target.Value.EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() == "project")
            {
                references.Add(BinAssembly(artifacts, configuration, library.Name.Split('/')[0]));
                continue;
            }

            if (!library.Value.TryGetProperty("compile", out var compile))
                continue;
            var path = root.GetProperty("libraries").GetProperty(library.Name).GetProperty("path").GetString()!;
            foreach (var asset in compile.EnumerateObject().Select(static asset => asset.Name).Where(static asset => !asset.EndsWith("/_._", StringComparison.Ordinal)))
                references.Add(folders.Select(folder => Path.Combine(folder, path, asset)).First(File.Exists));
        }

        // The SDK this build runs on holds the targeting packs; the runtime's own directory is
        // <dotnet>/shared/Microsoft.NETCore.App/<version>/.
        var dotnet = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));
        var frameworks = root.GetProperty("project").GetProperty("frameworks").GetProperty(target.Name).TryGetProperty("frameworkReferences", out var listed)
            ? listed.EnumerateObject().Select(static framework => framework.Name).ToArray()
            : [];
        foreach (var framework in frameworks.Append("Microsoft.NETCore.App").Distinct(StringComparer.Ordinal))
        {
            var pack = Directory.GetDirectories(Path.Combine(dotnet, "packs", framework + ".Ref"))
                .Where(version => Directory.Exists(Path.Combine(version, "ref", target.Name)))
                .MaxBy(static version => Version.Parse(Path.GetFileName(version).Split('-')[0]))
                ?? throw new InvalidOperationException($"no {framework}.Ref targeting pack for {target.Name} under {dotnet}");
            references.AddRange(Directory.GetFiles(Path.Combine(pack, "ref", target.Name), "*.dll"));
        }

        return references;
    }
}
