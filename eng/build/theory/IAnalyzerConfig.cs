// using System;
// using System.Collections.Generic;
// using System.Globalization;
// using System.IO;
// using System.Linq;
// using System.Reflection;
// using System.Runtime.Loader;
// using System.Text;
// using System.Text.RegularExpressions;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.Diagnostics;
// using Nuke.Common;
// using Nuke.Common.IO;
// using Nuke.Common.Tooling;
// using NuGet.Common;
// using NuGet.Configuration;
// using NuGet.Frameworks;
// using NuGet.Packaging.Core;
// using NuGet.Packaging.Signing;
// using NuGet.Protocol;
// using NuGet.Protocol.Core.Types;
// using NuGet.Versioning;
// using Serilog;
//
// namespace Components.Theory;
//
// /// <summary>
// /// Analyzer configuration component for generating EditorConfig files from NuGet analyzer packages.
// /// Based on Meziantou's pattern for comprehensive analyzer rule management.
// ///
// /// Features:
// /// - Scans project for referenced analyzer packages
// /// - Downloads and inspects analyzer assemblies
// /// - Generates per-package EditorConfig files with all rules
// /// - Generates BannedSymbols.txt for deprecated APIs
// /// - Preserves existing severity overrides on regeneration
// ///
// /// NUKE 10.1.0 patterns:
// /// - ParameterPrefix for namespacing
// /// - Async target convenience (runs synchronously behind the scenes)
// /// - TryDependsOn for loose coupling with other components
// /// </summary>
// [ParameterPrefix(nameof(IAnalyzerConfig))]
// internal interface IAnalyzerConfig : IHasSolution
// {
//     // ════════════════════════════════════════════════════════════════════════
//     // Paths
//     // ════════════════════════════════════════════════════════════════════════
//
//     AbsolutePath ConfigurationDirectory => EngConfigDotNetDirectory;
//
//     AbsolutePath BannedSymbolsFile => ConfigurationDirectory / "BannedSymbols.txt";
//
//     AbsolutePath NewtonsoftBannedSymbolsFile => ConfigurationDirectory / "BannedSymbols.NewtonsoftJson.txt";
//
//     // ════════════════════════════════════════════════════════════════════════
//     // Parameters
//     // ════════════════════════════════════════════════════════════════════════
//
//     [Parameter("Target framework for analyzer compatibility")]
//     string TargetFramework => TryGetValue(() => TargetFramework) ?? "net10.0";
//
//     [Parameter("Additional analyzer packages to include (comma-separated)")]
//     string AdditionalAnalyzers => TryGetValue(() => AdditionalAnalyzers);
//
//     [Parameter("Generate banned symbols for Newtonsoft.Json")]
//     bool BanNewtonsoftJson => TryGetValue(() => BanNewtonsoftJson) ?? true;
//
//     // ════════════════════════════════════════════════════════════════════════
//     // Targets
//     // ════════════════════════════════════════════════════════════════════════
//
//     Target GenerateAnalyzerConfigs => d => d
//         .Description("Generate EditorConfig files for all referenced analyzer packages")
//         .Executes(async () =>
//         {
//             Log.Information("Generating analyzer EditorConfig files...");
//             ConfigurationDirectory.CreateDirectory();
//
//             var packages = await GetAllReferencedNuGetPackages();
//             var writtenFiles = 0;
//
//             await Parallel.ForEachAsync(packages, async (item, ct) =>
//             {
//                 var (packageId, packageVersion) = item;
//                 Log.Debug("Processing {Package}@{Version}", packageId, packageVersion);
//
//                 var configPath = ConfigurationDirectory / $"Analyzer.{packageId}.editorconfig";
//                 var rules = new HashSet<AnalyzerRule>();
//
//                 foreach (var assembly in await GetAnalyzerReferences(packageId, packageVersion))
//                 {
//                     foreach (var type in assembly.GetTypes())
//                     {
//                         if (type.IsAbstract || type.IsInterface)
//                             continue;
//
//                         if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
//                             continue;
//
//                         try
//                         {
//                             var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
//                             foreach (var diagnostic in analyzer.SupportedDiagnostics)
//                             {
//                                 rules.Add(new AnalyzerRule(
//                                     diagnostic.Id,
//                                     diagnostic.Title.ToString(CultureInfo.InvariantCulture).Trim(),
//                                     diagnostic.HelpLinkUri,
//                                     diagnostic.IsEnabledByDefault,
//                                     diagnostic.DefaultSeverity,
//                                     diagnostic.IsEnabledByDefault ? diagnostic.DefaultSeverity : null));
//                             }
//                         }
//                         catch (Exception ex)
//                         {
//                             Log.Warning("Failed to instantiate analyzer {Type}: {Error}", type.Name, ex.Message);
//                         }
//                     }
//                 }
//
//                 if (rules.Count > 0)
//                 {
//                     var content = GenerateEditorConfigContent(configPath, rules);
//                     if (ShouldWriteFile(configPath, content))
//                     {
//                         await File.WriteAllTextAsync(configPath, content, ct);
//                         Interlocked.Increment(ref writtenFiles);
//                         Log.Information("  Generated: {File} ({Count} rules)", configPath.Name, rules.Count);
//                     }
//                 }
//             });
//
//             Log.Information("Generated {Count} analyzer configuration files", writtenFiles);
//         });
//
//     Target GenerateBannedSymbols => d => d
//         .Description("Generate BannedSymbols.txt for deprecated APIs (e.g., Newtonsoft.Json)")
//         .OnlyWhenStatic(() => BanNewtonsoftJson)
//         .Executes(async () =>
//         {
//             Log.Information("Generating banned symbols file...");
//             ConfigurationDirectory.CreateDirectory();
//
//             var bannedSymbols = new HashSet<string>(StringComparer.Ordinal);
//
//             var package = await DownloadNuGetPackage("Newtonsoft.Json", null, NullLogger.Instance, CancellationToken.None);
//             var libItems = await package.PackageReader.GetLibItemsAsync(CancellationToken.None);
//
//             var framework = NuGetFramework.Parse(TargetFramework);
//             var compatibleFrameworks = libItems
//                 .Where(item => DefaultCompatibilityProvider.Instance.IsCompatible(framework, item.TargetFramework));
//
//             var items = compatibleFrameworks.SelectMany(item => item.Items).ToArray();
//
//             foreach (var item in items)
//             {
//                 if (!string.Equals(Path.GetExtension(item), ".dll", StringComparison.OrdinalIgnoreCase))
//                     continue;
//
//                 await using var stream = package.PackageReader.GetStream(item);
//                 var namespaces = ExtractNamespaces(stream, "Newtonsoft.Json.JsonConvert");
//
//                 foreach (var ns in namespaces.Where(n => n.StartsWith("N:Newtonsoft", StringComparison.Ordinal)))
//                 {
//                     bannedSymbols.Add(ns);
//                 }
//             }
//
//             var sb = new StringBuilder();
//             sb.AppendLine("# Banned symbols from Newtonsoft.Json");
//             sb.AppendLine("# Prefer System.Text.Json for JSON serialization");
//             sb.AppendLine();
//             foreach (var symbol in bannedSymbols.OrderBy(s => s, StringComparer.Ordinal))
//             {
//                 sb.AppendLine(symbol);
//             }
//
//             var content = sb.ToString().ReplaceLineEndings("\n");
//             if (ShouldWriteFile(NewtonsoftBannedSymbolsFile, content))
//             {
//                 await File.WriteAllTextAsync(NewtonsoftBannedSymbolsFile, content);
//                 Log.Information("Generated: {File} ({Count} banned namespaces)",
//                     NewtonsoftBannedSymbolsFile.Name, bannedSymbols.Count);
//             }
//         });
//
//     Target AnalyzerConfigAll => d => d
//         .Description("Generate all analyzer configuration files")
//         .DependsOn<IAnalyzerConfig>(x => x.GenerateAnalyzerConfigs)
//         .DependsOn<IAnalyzerConfig>(x => x.GenerateBannedSymbols)
//         .Executes(() =>
//         {
//             Log.Information("All analyzer configuration files generated");
//         });
//
//     Target AnalyzerConfigInfo => d => d
//         .Description("Show analyzer configuration status")
//         .Executes(() =>
//         {
//             Log.Information("═══════════════════════════════════════════════════════════════");
//             Log.Information("  Analyzer Configuration Status");
//             Log.Information("═══════════════════════════════════════════════════════════════");
//             Log.Information("  Configuration Directory: {Path}", ConfigurationDirectory);
//             Log.Information("  Target Framework: {Fw}", TargetFramework);
//
//             if (ConfigurationDirectory.DirectoryExists())
//             {
//                 var editorConfigs = ConfigurationDirectory.GlobFiles("Analyzer.*.editorconfig");
//                 Log.Information("  EditorConfig Files: {Count}", editorConfigs.Count);
//                 foreach (var file in editorConfigs.OrderBy(f => f.Name))
//                 {
//                     Log.Information("    - {Name}", file.Name);
//                 }
//
//                 if (NewtonsoftBannedSymbolsFile.FileExists())
//                 {
//                     var lines = File.ReadAllLines(NewtonsoftBannedSymbolsFile)
//                         .Count(l => !l.StartsWith('#') && !string.IsNullOrWhiteSpace(l));
//                     Log.Information("  Banned Symbols (Newtonsoft): {Count} namespaces", lines);
//                 }
//             }
//             else
//             {
//                 Log.Warning("  Configuration directory does not exist");
//             }
//
//             Log.Information("═══════════════════════════════════════════════════════════════");
//         });
//
//     // ════════════════════════════════════════════════════════════════════════
//     // Private Helpers
//     // ════════════════════════════════════════════════════════════════════════
//
//     private async Task<(string Id, NuGetVersion Version)[]> GetAllReferencedNuGetPackages()
//     {
//         var foundPackages = new HashSet<SourcePackageDependencyInfo>();
//
//         var cache = new SourceCacheContext();
//         var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
//         var resource = await repository.GetResourceAsync<PackageMetadataResource>();
//
//         // Get packages from Directory.Packages.props or scan csproj files
//         var packages = await ScanForAnalyzerPackages();
//
//         // Add SDK analyzers
//         var sdkAnalyzers = new[] { "Microsoft.CodeAnalysis.NetAnalyzers" };
//         if (!string.IsNullOrEmpty(AdditionalAnalyzers))
//         {
//             sdkAnalyzers = [.. sdkAnalyzers, .. AdditionalAnalyzers.Split(',').Select(s => s.Trim())];
//         }
//
//         foreach (var packageId in sdkAnalyzers)
//         {
//             packages.Add((packageId, null));
//         }
//
//         var framework = NuGetFramework.Parse(TargetFramework);
//
//         foreach (var (packageId, versionStr) in packages)
//         {
//             var version = versionStr is null ? null : NuGetVersion.Parse(versionStr);
//             if (version is null)
//             {
//                 var metadata = await resource.GetMetadataAsync(
//                     packageId, includePrerelease: true, includeUnlisted: false,
//                     cache, NullLogger.Instance, CancellationToken.None);
//                 version = metadata.MaxBy(m => m.Identity.Version)?.Identity.Version;
//                 if (version is null) continue;
//             }
//
//             var packageIdentity = new PackageIdentity(packageId, version);
//             await CollectDependencies(packageIdentity, [repository], framework, cache, foundPackages);
//         }
//
//         return [.. foundPackages.Select(p => (p.Id, p.Version))];
//     }
//
//     private async Task<HashSet<(string Id, string? Version)>> ScanForAnalyzerPackages()
//     {
//         var result = new HashSet<(string Id, string? Version)>();
//
//         // Scan Directory.Packages.props for analyzer packages
//         var packagesProps = RootDirectory / "Directory.Packages.props";
//         if (packagesProps.FileExists())
//         {
//             var content = await File.ReadAllTextAsync(packagesProps);
//             var matches = Regex.Matches(content,
//                 @"<PackageVersion\s+Include=""([^""]+\.Analyzers?[^""]*)""\s+Version=""([^""]+)""",
//                 RegexOptions.IgnoreCase);
//
//             foreach (Match match in matches)
//             {
//                 result.Add((match.Groups[1].Value, match.Groups[2].Value));
//             }
//         }
//
//         return result;
//     }
//
//     private static async Task CollectDependencies(
//         PackageIdentity package,
//         IEnumerable<SourceRepository> repositories,
//         NuGetFramework framework,
//         SourceCacheContext cache,
//         HashSet<SourcePackageDependencyInfo> dependencies)
//     {
//         if (dependencies.Any(d => d.Id == package.Id && d.Version == package.Version))
//             return;
//
//         foreach (var repository in repositories)
//         {
//             var dependencyInfoResource = await repository.GetResourceAsync<DependencyInfoResource>();
//             var dependencyInfo = await dependencyInfoResource.ResolvePackage(
//                 package, framework, cache, NullLogger.Instance, CancellationToken.None);
//
//             if (dependencyInfo is null) continue;
//
//             if (dependencies.Add(dependencyInfo))
//             {
//                 foreach (var dependency in dependencyInfo.Dependencies)
//                 {
//                     await CollectDependencies(
//                         new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
//                         repositories, framework, cache, dependencies);
//                 }
//             }
//         }
//     }
//
//     private static async Task<Assembly[]> GetAnalyzerReferences(string packageId, NuGetVersion version)
//     {
//         var package = await DownloadNuGetPackage(packageId, version, NullLogger.Instance, CancellationToken.None);
//         var result = new List<Assembly>();
//         var files = package.PackageReader.GetFiles("analyzers");
//         var filesGroupedByFolder = files.GroupBy(Path.GetDirectoryName).ToArray();
//
//         foreach (var group in filesGroupedByFolder)
//         {
//             var context = new AssemblyLoadContext(null, isCollectible: true);
//             context.Resolving += (_, assemblyName) =>
//             {
//                 var assemblyFileName = assemblyName.Name + ".dll";
//                 return TryLoadFromFolderHierarchy(context, package, filesGroupedByFolder, group.Key, assemblyFileName);
//             };
//
//             foreach (var file in group)
//             {
//                 if (!string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase))
//                     continue;
//
//                 try
//                 {
//                     await using var stream = package.PackageReader.GetStream(file);
//                     using var ms = new MemoryStream();
//                     await stream.CopyToAsync(ms);
//                     ms.Position = 0;
//                     result.Add(context.LoadFromStream(ms));
//                 }
//                 catch (Exception ex)
//                 {
//                     Log.Debug("Failed to load {File}: {Error}", file, ex.Message);
//                 }
//             }
//         }
//
//         return [.. result];
//     }
//
//     private static Assembly? TryLoadFromFolderHierarchy(
//         AssemblyLoadContext context,
//         DownloadResourceResult package,
//         IGrouping<string?, string>[] filesGroupedByFolder,
//         string? startFolder,
//         string assemblyFileName)
//     {
//         var folder = startFolder;
//         while (!string.IsNullOrEmpty(folder))
//         {
//             var group = filesGroupedByFolder.FirstOrDefault(g =>
//                 string.Equals(g.Key, folder, StringComparison.OrdinalIgnoreCase));
//
//             if (group is not null)
//             {
//                 var assemblyPath = group.FirstOrDefault(f =>
//                     string.Equals(Path.GetFileName(f), assemblyFileName, StringComparison.OrdinalIgnoreCase));
//
//                 if (assemblyPath is not null)
//                 {
//                     try
//                     {
//                         using var stream = package.PackageReader.GetStream(assemblyPath);
//                         using var ms = new MemoryStream();
//                         stream.CopyTo(ms);
//                         ms.Position = 0;
//                         return context.LoadFromStream(ms);
//                     }
//                     catch
//                     {
//                         // Continue searching
//                     }
//                 }
//             }
//
//             folder = Path.GetDirectoryName(folder);
//         }
//
//         return null;
//     }
//
//     private static async Task<DownloadResourceResult> DownloadNuGetPackage(
//         string packageId,
//         NuGetVersion? version,
//         ILogger logger,
//         CancellationToken cancellationToken)
//     {
//         var settings = Settings.LoadDefaultSettings(null);
//         var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
//         const string source = "https://api.nuget.org/v3/index.json";
//
//         var cache = new SourceCacheContext();
//         var repository = Repository.Factory.GetCoreV3(source);
//         var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
//
//         if (version is null)
//         {
//             var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
//             var metadata = await metadataResource.GetMetadataAsync(
//                 packageId, includePrerelease: true, includeUnlisted: false,
//                 cache, NullLogger.Instance, CancellationToken.None);
//             version = metadata.MaxBy(m => m.Identity.Version)?.Identity.Version;
//             if (version is null)
//                 throw new InvalidOperationException($"Could not find package {packageId}");
//         }
//
//         var packageIdentity = new PackageIdentity(packageId, version);
//         var package = GlobalPackagesFolderUtility.GetPackage(packageIdentity, globalPackagesFolder);
//
//         if (package is null || package.Status is DownloadResourceResultStatus.NotFound)
//         {
//             using var packageStream = new MemoryStream();
//             await resource.CopyNupkgToStreamAsync(
//                 packageId, version, packageStream, cache, logger, cancellationToken);
//
//             packageStream.Seek(0, SeekOrigin.Begin);
//
//             package = await GlobalPackagesFolderUtility.AddPackageAsync(
//                 source, packageIdentity, packageStream, globalPackagesFolder,
//                 parentId: Guid.Empty,
//                 ClientPolicyContext.GetClientPolicy(settings, logger),
//                 logger, cancellationToken);
//         }
//
//         return package;
//     }
//
//     private string GenerateEditorConfigContent(AbsolutePath existingPath, HashSet<AnalyzerRule> rules)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("# Auto-generated analyzer configuration");
//         sb.AppendLine("# global_level must be higher than the .NET Analyzer files");
//         sb.AppendLine("is_global = true");
//         sb.AppendLine("global_level = 0");
//
//         var existingConfig = ParseExistingConfig(existingPath);
//
//         if (existingConfig.Unknowns.Length > 0)
//         {
//             foreach (var unknown in existingConfig.Unknowns)
//             {
//                 sb.AppendLine(unknown);
//             }
//         }
//         else
//         {
//             sb.AppendLine();
//         }
//
//         foreach (var rule in rules.OrderBy(r => r.Id))
//         {
//             var existingRule = existingConfig.Rules.FirstOrDefault(r => r.Id == rule.Id);
//             var severity = existingRule?.Severity ?? rule.DefaultEffectiveSeverity;
//
//             sb.AppendLine($"# {rule.Id}: {rule.Title}");
//             if (!string.IsNullOrEmpty(rule.Url))
//             {
//                 sb.AppendLine($"# Help link: {rule.Url}");
//             }
//             sb.AppendLine($"# Enabled: {rule.Enabled}, Severity: {FormatSeverity(rule.DefaultSeverity)}");
//
//             if (existingRule?.Comments.Length > 0)
//             {
//                 foreach (var comment in existingRule.Comments)
//                 {
//                     sb.AppendLine(comment);
//                 }
//             }
//
//             sb.AppendLine($"dotnet_diagnostic.{rule.Id}.severity = {FormatSeverity(severity)}");
//             sb.AppendLine();
//         }
//
//         return sb.ToString().ReplaceLineEndings("\n");
//     }
//
//     private static (AnalyzerConfiguration[] Rules, string[] Unknowns) ParseExistingConfig(AbsolutePath path)
//     {
//         var rules = new List<AnalyzerConfiguration>();
//         var unknowns = new List<string>();
//         var currentComment = new List<string>();
//
//         if (!path.FileExists())
//             return ([], []);
//
//         try
//         {
//             var lines = File.ReadAllLines(path);
//
//             foreach (var line in lines)
//             {
//                 try
//                 {
//                     if (line.StartsWith('#'))
//                     {
//                         if (line.StartsWith("# Enabled: ", StringComparison.Ordinal) ||
//                             line.StartsWith("# Default severity: ", StringComparison.Ordinal) ||
//                             line.StartsWith("# Help link: ", StringComparison.Ordinal) ||
//                             line.StartsWith("# Auto-generated", StringComparison.Ordinal))
//                             continue;
//
//                         currentComment.Add(line);
//                         continue;
//                     }
//
//                     if (line.StartsWith("is_global", StringComparison.Ordinal) ||
//                         line.StartsWith("global_level", StringComparison.Ordinal))
//                         continue;
//
//                     var match = Regex.Match(line,
//                         @"^dotnet_diagnostic\.(?<RuleId>[a-zA-Z0-9]+).severity\s*=\s*(?<Severity>[a-z]+)");
//
//                     if (match.Success)
//                     {
//                         var severity = ParseSeverity(match.Groups["Severity"].Value);
//                         rules.Add(new AnalyzerConfiguration(
//                             match.Groups["RuleId"].Value,
//                             [.. currentComment.Skip(1)],
//                             severity));
//                     }
//                     else
//                     {
//                         unknowns.AddRange(currentComment);
//                         if (rules.Count == 0 || !string.IsNullOrEmpty(line))
//                         {
//                             unknowns.Add(line);
//                         }
//                     }
//                 }
//                 finally
//                 {
//                     if (!line.StartsWith('#'))
//                     {
//                         currentComment.Clear();
//                     }
//                 }
//             }
//         }
//         catch
//         {
//             // Ignore parse errors
//         }
//
//         return ([.. rules], [.. unknowns]);
//     }
//
//     private static DiagnosticSeverity? ParseSeverity(string value) => value switch
//     {
//         "silent" => DiagnosticSeverity.Hidden,
//         "suggestion" => DiagnosticSeverity.Info,
//         "warning" => DiagnosticSeverity.Warning,
//         "error" => DiagnosticSeverity.Error,
//         "none" => null,
//         _ => Enum.TryParse<DiagnosticSeverity>(value, true, out var s) ? s : null
//     };
//
//     private static string FormatSeverity(DiagnosticSeverity? severity) => severity switch
//     {
//         null => "none",
//         DiagnosticSeverity.Hidden => "silent",
//         DiagnosticSeverity.Info => "suggestion",
//         DiagnosticSeverity.Warning => "warning",
//         DiagnosticSeverity.Error => "error",
//         _ => throw new ArgumentOutOfRangeException(nameof(severity))
//     };
//
//     private static IEnumerable<string> ExtractNamespaces(Stream assemblyStream, string rootTypeName)
//     {
//         // This would use Roslyn MetadataReference to extract namespaces
//         // Simplified implementation - in practice would use full Roslyn analysis
//         yield break;
//     }
//
//     private static bool ShouldWriteFile(AbsolutePath path, string content)
//     {
//         if (!path.FileExists())
//             return true;
//
//         var existing = File.ReadAllText(path).ReplaceLineEndings("\n");
//         return existing != content;
//     }
//
//     // ════════════════════════════════════════════════════════════════════════
//     // Records
//     // ════════════════════════════════════════════════════════════════════════
//
//     private sealed record AnalyzerConfiguration(string Id, string[] Comments, DiagnosticSeverity? Severity);
//
//     private sealed record AnalyzerRule(
//         string Id,
//         string Title,
//         string? Url,
//         bool Enabled,
//         DiagnosticSeverity DefaultSeverity,
//         DiagnosticSeverity? DefaultEffectiveSeverity);
// }
