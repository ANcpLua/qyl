using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Storage;

internal readonly record struct WorkflowCheckpointBlob(
    string CheckpointId,
    long Length);

internal readonly record struct WorkflowCheckpointSidecarMetrics(
    long LiveBytes,
    long TemporaryOrOrphanBytes)
{
    public long TotalBytes => checked(LiveBytes + TemporaryOrOrphanBytes);
}

internal sealed record WorkflowCheckpointSweepCandidate(
    string StorageKey,
    string? StorageIdentity,
    long Length,
    bool RetainWithoutLookup,
    bool IsDirectory);

internal sealed record WorkflowCheckpointManifestValidation(
    IReadOnlyList<WorkflowRunStorageRow> BrokenManifests,
    int ProcessedManifests,
    IReadOnlyList<string> ValidStorageIdentities);

internal readonly record struct WorkflowCheckpointIdentityDelta(
    ulong Epoch,
    int Ordinal,
    string StorageIdentity,
    bool Active);

internal sealed record WorkflowCheckpointManifestMutation(
    ulong Epoch,
    IReadOnlyList<WorkflowCheckpointIdentityDelta> Deltas);

internal readonly record struct WorkflowCheckpointPhysicalEntry(
    string? StorageIdentity,
    long Length);

internal readonly record struct WorkflowCheckpointPhysicalOverride(
    long Version,
    bool Present,
    WorkflowCheckpointPhysicalEntry Entry);

internal sealed record WorkflowCheckpointSweepPage(
    IReadOnlyList<WorkflowCheckpointSweepCandidate> Candidates,
    int ExaminedEntries,
    bool SweepComplete)
{
    public int NextCandidateIndex { get; set; }

    public string? ClaimedQuarantinePath { get; set; }

    public long ClaimedLength { get; set; }

    public bool ClaimedIsDirectory { get; set; }

    public bool ClaimedDeletionCompleted { get; set; }
}

internal sealed class WorkflowCheckpointIncompatibleException(string message)
    : Exception(message);

internal sealed class WorkflowCheckpointReconciliationRestartException(
    string message,
    Exception innerException) : IOException(message, innerException);

internal sealed class WorkflowCheckpointStore : IDisposable
{
    private readonly string? _root;
    private readonly string? _quarantineRoot;
    private readonly WorkflowCheckpointFileSystem? _files;
    private readonly WorkflowProjectionLimits _limits;
    private readonly Func<
        WorkflowCheckpointReconciliationStage,
        CancellationToken,
        ValueTask>? _beforeReconciliation;
    private readonly ConcurrentDictionary<string, byte[]> _memory = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private HashSet<string> _activeStorageIdentities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _physicalKeysByIdentity =
        new(StringComparer.Ordinal);
    private Dictionary<string, WorkflowCheckpointPhysicalEntry> _physicalEntries =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IEnumerator<KeyValuePair<string, byte[]>>? _memorySweep;
    private IEnumerator<string>? _fileSweep;
    private IEnumerator<string>? _directorySweep;
    private WorkflowCheckpointSweepPage? _pendingSweepPage;
    private bool _fileSweepComplete;
    private HashSet<string>? _cycleActiveStorageIdentities;
    private Dictionary<string, WorkflowCheckpointPhysicalEntry>? _cyclePhysicalEntries;
    private readonly Dictionary<string, WorkflowCheckpointIdentityDelta>
        _cycleIdentityOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkflowCheckpointPhysicalOverride>
        _cyclePhysicalOverrides = new(StringComparer.Ordinal);
    private ulong _cycleSnapshotEpoch;
    private long _cyclePhysicalSnapshotVersion;
    private long _physicalVersion;
    private long _liveBytes;
    private long _temporaryOrOrphanBytes;

    public WorkflowCheckpointStore(
        string? root,
        WorkflowProjectionLimits limits,
        Func<
            WorkflowCheckpointReconciliationStage,
            CancellationToken,
            ValueTask>? beforeReconciliation = null)
    {
        _root = root is null
            ? null
            : System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(root));
        _quarantineRoot = _root is null
            ? null
            : System.IO.Path.Combine(_root, ".quarantine");
        _files = _root is null
            ? null
            : new WorkflowCheckpointFileSystem(_root);
        _limits = limits;
        _beforeReconciliation = beforeReconciliation;
        try
        {
            _files?.CreateDirectory(_quarantineRoot!);
        }
        catch
        {
            _files?.Dispose();
            throw;
        }
    }

    public string? Root => _root;

    public void Dispose()
    {
        ResetSweepState();
        _files?.Dispose();
        _gate.Dispose();
    }

    internal string CanonicalStorageKey(WorkflowRunStorageRow run) =>
        HasCanonicalManifest(run)
            ? StorageKey(run, run.ActiveCheckpointId!)
            : throw new InvalidDataException(
                "Workflow checkpoint manifest identity is invalid.");

    internal static string CanonicalStorageIdentity(WorkflowRunStorageRow run) =>
        CanonicalStorageIdentity(
            run.ProjectId,
            run.RunId,
            run.RunGeneration,
            run.ActiveCheckpointSequence,
            run.ActiveCheckpointId);

    internal static string CanonicalStorageIdentity(
        string projectId,
        string runId,
        string generation,
        ulong sequence,
        string? checkpointId) =>
        checkpointId is not null &&
        IsCanonicalCheckpointId(checkpointId, sequence)
            ? StorageIdentity(projectId, runId, generation, checkpointId)
            : throw new InvalidDataException(
                "Workflow checkpoint manifest identity is invalid.");

    internal static bool HasCanonicalManifest(WorkflowRunStorageRow run)
    {
        if (run.ActiveCheckpointId is null)
        {
            return run.ActiveCheckpointSequence is 0 &&
                   run.ActiveCheckpointStorageKey is null;
        }

        if (!IsCanonicalCheckpointId(
                run.ActiveCheckpointId,
                run.ActiveCheckpointSequence))
        {
            return false;
        }

        return run.ActiveCheckpointStorageKey ==
               CanonicalStorageIdentity(run);
    }

    internal static bool IsCanonicalGeneration(string generation) =>
        generation.Length is 32 &&
        generation[12] is '4' &&
        generation[16] is '8' or '9' or 'a' or 'b' &&
        Guid.TryParseExact(generation, "N", out var parsed) &&
        parsed.ToString("N") == generation;

    internal static bool IsCanonicalCheckpointId(string checkpointId)
    {
        if (checkpointId.Length is not 86 ||
            checkpointId[16] is not '-' ||
            !checkpointId.EndsWith(".json", StringComparison.Ordinal))
        {
            return false;
        }
        for (var index = 0; index < 16; index++)
        {
            if (!IsLowerHex(checkpointId[index]))
                return false;
        }
        for (var index = 17; index < 81; index++)
        {
            if (!IsLowerHex(checkpointId[index]))
                return false;
        }
        return true;
    }

    internal static bool IsCanonicalCheckpointId(
        string checkpointId,
        ulong sequence) =>
        IsCanonicalCheckpointId(checkpointId) &&
        checkpointId.AsSpan(0, 16).SequenceEqual(
            sequence.ToString("x16", CultureInfo.InvariantCulture));

    public WorkflowCheckpointSidecarMetrics Metrics => new(
        Interlocked.Read(ref _liveBytes),
        Interlocked.Read(ref _temporaryOrOrphanBytes));

    public async Task<WorkflowCheckpointBlob> WriteAsync(
        WorkflowProjectionCheckpoint checkpoint,
        CancellationToken ct)
    {
        if (_root is null)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using var buffer = new WorkflowCheckpointMemoryStream(_limits.MaxCheckpointBytes);
                var digest = await SerializeAsync(checkpoint, buffer, ct).ConfigureAwait(false);
                var checkpointId = CheckpointId(checkpoint.JournalSequence, digest);
                var key = StorageKey(checkpoint, checkpointId);
                var bytes = buffer.ToArray();
                var created = _memory.TryAdd(key, bytes);
                lock (_pending)
                    _pending.Add(key);
                if (created)
                {
                    RecordPhysicalMutationCore(
                        key,
                        StorageIdentityFromMemoryKey(key),
                        bytes.LongLength,
                        present: true);
                }
                return new WorkflowCheckpointBlob(checkpointId, bytes.LongLength);
            }
            finally
            {
                _gate.Release();
            }
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var directory = GenerationDirectory(
                checkpoint.ProjectId,
                checkpoint.RunId,
                checkpoint.RunGeneration);
            _files!.CreateDirectory(directory);
            var temporaryPath = ContainedPath(
                directory,
                $".{Guid.NewGuid():N}.tmp");
            try
            {
                string digest;
                long length;
                var temporaryHandle = _files.CreateFile(temporaryPath);
                try
                {
                    await using var stream = new FileStream(
                        temporaryHandle,
                        FileAccess.Write,
                        bufferSize: Math.Max(
                            1,
                            Math.Min(64 * 1024, _limits.MaxCheckpointBytes)),
                        isAsync: false);
                    digest = await SerializeAsync(checkpoint, stream, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                    length = stream.Length;
                }
                finally
                {
                    temporaryHandle.Dispose();
                }

                var checkpointId = CheckpointId(checkpoint.JournalSequence, digest);
                var path = Path.Combine(directory, checkpointId);
                var created = _files.MoveFileNoReplace(temporaryPath, path);
                if (!created)
                {
                    if (await FileMatchesAsync(path, length, digest, ct).ConfigureAwait(false))
                    {
                        _files.DeleteFile(temporaryPath);
                    }
                    else
                    {
                        // The name encodes the content digest, so an existing
                        // file with different bytes is corrupt. This writer
                        // holds the canonical content: replace the corrupt file.
                        _files.DeleteFile(path);
                        created = _files.MoveFileNoReplace(temporaryPath, path);
                        if (!created)
                        {
                            throw new InvalidDataException(
                                "Workflow checkpoint content-addressed path contains different data.");
                        }
                    }
                }

                lock (_pending)
                    _pending.Add(path);
                if (created)
                {
                    RecordPhysicalMutationCore(
                        path,
                        StorageIdentity(
                            checkpoint.ProjectId,
                            checkpoint.RunId,
                            checkpoint.RunGeneration,
                            checkpointId),
                        length,
                        present: true);
                }
                return new WorkflowCheckpointBlob(checkpointId, length);
            }
            catch
            {
                _files.DeleteFile(temporaryPath);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkflowProjectionCheckpoint> ReadAsync(
        WorkflowRunStorageRow run,
        CancellationToken ct)
    {
        if (run.ActiveCheckpointId is null)
            throw new WorkflowCheckpointIncompatibleException(
                "Workflow run has no active checkpoint.");
        if (!HasCanonicalManifest(run))
            throw new WorkflowCheckpointIncompatibleException(
                "Workflow checkpoint manifest identity is invalid.");
        ValidateGeneration(run.RunGeneration);
        byte[] bytes;
        var storageKey = StorageKey(run, run.ActiveCheckpointId);
        if (_root is null)
        {
            if (!_memory.TryGetValue(storageKey, out bytes!))
                throw new WorkflowCheckpointIncompatibleException(
                    "Workflow checkpoint blob is missing.");
            EnsureReadableLength(bytes.LongLength);
        }
        else
        {
            var path = CheckpointFilePath(
                run.ProjectId,
                run.RunId,
                run.RunGeneration,
                run.ActiveCheckpointId);
            if (!_files!.Exists(path))
                throw new FileNotFoundException(
                    "Workflow checkpoint blob is missing.",
                    path);
            bytes = await ReadBytesAsync(path, ct).ConfigureAwait(false);
        }

        ValidateDigest(
            run.ActiveCheckpointId,
            run.ActiveCheckpointSequence,
            bytes);
        var checkpoint = JsonSerializer.Deserialize(
                             bytes,
                             WorkflowStorageJsonContext.Default.WorkflowProjectionCheckpoint)
                         ?? throw new InvalidDataException(
                             "Workflow checkpoint blob is invalid.");
        if (checkpoint.FormatVersion is not 2 ||
            checkpoint.ProjectId != run.ProjectId ||
            checkpoint.RunId != run.RunId ||
            checkpoint.RunGeneration != run.RunGeneration ||
            checkpoint.ProjectorSemanticFingerprint !=
            WorkflowProjectionBuilder.SemanticFingerprint ||
            checkpoint.ProjectionConfigurationFingerprint !=
            _limits.ConfigurationFingerprint ||
            checkpoint.RunInputHash != WorkflowProjectionBuilder.RunInputHash(run) ||
            checkpoint.JournalSequence != run.ActiveCheckpointSequence)
        {
            throw new WorkflowCheckpointIncompatibleException(
                "Workflow checkpoint manifest is incompatible with the current projector.");
        }
        return checkpoint;
    }

    public async Task CompleteCandidateAsync(
        WorkflowRunStorageRow run,
        WorkflowCheckpointBlob blob,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var key = StorageKey(run, blob.CheckpointId);
            lock (_pending)
                _pending.Remove(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyManifestMutationAsync(
        WorkflowCheckpointManifestMutation mutation,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var delta in mutation.Deltas.OrderBy(static item => item.Ordinal))
            {
                SetIdentityActiveCore(
                    _activeStorageIdentities,
                    delta.StorageIdentity,
                    delta.Active,
                    updateCounters: true);
                if (_cycleActiveStorageIdentities is null ||
                    delta.Epoch <= _cycleSnapshotEpoch)
                {
                    continue;
                }

                if (_cycleIdentityOverrides.TryGetValue(
                        delta.StorageIdentity,
                        out var current) &&
                    (current.Epoch > delta.Epoch ||
                     (current.Epoch == delta.Epoch &&
                      current.Ordinal >= delta.Ordinal)))
                {
                    continue;
                }
                _cycleIdentityOverrides[delta.StorageIdentity] = delta;
                SetIdentityActiveCore(
                    _cycleActiveStorageIdentities,
                    delta.StorageIdentity,
                    delta.Active,
                    updateCounters: false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RetireGenerationAsync(
        WorkflowProjectionKey key,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var prefix = StoragePrefix(key);
            if (_root is null)
            {
                foreach (var storageKey in _memory.Keys.Where(
                             candidate => candidate.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    if (_memory.TryRemove(storageKey, out var bytes))
                    {
                        lock (_pending)
                        {
                            _pending.Remove(storageKey);
                        }
                        RecordPhysicalMutationCore(
                            storageKey,
                            StorageIdentityFromMemoryKey(storageKey),
                            bytes.LongLength,
                            present: false);
                    }
                }
                return;
            }

            var directory = GenerationDirectory(
                key.ProjectId,
                key.RunId,
                key.RunGeneration);
            foreach (var file in EnumerateFilesNoFollow(directory))
            {
                var containedFile = EnsureContained(directory, file);
                var length = _files!.Length(containedFile);
                lock (_pending)
                {
                    _pending.Remove(containedFile);
                }
                _files.DeleteFile(containedFile);
                RecordPhysicalMutationCore(
                    containedFile,
                    StorageIdentityFromFile(containedFile),
                    length,
                    present: false);
            }
            foreach (var child in EnumerateDirectoriesBottomUp(directory))
                _files!.DeleteEmptyDirectory(child);
            _files!.DeleteEmptyDirectory(directory);
            var runDirectory = System.IO.Path.GetDirectoryName(directory);
            if (runDirectory is not null)
                _files.DeleteEmptyDirectory(runDirectory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task BeginReconciliationCycleAsync(
        ulong snapshotEpoch,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ResetSweepState();
            _cycleSnapshotEpoch = snapshotEpoch;
            _cyclePhysicalSnapshotVersion = _physicalVersion;
            _cycleActiveStorageIdentities =
                new HashSet<string>(StringComparer.Ordinal);
            _cyclePhysicalEntries =
                new Dictionary<string, WorkflowCheckpointPhysicalEntry>(
                    StringComparer.Ordinal);
            _cycleIdentityOverrides.Clear();
            _cyclePhysicalOverrides.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkflowCheckpointManifestValidation> ValidateManifestsAsync(
        IReadOnlyList<WorkflowRunStorageRow> manifests,
        CancellationToken ct)
    {
        var broken = new List<WorkflowRunStorageRow>();
        var validStorageIdentities = new List<string>();
        var processed = 0;
        long examinedBytes = 0;
        var elapsed = Stopwatch.StartNew();
        foreach (var run in manifests)
        {
            ct.ThrowIfCancellationRequested();
            if (processed > 0 &&
                (examinedBytes >= _limits.CheckpointReconciliationByteLimit ||
                 elapsed.Elapsed >= _limits.CheckpointReconciliationTimeLimit))
            {
                break;
            }

            processed++;
            try
            {
                var key = CanonicalStorageKey(run);
                var length = Length(key);
                examinedBytes = AddSaturating(examinedBytes, Math.Max(0, length));
                await ReadAsync(run, ct).ConfigureAwait(false);
                validStorageIdentities.Add(CanonicalStorageIdentity(run));
            }
            catch (WorkflowCheckpointIncompatibleException)
            {
                // Semantic drift (projector or configuration fingerprint, run
                // input hash) is resolved by the projector's rebuild-and-
                // republish; the manifest stays referenced and the blob must
                // survive the sweep.
                validStorageIdentities.Add(CanonicalStorageIdentity(run));
            }
            catch (Exception error) when (
                error is InvalidDataException or JsonException or FileNotFoundException)
            {
                broken.Add(run);
            }
        }
        return new WorkflowCheckpointManifestValidation(
            broken,
            processed,
            validStorageIdentities);
    }

    public async Task CommitManifestPageAsync(
        IReadOnlyList<string> validStorageIdentities,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var active = _cycleActiveStorageIdentities ??
                         throw new InvalidOperationException(
                             "Workflow checkpoint reconciliation has not started.");
            foreach (var identity in validStorageIdentities)
            {
                if (_cycleIdentityOverrides.ContainsKey(identity))
                    continue;
                active.Add(identity);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkflowCheckpointSweepPage> PrepareSweepPageAsync(
        CancellationToken ct)
    {
        try
        {
            return await PrepareSweepPageCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            await AbortReconciliationCycleAsync(CancellationToken.None)
                .ConfigureAwait(false);
            throw new WorkflowCheckpointReconciliationRestartException(
                "Workflow checkpoint sweep preparation must restart.",
                error);
        }
    }

    private async Task<WorkflowCheckpointSweepPage> PrepareSweepPageCoreAsync(
        CancellationToken ct)
    {
        if (_pendingSweepPage is not null)
            return _pendingSweepPage;

        ct.ThrowIfCancellationRequested();
        var candidates = new List<WorkflowCheckpointSweepCandidate>();
        var examined = 0;
        long examinedBytes = 0;
        var limit = Math.Max(1, _limits.CheckpointSweepLimit);
        var elapsed = Stopwatch.StartNew();
        var sweepComplete = false;

        if (_root is null)
        {
            _memorySweep ??= _memory.GetEnumerator();
            while (CanExamineAnother(
                       examined,
                       examinedBytes,
                       elapsed.Elapsed,
                       limit) &&
                   _memorySweep.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                examined++;
                var pair = _memorySweep.Current;
                examinedBytes = AddSaturating(
                    examinedBytes,
                    pair.Value.LongLength);
                candidates.Add(new WorkflowCheckpointSweepCandidate(
                    pair.Key,
                    StorageIdentityFromMemoryKey(pair.Key),
                    pair.Value.LongLength,
                    RetainWithoutLookup: IsProtected(pair.Key),
                    IsDirectory: false));
            }
            if (examined < limit &&
                examinedBytes < _limits.CheckpointReconciliationByteLimit &&
                elapsed.Elapsed < _limits.CheckpointReconciliationTimeLimit)
            {
                _memorySweep.Dispose();
                _memorySweep = null;
                sweepComplete = true;
            }
        }
        else
        {
            _fileSweep ??= EnumerateFilesNoFollow(_root).GetEnumerator();
            while (!_fileSweepComplete &&
                   CanExamineAnother(
                       examined,
                       examinedBytes,
                       elapsed.Elapsed,
                       limit))
            {
                ct.ThrowIfCancellationRequested();
                if (!_fileSweep.MoveNext())
                {
                    _fileSweep.Dispose();
                    _fileSweep = null;
                    _fileSweepComplete = true;
                    break;
                }

                examined++;
                if (_beforeReconciliation is not null)
                {
                    await _beforeReconciliation(
                            WorkflowCheckpointReconciliationStage.SweepMetadataRead,
                            ct)
                        .ConfigureAwait(false);
                }
                var file = _fileSweep.Current;
                var length = _files!.Length(file);
                examinedBytes = AddSaturating(
                    examinedBytes,
                    Math.Max(0, length));
                candidates.Add(new WorkflowCheckpointSweepCandidate(
                    file,
                    StorageIdentityFromFile(file),
                    length,
                    RetainWithoutLookup: IsProtected(file),
                    IsDirectory: false));
            }

            if (_fileSweepComplete &&
                CanExamineAnother(
                    examined,
                    examinedBytes,
                    elapsed.Elapsed,
                    limit))
            {
                _directorySweep ??= EnumerateDirectoriesBottomUp(_root).GetEnumerator();
                while (CanExamineAnother(
                           examined,
                           examinedBytes,
                           elapsed.Elapsed,
                           limit) &&
                       _directorySweep.MoveNext())
                {
                    ct.ThrowIfCancellationRequested();
                    examined++;
                    candidates.Add(new WorkflowCheckpointSweepCandidate(
                        _directorySweep.Current,
                        null,
                        0,
                        RetainWithoutLookup: false,
                        IsDirectory: true));
                }
                if (examined < limit &&
                    examinedBytes < _limits.CheckpointReconciliationByteLimit &&
                    elapsed.Elapsed < _limits.CheckpointReconciliationTimeLimit)
                {
                    _directorySweep.Dispose();
                    _directorySweep = null;
                    sweepComplete = true;
                }
            }
        }

        _pendingSweepPage = new WorkflowCheckpointSweepPage(
            candidates,
            examined,
            sweepComplete);
        return _pendingSweepPage;
    }

    private async Task AbortReconciliationCycleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ResetSweepState();
            _cycleActiveStorageIdentities = null;
            _cyclePhysicalEntries = null;
            _cycleIdentityOverrides.Clear();
            _cyclePhysicalOverrides.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ApplySweepPageAsync(
        WorkflowCheckpointSweepPage page,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(page, _pendingSweepPage))
                throw new InvalidOperationException(
                    "Workflow checkpoint sweep page is no longer current.");
        }
        finally
        {
            _gate.Release();
        }

        var elapsed = Stopwatch.StartNew();
        var processed = 0;
        while (page.NextCandidateIndex < page.Candidates.Count ||
               page.ClaimedQuarantinePath is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (processed > 0 &&
                elapsed.Elapsed >= _limits.CheckpointReconciliationTimeLimit)
            {
                return false;
            }

            if (page.ClaimedQuarantinePath is null)
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!ReferenceEquals(page, _pendingSweepPage))
                        throw new InvalidOperationException(
                            "Workflow checkpoint sweep page is no longer current.");
                    var candidate = page.Candidates[page.NextCandidateIndex];
                    if (candidate.IsDirectory)
                    {
                        if (!IsQuarantineRoot(candidate.StorageKey))
                            _files!.DeleteEmptyDirectory(candidate.StorageKey);
                        page.NextCandidateIndex++;
                    }
                    else if (!Exists(candidate.StorageKey))
                    {
                        RecordPhysicalMutationCore(
                            candidate.StorageKey,
                            candidate.StorageIdentity,
                            candidate.Length,
                            present: false);
                        page.NextCandidateIndex++;
                    }
                    else
                    {
                        var length = Length(candidate.StorageKey);
                        if (candidate.RetainWithoutLookup ||
                            IsProtected(candidate.StorageKey) ||
                            (candidate.StorageIdentity is { } identity &&
                             (_cycleActiveStorageIdentities?.Contains(identity) ??
                              false)))
                        {
                            ObserveScannedPhysicalCore(
                                candidate.StorageKey,
                                candidate.StorageIdentity,
                                length);
                            page.NextCandidateIndex++;
                        }
                        else if (_root is null)
                        {
                            _memory.TryRemove(candidate.StorageKey, out _);
                            RecordPhysicalMutationCore(
                                candidate.StorageKey,
                                candidate.StorageIdentity,
                                length,
                                present: false);
                            page.NextCandidateIndex++;
                        }
                        else
                        {
                            var source = candidate.StorageKey;
                            var quarantinePath = IsQuarantinePath(source)
                                ? source
                                : NewQuarantinePath(isDirectory: false);
                            if (!IsQuarantinePath(source) &&
                                !_files!.MoveFileToQuarantine(source, quarantinePath))
                            {
                                page.NextCandidateIndex++;
                                continue;
                            }
                            RecordPhysicalMutationCore(
                                source,
                                candidate.StorageIdentity,
                                length,
                                present: false);
                            RecordPhysicalMutationCore(
                                quarantinePath,
                                null,
                                length,
                                present: true);
                            page.ClaimedQuarantinePath = quarantinePath;
                            page.ClaimedLength = length;
                            page.ClaimedIsDirectory = false;
                        }
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            if (page.ClaimedQuarantinePath is { } claimedPath)
            {
                if (_beforeReconciliation is not null)
                {
                    await _beforeReconciliation(
                            WorkflowCheckpointReconciliationStage.SweepClaimed,
                            ct)
                        .ConfigureAwait(false);
                }

                if (!page.ClaimedDeletionCompleted)
                {
                    if (page.ClaimedIsDirectory)
                    {
                        _files!.DeleteEmptyDirectory(claimedPath);
                    }
                    else
                    {
                        _files!.DeleteFile(claimedPath);
                    }
                    page.ClaimedDeletionCompleted = true;
                }

                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!ReferenceEquals(page, _pendingSweepPage))
                        throw new InvalidOperationException(
                            "Workflow checkpoint sweep page is no longer current.");
                    if (!page.ClaimedIsDirectory)
                    {
                        RecordPhysicalMutationCore(
                            claimedPath,
                            null,
                            page.ClaimedLength,
                            present: false);
                    }
                    page.ClaimedQuarantinePath = null;
                    page.ClaimedLength = 0;
                    page.ClaimedIsDirectory = false;
                    page.ClaimedDeletionCompleted = false;
                    page.NextCandidateIndex++;
                }
                finally
                {
                    _gate.Release();
                }
            }
            processed++;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(page, _pendingSweepPage))
                throw new InvalidOperationException(
                    "Workflow checkpoint sweep page is no longer current.");
            _pendingSweepPage = null;
            if (!page.SweepComplete)
                return true;

            ResetSweepState();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteReconciliationCycleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var active = _cycleActiveStorageIdentities ??
                         throw new InvalidOperationException(
                             "Workflow checkpoint reconciliation has not started.");
            var physical = _cyclePhysicalEntries ??
                           throw new InvalidOperationException(
                               "Workflow checkpoint reconciliation has not started.");

            _activeStorageIdentities = active;
            _physicalEntries = physical;
            _physicalKeysByIdentity.Clear();
            long liveBytes = 0;
            long totalBytes = 0;
            foreach (var pair in _physicalEntries)
            {
                totalBytes = checked(totalBytes + pair.Value.Length);
                if (pair.Value.StorageIdentity is not { } identity)
                    continue;
                AddPhysicalIdentityIndex(identity, pair.Key);
                if (_activeStorageIdentities.Contains(identity))
                    liveBytes = checked(liveBytes + pair.Value.Length);
            }

            Interlocked.Exchange(ref _liveBytes, liveBytes);
            Interlocked.Exchange(
                ref _temporaryOrOrphanBytes,
                checked(totalBytes - liveBytes));
            _cycleActiveStorageIdentities = null;
            _cyclePhysicalEntries = null;
            _cycleIdentityOverrides.Clear();
            _cyclePhysicalOverrides.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool CanExamineAnother(
        int examined,
        long examinedBytes,
        TimeSpan elapsed,
        int limit) =>
        examined < limit &&
        (examined is 0 ||
         (examinedBytes < _limits.CheckpointReconciliationByteLimit &&
          elapsed < _limits.CheckpointReconciliationTimeLimit));

    private static long AddSaturating(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private IEnumerable<string> EnumerateDirectoriesBottomUp(string root)
    {
        foreach (var entry in _files!.EnumerateTree(root))
            if (entry.IsDirectory)
                yield return entry.Path;
    }

    private void ResetSweepState()
    {
        _memorySweep?.Dispose();
        _memorySweep = null;
        _fileSweep?.Dispose();
        _fileSweep = null;
        _directorySweep?.Dispose();
        _directorySweep = null;
        _pendingSweepPage = null;
        _fileSweepComplete = false;
    }

    private async Task<string> SerializeAsync(
        WorkflowProjectionCheckpoint checkpoint,
        Stream destination,
        CancellationToken ct)
    {
        using var capped = new WorkflowCheckpointWriteStream(
            destination,
            _limits.MaxCheckpointBytes);
        await JsonSerializer.SerializeAsync(
            capped,
            checkpoint,
            WorkflowStorageJsonContext.Default.WorkflowProjectionCheckpoint,
            ct).ConfigureAwait(false);
        await capped.FlushAsync(ct).ConfigureAwait(false);
        return capped.CompleteDigest();
    }

    private static string CheckpointId(ulong sequence, string digest) =>
        $"{sequence.ToString("x16", CultureInfo.InvariantCulture)}-{digest}.json";

    private async Task<bool> FileMatchesAsync(
        string path,
        long expectedLength,
        string expectedDigest,
        CancellationToken ct)
    {
        try
        {
            using var handle = _files!.OpenFile(path);
            if (RandomAccess.GetLength(handle) != expectedLength)
                return false;
            await using var stream = new FileStream(
                handle,
                FileAccess.Read,
                bufferSize: 64 * 1024,
                isAsync: false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = GC.AllocateUninitializedArray<byte>(
                (int)Math.Min(64 * 1024L, Math.Max(1, expectedLength)));
            long length = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read is 0)
                    break;
                length = checked(length + read);
                if (length > expectedLength)
                    return false;
                hash.AppendData(buffer.AsSpan(0, read));
            }
            return length == expectedLength &&
                   Convert.ToHexStringLower(hash.GetHashAndReset()) == expectedDigest;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private async Task<byte[]> ReadBytesAsync(string path, CancellationToken ct)
    {
        using var handle = _files!.OpenFile(path);
        var length = RandomAccess.GetLength(handle);
        EnsureReadableLength(length);
        var bytes = GC.AllocateUninitializedArray<byte>((int)length);
        await using var stream = new FileStream(
            handle,
            FileAccess.Read,
            bufferSize: Math.Max(1, Math.Min(64 * 1024, bytes.Length)),
            isAsync: false);
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(offset), ct).ConfigureAwait(false);
            if (read is 0)
                throw new InvalidDataException(
                    "Workflow checkpoint blob ended before its declared length.");
            offset += read;
        }
        if (stream.ReadByte() is not -1)
            throw new InvalidDataException(
                "Workflow checkpoint blob grew beyond its bounded length.");
        return bytes;
    }

    private void EnsureReadableLength(long length)
    {
        if (length < 0 || length > _limits.MaxCheckpointBytes)
        {
            throw new InvalidDataException(
                $"Workflow checkpoint blob exceeds the configured maximum of " +
                $"{_limits.MaxCheckpointBytes} bytes.");
        }
    }

    private static void ValidateDigest(
        string checkpointId,
        ulong sequence,
        byte[] bytes)
    {
        var separator = checkpointId.IndexOf('-', StringComparison.Ordinal);
        if (separator < 0 ||
            checkpointId[..separator] !=
            sequence.ToString("x16", CultureInfo.InvariantCulture) ||
            !checkpointId.EndsWith(".json", StringComparison.Ordinal) ||
            checkpointId[(separator + 1)..^5] !=
            Convert.ToHexStringLower(SHA256.HashData(bytes)))
        {
            throw new InvalidDataException("Workflow checkpoint digest is invalid.");
        }
    }

    private string GenerationDirectory(
        string projectId,
        string runId,
        string generation)
    {
        ValidateGeneration(generation);
        var runDirectory = ContainedPath(_root!, RunStorageKey(projectId, runId));
        return ContainedPath(runDirectory, generation);
    }

    private string CheckpointFilePath(
        string projectId,
        string runId,
        string generation,
        string checkpointId)
    {
        ValidateCheckpointId(checkpointId);
        return ContainedPath(
            GenerationDirectory(projectId, runId, generation),
            checkpointId);
    }

    private string StorageKey(
        WorkflowProjectionCheckpoint checkpoint,
        string checkpointId) =>
        _root is null
            ? StorageIdentity(
                checkpoint.ProjectId,
                checkpoint.RunId,
                checkpoint.RunGeneration,
                checkpointId)
            : CheckpointFilePath(
                checkpoint.ProjectId,
                checkpoint.RunId,
                checkpoint.RunGeneration,
                checkpointId);

    private string StorageKey(WorkflowRunStorageRow run, string checkpointId) =>
        _root is null
            ? StorageIdentity(
                run.ProjectId,
                run.RunId,
                run.RunGeneration,
                checkpointId)
            : CheckpointFilePath(run.ProjectId, run.RunId, run.RunGeneration, checkpointId);

    private static string StoragePrefix(WorkflowProjectionKey key)
    {
        ValidateGeneration(key.RunGeneration);
        return $"{RunStorageKey(key.ProjectId, key.RunId)}/{key.RunGeneration}/";
    }

    private static string StorageIdentity(
        string projectId,
        string runId,
        string generation,
        string checkpointId)
    {
        ValidateCheckpointId(checkpointId);
        return $"{StoragePrefix(new WorkflowProjectionKey(
            projectId,
            runId,
            generation))}{checkpointId}";
    }

    private static string RunStorageKey(string projectId, string runId) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{projectId.Length}:{projectId}{runId.Length}:{runId}")));

    private long Length(string storageKey)
    {
        if (_root is null)
            return _memory.TryGetValue(storageKey, out var bytes)
                ? bytes.LongLength
                : 0;
        return _files!.Length(storageKey);
    }

    private bool Exists(string storageKey) =>
        _root is null
            ? _memory.ContainsKey(storageKey)
            : _files!.Exists(storageKey);

    private bool IsProtected(string storageKey)
    {
        lock (_pending)
            return _pending.Contains(storageKey);
    }

    private static void ValidateGeneration(string generation)
    {
        if (!IsCanonicalGeneration(generation))
            throw new InvalidDataException("Workflow run generation identity is invalid.");
    }

    private static void ValidateCheckpointId(string checkpointId)
    {
        if (!IsCanonicalCheckpointId(checkpointId))
            throw new InvalidDataException("Workflow checkpoint identity is invalid.");
    }

    private static void ValidateCheckpointId(string checkpointId, ulong sequence)
    {
        if (!IsCanonicalCheckpointId(checkpointId, sequence))
            throw new InvalidDataException("Workflow checkpoint manifest identity is invalid.");
    }

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static string ContainedPath(string parent, string child)
    {
        var fullParent = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(parent));
        var fullPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(fullParent, child));
        return EnsureContained(fullParent, fullPath);
    }

    private static string EnsureContained(string parent, string path)
    {
        var fullParent = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(parent));
        var fullPath = System.IO.Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(
                $"{fullParent}{System.IO.Path.DirectorySeparatorChar}",
                comparison))
        {
            throw new InvalidDataException(
                "Workflow checkpoint path escapes its storage directory.");
        }
        return fullPath;
    }

    private string NewQuarantinePath(bool isDirectory)
    {
        var suffix = isDirectory ? ".directory" : ".file";
        return System.IO.Path.Combine(
            _quarantineRoot!,
            $"{Guid.NewGuid():N}{suffix}");
    }

    private bool IsQuarantinePath(string path)
    {
        if (_quarantineRoot is null)
            return false;
        var fullPath = System.IO.Path.GetFullPath(path);
        var quarantine = System.IO.Path.TrimEndingDirectorySeparator(
            System.IO.Path.GetFullPath(_quarantineRoot));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(
            $"{quarantine}{System.IO.Path.DirectorySeparatorChar}",
            comparison);
    }

    private bool IsQuarantineRoot(string path)
    {
        if (_quarantineRoot is null)
            return false;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(path)),
            System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(_quarantineRoot)),
            comparison);
    }

    private IEnumerable<string> EnumerateFilesNoFollow(string directory)
    {
        foreach (var entry in _files!.EnumerateTree(directory))
            if (!entry.IsDirectory)
                yield return entry.Path;
    }

    private static string? StorageIdentityFromMemoryKey(string storageKey)
    {
        var parts = storageKey.Split('/');
        return parts.Length is 3 &&
               IsRunStorageKey(parts[0]) &&
               IsCanonicalGeneration(parts[1]) &&
               IsCanonicalCheckpointId(parts[2])
            ? storageKey
            : null;
    }

    private string? StorageIdentityFromFile(string file)
    {
        var fullPath = System.IO.Path.GetFullPath(file);
        var relative = System.IO.Path.GetRelativePath(_root!, fullPath);
        var parts = relative.Split(
            [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length is 3 &&
               IsRunStorageKey(parts[0]) &&
               IsCanonicalGeneration(parts[1]) &&
               IsCanonicalCheckpointId(parts[2])
            ? string.Join('/', parts)
            : null;
    }

    private static bool IsRunStorageKey(string value)
    {
        if (value.Length is not 64)
            return false;
        foreach (var character in value)
        {
            if (!IsLowerHex(character))
                return false;
        }
        return true;
    }

    private void ObserveScannedPhysicalCore(
        string storageKey,
        string? storageIdentity,
        long length)
    {
        var physical = _cyclePhysicalEntries ??
                       throw new InvalidOperationException(
                           "Workflow checkpoint reconciliation has not started.");
        if (_cyclePhysicalOverrides.TryGetValue(storageKey, out var mutation) &&
            mutation.Version > _cyclePhysicalSnapshotVersion)
        {
            return;
        }
        SetPhysicalEntryCore(
            physical,
            storageKey,
            new WorkflowCheckpointPhysicalEntry(storageIdentity, length),
            present: true,
            updateCounters: false);
    }

    private void RecordPhysicalMutationCore(
        string storageKey,
        string? storageIdentity,
        long length,
        bool present)
    {
        var entry = new WorkflowCheckpointPhysicalEntry(
            storageIdentity,
            Math.Max(0, length));
        SetPhysicalEntryCore(
            _physicalEntries,
            storageKey,
            entry,
            present,
            updateCounters: true);

        var version = checked(++_physicalVersion);
        if (_cyclePhysicalEntries is null ||
            version <= _cyclePhysicalSnapshotVersion)
        {
            return;
        }
        var mutation = new WorkflowCheckpointPhysicalOverride(
            version,
            present,
            entry);
        _cyclePhysicalOverrides[storageKey] = mutation;
        SetPhysicalEntryCore(
            _cyclePhysicalEntries,
            storageKey,
            entry,
            present,
            updateCounters: false);
    }

    private void SetPhysicalEntryCore(
        Dictionary<string, WorkflowCheckpointPhysicalEntry> entries,
        string storageKey,
        WorkflowCheckpointPhysicalEntry entry,
        bool present,
        bool updateCounters)
    {
        if (entries.Remove(storageKey, out var previous) && updateCounters)
        {
            RemovePhysicalIdentityIndex(previous.StorageIdentity, storageKey);
            AddMetricBytes(previous, -1);
        }
        if (!present)
            return;
        entries[storageKey] = entry;
        if (!updateCounters)
            return;
        if (entry.StorageIdentity is { } identity)
            AddPhysicalIdentityIndex(identity, storageKey);
        AddMetricBytes(entry, 1);
    }

    private void SetIdentityActiveCore(
        HashSet<string> identities,
        string identity,
        bool active,
        bool updateCounters)
    {
        var changed = active
            ? identities.Add(identity)
            : identities.Remove(identity);
        if (!changed || !updateCounters ||
            !_physicalKeysByIdentity.TryGetValue(identity, out var keys))
        {
            return;
        }

        long bytes = 0;
        foreach (var key in keys)
            bytes = checked(bytes + _physicalEntries[key].Length);
        Interlocked.Add(ref _liveBytes, active ? bytes : -bytes);
        Interlocked.Add(ref _temporaryOrOrphanBytes, active ? -bytes : bytes);
    }

    private void AddMetricBytes(WorkflowCheckpointPhysicalEntry entry, int direction)
    {
        var bytes = checked(entry.Length * direction);
        if (entry.StorageIdentity is { } identity &&
            _activeStorageIdentities.Contains(identity))
        {
            Interlocked.Add(ref _liveBytes, bytes);
        }
        else
        {
            Interlocked.Add(ref _temporaryOrOrphanBytes, bytes);
        }
    }

    private void AddPhysicalIdentityIndex(string identity, string storageKey)
    {
        if (!_physicalKeysByIdentity.TryGetValue(identity, out var keys))
        {
            keys = new HashSet<string>(StringComparer.Ordinal);
            _physicalKeysByIdentity[identity] = keys;
        }
        keys.Add(storageKey);
    }

    private void RemovePhysicalIdentityIndex(string? identity, string storageKey)
    {
        if (identity is null ||
            !_physicalKeysByIdentity.TryGetValue(identity, out var keys))
        {
            return;
        }
        keys.Remove(storageKey);
        if (keys.Count is 0)
            _physicalKeysByIdentity.Remove(identity);
    }

}
