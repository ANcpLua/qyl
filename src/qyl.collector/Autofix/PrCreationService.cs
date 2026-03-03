using qyl.collector.Identity;

namespace qyl.collector.Autofix;

/// <summary>Result of a pull-request creation attempt.</summary>
public sealed record PrCreationResult(
    bool Success,
    string? PrUrl,
    int? PrNumber,
    string? Error);

/// <summary>
///     Applies the <c>changes_json</c> from a fix run to a GitHub repository:
///     creates a branch, commits each patched file, and opens a pull request.
/// </summary>
public sealed partial class PrCreationService(
    GitHubService github,
    DuckDbStore store,
    ILogger<PrCreationService> logger)
{
    public async Task<PrCreationResult> CreatePrAsync(
        string runId,
        string repoFullName,
        string? baseBranch = null,
        CancellationToken ct = default)
    {
        FixRunRecord? run = await store.GetFixRunAsync(runId, ct).ConfigureAwait(false);
        if (run is null)
            return new PrCreationResult(false, null, null, $"Fix run '{runId}' not found.");

        if (string.IsNullOrWhiteSpace(run.ChangesJson))
            return new PrCreationResult(false, null, null, "Fix run has no changes_json. Run qyl.generate_fix first.");

        PatchDocument? patch;
        try
        {
            patch = JsonSerializer.Deserialize(run.ChangesJson, PrCreationJsonContext.Default.PatchDocument);
        }
        catch (JsonException ex)
        {
            return new PrCreationResult(false, null, null, $"Invalid changes_json: {ex.Message}");
        }

        if (patch is null || patch.Files is not { Count: > 0 })
            return new PrCreationResult(false, null, null, "No file patches in changes_json.");

        // Resolve base branch (default: repo default branch)
        string effectiveBase = baseBranch ?? "main";
        string? baseSha = await github.GetBranchShaAsync(repoFullName, effectiveBase, ct).ConfigureAwait(false);
        if (baseSha is null)
            return new PrCreationResult(false, null, null,
                $"Could not resolve SHA for branch '{effectiveBase}' in {repoFullName}. Check QYL_GITHUB_TOKEN and repo name.");

        // Create fix branch
        string branchName = $"qyl/fix-{run.IssueId[..Math.Min(8, run.IssueId.Length)]}-{run.RunId[..Math.Min(8, run.RunId.Length)]}";
        bool branchCreated = await github.CreateBranchAsync(repoFullName, branchName, baseSha, ct).ConfigureAwait(false);
        if (!branchCreated)
            return new PrCreationResult(false, null, null,
                $"Failed to create branch '{branchName}' in {repoFullName}.");

        LogBranchCreated(branchName, repoFullName);

        // Apply and commit each patched file
        List<string> committedFiles = [];
        List<string> patchErrors = [];

        foreach (PatchFile file in patch.Files)
        {
            string result = await CommitFileAsync(repoFullName, file, branchName, run.RunId, ct)
                .ConfigureAwait(false);
            if (result.StartsWithIgnoreCase("error:"))
                patchErrors.Add(result);
            else
                committedFiles.Add(file.Path);
        }

        if (committedFiles.Count == 0)
            return new PrCreationResult(false, null, null,
                $"No files could be committed. Errors: {string.Join("; ", patchErrors)}");

        // Open the pull request
        string prTitle = patch.PrTitle ?? $"fix: automated fix for issue {run.IssueId[..Math.Min(8, run.IssueId.Length)]}";
        string prBody = BuildPrBody(patch, run, committedFiles, patchErrors);

        string? prUrl = await github.CreatePullRequestAsync(
            repoFullName, prTitle, prBody, branchName, effectiveBase, ct).ConfigureAwait(false);

        if (prUrl is null)
            return new PrCreationResult(false, null, null,
                $"Branch '{branchName}' was pushed but PR creation failed.");

        LogPrCreated(prUrl, run.IssueId);

        // Update fix run with PR URL in the description
        await store.UpdateFixRunAsync(
            runId, "applied",
            description: $"PR: {prUrl}",
            confidence: run.ConfidenceScore,
            changesJson: run.ChangesJson,
            ct: ct).ConfigureAwait(false);

        return new PrCreationResult(true, prUrl, null, null);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> CommitFileAsync(
        string repoFullName, PatchFile file, string branch, string runId,
        CancellationToken ct)
    {
        // Fetch current file content + SHA (needed for update)
        GitHubFileContent? existing = await github
            .GetFileContentAsync(repoFullName, file.Path, branch, ct)
            .ConfigureAwait(false);

        string currentContent;
        if (existing is not null && existing.Content is not null)
        {
            // GitHub returns base64 with newlines
            string cleanBase64 = existing.Content.Replace("\n", "").Replace("\r", "");
            currentContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cleanBase64));
        }
        else
        {
            // File doesn't exist yet — will be created
            currentContent = string.Empty;
        }

        string? patched = TryApplyHunks(file, currentContent);
        if (patched is null)
            return $"error: could not locate original_lines in '{file.Path}' — " +
                   "LLM-generated lines may not match real file content. Use qyl.export_for_agent to apply manually.";
        string contentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(patched));

        string commitMessage = $"fix: {file.Path} — qyl autofix run {runId[..Math.Min(8, runId.Length)]}";
        bool ok = await github.CreateOrUpdateFileAsync(
            repoFullName, file.Path, contentBase64,
            commitMessage, branch, existing?.Sha, ct).ConfigureAwait(false);

        return ok ? file.Path : $"error: failed to commit {file.Path}";
    }

    /// <summary>
    ///     Applies all hunks to <paramref name="content"/>.
    ///     Returns <c>null</c> if any hunk cannot be located, so the caller can skip
    ///     committing the file rather than writing corrupt content.
    /// </summary>
    private static string? TryApplyHunks(PatchFile file, string content)
    {
        foreach (PatchHunk hunk in file.Hunks ?? [])
        {
            if (hunk.OriginalLines is not { Count: > 0 })
                continue;

            string? patched = FindAndReplace(content, hunk.OriginalLines, hunk.ReplacementLines ?? []);
            if (patched is null)
                return null; // Hunk failed — caller decides; never corrupt the file.

            content = patched;
        }

        return content;
    }

    /// <summary>
    ///     Finds <paramref name="originalLines"/> inside <paramref name="content"/> using
    ///     a line-by-line trimmed comparison (handles indentation divergence from LLM output)
    ///     and replaces that block with <paramref name="replacementLines"/>, preserving the
    ///     original indentation of the first matched line.
    /// </summary>
    private static string? FindAndReplace(
        string content,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> replacementLines)
    {
        string[] contentLines = content.Split('\n');
        int origLen = originalLines.Count;

        for (int i = 0; i <= contentLines.Length - origLen; i++)
        {
            bool match = true;
            for (int j = 0; j < origLen; j++)
            {
                if (!string.Equals(
                        contentLines[i + j].Trim(),
                        originalLines[j].Trim(),
                        StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }

            if (!match) continue;

            // Preserve the leading whitespace of the first matched line so the replacement
            // fits the file's existing indentation style.
            string leadingWhitespace = contentLines[i][..(contentLines[i].Length - contentLines[i].TrimStart().Length)];

            List<string> result = new(contentLines.Length - origLen + replacementLines.Count);
            result.AddRange(contentLines[..i]);

            foreach (string line in replacementLines)
            {
                // Re-indent: apply leading whitespace unless the line already has it.
                result.Add(line.Length > 0 && !line.StartsWithOrdinal(leadingWhitespace)
                    ? leadingWhitespace + line.TrimStart()
                    : line);
            }

            result.AddRange(contentLines[(i + origLen)..]);
            return string.Join("\n", result);
        }

        return null; // Block not found even with whitespace normalisation.
    }

    private static string BuildPrBody(
        PatchDocument patch, FixRunRecord run,
        List<string> committed, List<string> errors)
    {
        StringBuilder sb = new();
        sb.AppendLine(patch.PrBody ?? $"Automated fix generated by qyl Seer for issue `{run.IssueId}`.");
        sb.AppendLine();
        sb.AppendLine($"**Confidence:** {run.ConfidenceScore:P0}");
        sb.AppendLine($"**Fix run:** `{run.RunId}`");
        sb.AppendLine($"**Issue:** `{run.IssueId}`");

        if (committed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Patched files:**");
            foreach (string f in committed)
                sb.AppendLine($"- `{f}`");
        }

        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Patch warnings** (some hunks could not be applied exactly — see FIXME comments):");
            foreach (string e in errors)
                sb.AppendLine($"- {e}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Generated by [qyl](https://qyl.io) Seer autofix pipeline.*");
        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Created branch '{Branch}' in {Repo}")]
    private partial void LogBranchCreated(string branch, string repo);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Opened PR at {PrUrl} for issue {IssueId}")]
    private partial void LogPrCreated(string prUrl, string issueId);
}

// =============================================================================
// Patch document DTOs — matches changes_json schema_version=1
// =============================================================================

public sealed record PatchDocument
{
    [JsonPropertyName("schema_version")] public string? SchemaVersion { get; init; }
    [JsonPropertyName("root_cause")] public string? RootCause { get; init; }
    [JsonPropertyName("confidence")] public double Confidence { get; init; }
    [JsonPropertyName("files")] public IReadOnlyList<PatchFile>? Files { get; init; }
    [JsonPropertyName("pr_title")] public string? PrTitle { get; init; }
    [JsonPropertyName("pr_body")] public string? PrBody { get; init; }
}

public sealed record PatchFile
{
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("operation")] public string Operation { get; init; } = "modify";
    [JsonPropertyName("hunks")] public IReadOnlyList<PatchHunk>? Hunks { get; init; }
    [JsonPropertyName("rationale")] public string? Rationale { get; init; }
}

public sealed record PatchHunk
{
    [JsonPropertyName("context_before")] public IReadOnlyList<string>? ContextBefore { get; init; }
    [JsonPropertyName("original_lines")] public IReadOnlyList<string>? OriginalLines { get; init; }
    [JsonPropertyName("replacement_lines")] public IReadOnlyList<string>? ReplacementLines { get; init; }
    [JsonPropertyName("context_after")] public IReadOnlyList<string>? ContextAfter { get; init; }
}

[JsonSerializable(typeof(PatchDocument))]
[JsonSerializable(typeof(PatchFile))]
[JsonSerializable(typeof(PatchHunk))]
internal sealed partial class PrCreationJsonContext : JsonSerializerContext;
