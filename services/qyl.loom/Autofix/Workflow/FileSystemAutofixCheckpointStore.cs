// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace Qyl.Loom.Autofix.Workflow;

/// File-backed JsonCheckpointStore — survives process restart so a workflow
/// run can resume from the last super-step after the host crashes or the
/// dashboard tab is reloaded. Writes one JSON file per checkpoint under
/// <c>{root}/{sessionId}/{checkpointId}.json</c>; lists by directory
/// enumeration. Configure the root via the
/// <c>QYL_AUTOFIX_CHECKPOINT_ROOT</c> environment variable; default lives
/// under the OS temp folder so dev hosts don't clutter the user profile.
///
/// Path-traversal hardening — every <c>sessionId</c> / <c>checkpointId</c> is
/// validated against <see cref="IsSafeIdentifier" /> (alphanumeric +
/// dash/underscore only) before it touches <c>Path.Combine</c>, so a
/// malicious <c>"../foo"</c> id can't escape the checkpoint root.
internal sealed class FileSystemAutofixCheckpointStore(IConfiguration configuration) : JsonCheckpointStore
{
    private readonly string _root = ResolveRoot(configuration);

    private static string ResolveRoot(IConfiguration configuration) =>
        configuration["QYL_AUTOFIX_CHECKPOINT_ROOT"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "qyl-autofix-checkpoints");

    public override async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId, JsonElement value, CheckpointInfo? parent = null)
    {
        var sessionDir = SessionDir(sessionId);
        Directory.CreateDirectory(sessionDir);

        var checkpointId = Guid.NewGuid().ToString("N");
        var path = Path.Combine(sessionDir, $"{checkpointId}.json");

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value).ConfigureAwait(false);

        return new CheckpointInfo(sessionId, checkpointId);
    }

    public override async ValueTask<JsonElement> RetrieveCheckpointAsync(
        string sessionId, CheckpointInfo key)
    {
        var checkpointFile = SafeFileName(key.CheckpointId);
        var path = Path.Combine(SessionDir(sessionId), $"{checkpointFile}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Checkpoint {checkpointFile} for session not found at {path}.");
        }

        await using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return doc.RootElement.Clone();
    }

    public override ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId, CheckpointInfo? withParent = null)
    {
        var dir = SessionDir(sessionId);
        if (!Directory.Exists(dir))
        {
            return new(Array.Empty<CheckpointInfo>());
        }

        var infos = Directory
            .EnumerateFiles(dir, "*.json")
            .Select(path => new CheckpointInfo(sessionId, Path.GetFileNameWithoutExtension(path)))
            .ToArray();

        return new(infos);
    }

    private string SessionDir(string sessionId) => Path.Combine(_root, SafeFileName(sessionId));

    /// Reject any id that could collapse <c>Path.Combine</c> back up to
    /// the checkpoint root or beyond. The contract between MAF and this store
    /// only ever passes mint-fresh GUID-like ids, so the validation just
    /// fail-closes on anything else; there is no caller-controlled use case
    /// for <c>../</c> in a checkpoint identifier.
    private static string SafeFileName(string id)
    {
        if (string.IsNullOrEmpty(id) || !IsSafeIdentifier(id))
        {
            throw new ArgumentException(
                $"Checkpoint identifier '{id}' contains characters that aren't safe for filesystem use.",
                nameof(id));
        }
        return id;
    }

    private static bool IsSafeIdentifier(string id)
    {
        foreach (var c in id)
        {
            if (!(char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                return false;
            }
        }
        return true;
    }
}
