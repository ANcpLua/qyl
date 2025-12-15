using System.IO;
using System.Threading;
using Nuke.Common.IO;
using Serilog;

namespace Domain.Utilities;

/// <summary>
///     Generation guard utility for handling file overwrites during code generation.
///     Provides Force/DryRun/SkipExisting modes with content-aware skipping.
/// </summary>
public sealed class GenerationGuard
{
    public GenerationGuard(bool force = false, bool dryRun = false, bool skipExisting = false)
    {
        Force = force;
        DryRun = dryRun;
        SkipExisting = skipExisting;
    }

    public bool Force { get; }

    public bool DryRun { get; }

    public bool SkipExisting { get; }

    public GenerationStats Stats { get; } = new();

    // ════════════════════════════════════════════════════════════════════════
    // Core Methods
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Check if a file should be generated at the given path.
    /// </summary>
    public GenerationDecision ShouldGenerate(AbsolutePath path, string description)
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

        // No force flag - skip by default (safe)
        Log.Warning("  [SKIP] {Description} already exists (use --Force to overwrite): {Path}",
            description, path);
        Stats.IncrementSkipped();
        return GenerationDecision.Skip;
    }

    /// <summary>
    ///     Check if content should be written (content-aware generation).
    ///     Skips write if content is identical.
    /// </summary>
    public GenerationDecision ShouldGenerateWithContent(AbsolutePath path, string content, string description)
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

        Log.Warning("  [SKIP] {Description} would be updated (use --Force): {Path}", description, path);
        Stats.IncrementSkipped();
        return GenerationDecision.Skip;
    }

    /// <summary>
    ///     Write content to a file if the decision permits.
    /// </summary>
    public void WriteIfAllowed(AbsolutePath path, string content, string description)
    {
        var decision = ShouldGenerateWithContent(path, content, description);

        if (decision is not (GenerationDecision.Generate or GenerationDecision.Update
            or GenerationDecision.Overwrite))
            return;

        path.Parent.CreateDirectory();
        File.WriteAllText(path, content);
        LogGenerated(path, description);
    }

    /// <summary>
    ///     Log that a file was successfully generated.
    /// </summary>
    public void LogGenerated(AbsolutePath path, string? description = null)
    {
        Stats.IncrementGenerated();
        if (description is not null)
            Log.Information("  [GENERATED] {Description}: {Path}", description, path);
        else
            Log.Information("  [GENERATED] {Path}", path);
    }

    /// <summary>
    ///     Log generation summary statistics.
    /// </summary>
    public void LogSummary()
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
        if (total is 0 && Stats.SkippedCount > 0)
        {
            Log.Information("");
            Log.Information("  Tip: Use --Force to overwrite existing files");
            Log.Information("       Use --DryRun to preview what would be generated");
        }

        Log.Information("═══════════════════════════════════════════════════════════════");
    }
}

/// <summary>
///     Result of generation decision.
/// </summary>
public enum GenerationDecision
{
    /// <summary>File doesn't exist, generate it</summary>
    Generate,

    /// <summary>File exists, user chose to overwrite</summary>
    Overwrite,

    /// <summary>File exists with different content, update it</summary>
    Update,

    /// <summary>File exists, skip generation</summary>
    Skip,

    /// <summary>File content is identical, no action needed</summary>
    Unchanged,

    /// <summary>Dry run mode, no file written</summary>
    DryRun
}

/// <summary>
///     Thread-safe statistics for generation operations.
/// </summary>
public sealed class GenerationStats
{
    int _dryRunCount;
    int _generatedCount;
    int _overwrittenCount;
    int _skippedCount;
    int _unchangedCount;
    int _updatedCount;

    public int GeneratedCount => _generatedCount;
    public int UpdatedCount => _updatedCount;
    public int OverwrittenCount => _overwrittenCount;
    public int SkippedCount => _skippedCount;
    public int UnchangedCount => _unchangedCount;
    public int DryRunCount => _dryRunCount;

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