using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components.Theory;

/// <summary>
/// Generation guard component for handling file overwrites during code generation.
///
/// Use cases:
/// - Demo-friendly: Shows "file exists, skipping" messages
/// - Force flag: --IGenerationGuardForce=true to overwrite
/// - Interactive mode: Prompts when running in terminal (not CI)
/// - Dry run: Shows what would be generated without writing
///
/// Example usage in other components:
/// <code>
/// var guard = ((IGenerationGuard)this);
/// if (guard.ShouldGenerate(outputPath, "MyComponent"))
/// {
///     await File.WriteAllTextAsync(outputPath, content);
///     guard.LogGenerated(outputPath);
/// }
/// </code>
/// </summary>
[ParameterPrefix(nameof(IGenerationGuard))]
internal interface IGenerationGuard : INukeBuild
{
    // ════════════════════════════════════════════════════════════════════════
    // Parameters
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Force overwrite existing files without prompting.
    /// Usage: --Force or --IGenerationGuardForce
    /// </summary>
    [Parameter("Force overwrite existing generated files")]
    bool Force => TryGetValue<bool?>(() => Force) ?? false;

    /// <summary>
    /// Dry run mode - show what would be generated without writing.
    /// Usage: --DryRun or --IGenerationGuardDryRun
    /// </summary>
    [Parameter("Dry run - show what would be generated without writing")]
    bool DryRun => TryGetValue<bool?>(() => DryRun) ?? false;

    /// <summary>
    /// Skip all existing files without prompting.
    /// Usage: --SkipExisting or --IGenerationGuardSkipExisting
    /// </summary>
    [Parameter("Skip all existing files without prompting")]
    bool SkipExisting => TryGetValue<bool?>(() => SkipExisting) ?? false;

    /// <summary>
    /// Enable interactive prompts for file overwrites.
    /// Auto-detected based on terminal/CI environment.
    /// </summary>
    [Parameter("Enable interactive prompts (auto-detected if not set)")]
    bool? Interactive => TryGetValue<bool?>(() => Interactive);

    // ════════════════════════════════════════════════════════════════════════
    // Computed Properties
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether we're in an interactive terminal session.
    /// Returns false in CI environments.
    /// </summary>
    bool IsInteractive => Interactive ?? (!IsServerBuild && Environment.UserInteractive);

    /// <summary>
    /// Generation statistics for logging (thread-safe singleton).
    /// </summary>
    GenerationStats Stats => GenerationStats.Instance;

    // ════════════════════════════════════════════════════════════════════════
    // Core Methods
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a file should be generated at the given path.
    /// Handles existence checks, prompts, and logging.
    /// </summary>
    /// <param name="path">Target file path</param>
    /// <param name="description">Human-readable description (e.g., "C# client")</param>
    /// <returns>True if file should be generated/written</returns>
    GenerationDecision ShouldGenerate(AbsolutePath path, string description)
    {
        if (DryRun)
        {
            Log.Information("  [DRY RUN] Would generate: {Path}", path);
            Stats.IncrementDryRun();
            return GenerationDecision.DryRun;
        }

        if (!path.FileExists())
        {
            Log.Debug("  [NEW] {Description}: {Path}", description, path);
            return GenerationDecision.Generate;
        }

        // File exists - determine action
        if (Force)
        {
            Log.Information("  [OVERWRITE] {Description}: {Path}", description, path);
            Stats.IncrementOverwritten();
            return GenerationDecision.Overwrite;
        }

        if (SkipExisting)
        {
            Log.Information("  [SKIP] {Description} already exists: {Path}", description, path);
            Stats.IncrementSkipped();
            return GenerationDecision.Skip;
        }

        if (IsInteractive)
        {
            return PromptForOverwrite(path, description);
        }

        // Non-interactive, no force flag - skip by default (safe)
        Log.Warning("  [SKIP] {Description} already exists (use --Force to overwrite): {Path}",
            description, path);
        Stats.IncrementSkipped();
        return GenerationDecision.Skip;
    }

    /// <summary>
    /// Check if content should be written (content-aware generation).
    /// Skips write if content is identical.
    /// </summary>
    GenerationDecision ShouldGenerateWithContent(AbsolutePath path, string content, string description)
    {
        if (DryRun)
        {
            Log.Information("  [DRY RUN] Would generate: {Path}", path);
            Stats.IncrementDryRun();
            return GenerationDecision.DryRun;
        }

        if (!path.FileExists())
        {
            Log.Debug("  [NEW] {Description}: {Path}", description, path);
            return GenerationDecision.Generate;
        }

        // Check if content is identical
        var existingContent = File.ReadAllText(path).ReplaceLineEndings("\n");
        var normalizedContent = content.ReplaceLineEndings("\n");

        if (existingContent == normalizedContent)
        {
            Log.Debug("  [UNCHANGED] {Description}: {Path}", description, path);
            Stats.IncrementUnchanged();
            return GenerationDecision.Unchanged;
        }

        // Content differs - apply normal overwrite logic
        if (Force)
        {
            Log.Information("  [UPDATE] {Description}: {Path}", description, path);
            Stats.IncrementUpdated();
            return GenerationDecision.Update;
        }

        if (SkipExisting)
        {
            Log.Information("  [SKIP] {Description} differs but skipping: {Path}", description, path);
            Stats.IncrementSkipped();
            return GenerationDecision.Skip;
        }

        if (IsInteractive)
        {
            return PromptForOverwrite(path, description);
        }

        Log.Warning("  [SKIP] {Description} would be updated (use --Force): {Path}", description, path);
        Stats.IncrementSkipped();
        return GenerationDecision.Skip;
    }

    /// <summary>
    /// Log that a file was successfully generated.
    /// </summary>
    void LogGenerated(AbsolutePath path, string? description = null)
    {
        Stats.IncrementGenerated();
        if (description is not null)
        {
            Log.Information("  [GENERATED] {Description}: {Path}", description, path);
        }
        else
        {
            Log.Information("  [GENERATED] {Path}", path);
        }
    }

    /// <summary>
    /// Log generation summary statistics.
    /// </summary>
    void LogSummary()
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("  Generation Summary");
        Log.Information("═══════════════════════════════════════════════════════════════");

        if (Stats.GeneratedCount > 0)
            Log.Information("  Generated:  {Count} files", Stats.GeneratedCount);
        if (Stats.UpdatedCount > 0)
            Log.Information("  Updated:    {Count} files", Stats.UpdatedCount);
        if (Stats.OverwrittenCount > 0)
            Log.Information("  Overwritten:{Count} files", Stats.OverwrittenCount);
        if (Stats.SkippedCount > 0)
            Log.Information("  Skipped:    {Count} files (already exist)", Stats.SkippedCount);
        if (Stats.UnchangedCount > 0)
            Log.Information("  Unchanged:  {Count} files (content identical)", Stats.UnchangedCount);
        if (Stats.DryRunCount > 0)
            Log.Information("  Dry Run:    {Count} files (would generate)", Stats.DryRunCount);

        var total = Stats.GeneratedCount + Stats.UpdatedCount + Stats.OverwrittenCount;
        if (total == 0 && Stats.SkippedCount > 0)
        {
            Log.Information("");
            Log.Information("  💡 Tip: Use --Force to overwrite existing files");
            Log.Information("          Use --DryRun to preview what would be generated");
        }

        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Interactive Prompt
    // ════════════════════════════════════════════════════════════════════════

    private GenerationDecision PromptForOverwrite(AbsolutePath path, string description)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠️  {description} already exists:");
        Console.ResetColor();
        Console.WriteLine($"      {path}");
        Console.WriteLine();
        Console.Write("  Overwrite? [y]es / [n]o / [a]ll / [s]kip all: ");

        var key = Console.ReadKey(intercept: false);
        Console.WriteLine();

        return key.KeyChar switch
        {
            'y' or 'Y' => GenerationDecision.Overwrite,
            'a' or 'A' => SetForceAndReturn(),
            's' or 'S' => SetSkipAndReturn(),
            _ => SkipAndLog()
        };

        GenerationDecision SetForceAndReturn()
        {
            // Can't modify interface property, but the behavior changes
            Log.Information("  [ALL] Overwriting all remaining files");
            Stats.IncrementOverwritten();
            return GenerationDecision.OverwriteAll;
        }

        GenerationDecision SetSkipAndReturn()
        {
            Log.Information("  [SKIP ALL] Skipping all remaining existing files");
            Stats.IncrementSkipped();
            return GenerationDecision.SkipAll;
        }

        GenerationDecision SkipAndLog()
        {
            Log.Information("  [SKIP] {Path}", path);
            Stats.IncrementSkipped();
            return GenerationDecision.Skip;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Targets
    // ════════════════════════════════════════════════════════════════════════

    Target GenerationGuardInfo => d => d
        .Description("Show generation guard configuration")
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generation Guard Configuration");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Force:        {Force}", Force);
            Log.Information("  DryRun:       {DryRun}", DryRun);
            Log.Information("  SkipExisting: {Skip}", SkipExisting);
            Log.Information("  Interactive:  {Interactive} (detected: {Detected})",
                Interactive?.ToString() ?? "auto", IsInteractive);
            Log.Information("  CI Build:     {IsCi}", IsServerBuild);
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("");
            Log.Information("  Usage Examples:");
            Log.Information("    nuke GenerateAll                    # Interactive prompts");
            Log.Information("    nuke GenerateAll --Force            # Overwrite all");
            Log.Information("    nuke GenerateAll --SkipExisting     # Skip all existing");
            Log.Information("    nuke GenerateAll --DryRun           # Preview only");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}

/// <summary>
/// Result of generation decision.
/// </summary>
public enum GenerationDecision
{
    /// <summary>File doesn't exist, generate it</summary>
    Generate,

    /// <summary>File exists, user chose to overwrite</summary>
    Overwrite,

    /// <summary>File exists, user chose to overwrite all</summary>
    OverwriteAll,

    /// <summary>File exists with different content, update it</summary>
    Update,

    /// <summary>File exists, skip generation</summary>
    Skip,

    /// <summary>File exists, skip all remaining</summary>
    SkipAll,

    /// <summary>File content is identical, no action needed</summary>
    Unchanged,

    /// <summary>Dry run mode, no file written</summary>
    DryRun
}

/// <summary>
/// Thread-safe statistics for generation operations.
/// Uses singleton pattern since NUKE interfaces can't hold instance state.
/// </summary>
public sealed class GenerationStats
{
    private static readonly Lazy<GenerationStats> LazyInstance = new(() => new GenerationStats());

    public static GenerationStats Instance => LazyInstance.Value;

    private int _generatedCount;
    private int _updatedCount;
    private int _overwrittenCount;
    private int _skippedCount;
    private int _unchangedCount;
    private int _dryRunCount;

    private GenerationStats() { }

    public int GeneratedCount
    {
        get => _generatedCount;
        set => Interlocked.Exchange(ref _generatedCount, value);
    }

    public int UpdatedCount
    {
        get => _updatedCount;
        set => Interlocked.Exchange(ref _updatedCount, value);
    }

    public int OverwrittenCount
    {
        get => _overwrittenCount;
        set => Interlocked.Exchange(ref _overwrittenCount, value);
    }

    public int SkippedCount
    {
        get => _skippedCount;
        set => Interlocked.Exchange(ref _skippedCount, value);
    }

    public int UnchangedCount
    {
        get => _unchangedCount;
        set => Interlocked.Exchange(ref _unchangedCount, value);
    }

    public int DryRunCount
    {
        get => _dryRunCount;
        set => Interlocked.Exchange(ref _dryRunCount, value);
    }

    public void IncrementGenerated() => Interlocked.Increment(ref _generatedCount);
    public void IncrementUpdated() => Interlocked.Increment(ref _updatedCount);
    public void IncrementOverwritten() => Interlocked.Increment(ref _overwrittenCount);
    public void IncrementSkipped() => Interlocked.Increment(ref _skippedCount);
    public void IncrementUnchanged() => Interlocked.Increment(ref _unchangedCount);
    public void IncrementDryRun() => Interlocked.Increment(ref _dryRunCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _generatedCount, 0);
        Interlocked.Exchange(ref _updatedCount, 0);
        Interlocked.Exchange(ref _overwrittenCount, 0);
        Interlocked.Exchange(ref _skippedCount, 0);
        Interlocked.Exchange(ref _unchangedCount, 0);
        Interlocked.Exchange(ref _dryRunCount, 0);
    }
}
