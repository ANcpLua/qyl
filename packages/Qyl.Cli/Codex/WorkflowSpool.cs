using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Qyl.Cli.Codex;

internal sealed class WorkflowSpool
{
    private const string MetadataFileName = "metadata.qyl";
    private const string EventsFileName = "events.qyl";
    private const string AckFileName = "ack";

    private readonly Channel<byte> _writeLock = CreateWriteLock();
    private readonly WorkflowSpoolProtector _protector;

    public WorkflowSpool(string root, string runId, WorkflowSpoolProtector protector)
    {
        RunId = runId;
        DirectoryPath = Path.Combine(root, "runs", runId);
        Directory.CreateDirectory(DirectoryPath);
        _protector = protector;
    }

    public string RunId { get; }
    public string DirectoryPath { get; }

    private string MetadataPath => Path.Combine(DirectoryPath, MetadataFileName);
    private string EventsPath => Path.Combine(DirectoryPath, EventsFileName);
    private string AckPath => Path.Combine(DirectoryPath, AckFileName);

    public async Task WriteMetadataAsync(
        WorkflowSpoolMetadata metadata,
        CancellationToken cancellationToken)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(
            metadata,
            CodexObserverStateJsonContext.Default.WorkflowSpoolMetadata);
        await WriteEnvelopeAtomicallyAsync(MetadataPath, _protector.Protect(plaintext), cancellationToken)
            .ConfigureAwait(false);
    }

    public WorkflowSpoolMetadata? ReadMetadata()
    {
        if (!File.Exists(MetadataPath))
            return null;
        var envelope = JsonSerializer.Deserialize(
            File.ReadAllText(MetadataPath),
            CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope)
            ?? throw new InvalidDataException($"Workflow spool metadata '{MetadataPath}' is empty.");
        return JsonSerializer.Deserialize(
            _protector.Unprotect(envelope),
            CodexObserverStateJsonContext.Default.WorkflowSpoolMetadata)
            ?? throw new InvalidDataException($"Workflow spool metadata '{MetadataPath}' is invalid.");
    }

    public async Task AppendAsync(WorkflowSpoolEntry entry, CancellationToken cancellationToken)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(
            entry,
            CodexObserverStateJsonContext.Default.WorkflowSpoolEntry);
        var envelope = _protector.Protect(plaintext);
        var line = JsonSerializer.SerializeToUtf8Bytes(
            envelope,
            CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope);
        var record = GC.AllocateUninitializedArray<byte>(line.Length + 1);
        line.CopyTo(record, 0);
        record[^1] = (byte)'\n';

        await _writeLock.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stream = new FileStream(
                EventsPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (stream.ConfigureAwait(false))
            {
                WorkflowSpoolProtector.RestrictToCurrentUser(EventsPath);
                var originalLength = stream.Length;
                stream.Position = originalLength;
                try
                {
                    await stream.WriteAsync(record, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    stream.SetLength(originalLength);
                    stream.Flush(flushToDisk: true);
                    throw;
                }
            }
        }
        finally
        {
            _writeLock.Writer.TryWrite(0);
        }
    }

    public IReadOnlyList<WorkflowSpoolEntry> ReadAfter(ulong acknowledgedSourceSequence, int limit)
    {
        if (!File.Exists(EventsPath))
            return [];

        var entries = new List<WorkflowSpoolEntry>(limit);
        foreach (var line in File.ReadLines(EventsPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var envelope = JsonSerializer.Deserialize(
                line,
                CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope)
                ?? throw new InvalidDataException($"Workflow spool '{EventsPath}' contains an empty envelope.");
            var entry = JsonSerializer.Deserialize(
                _protector.Unprotect(envelope),
                CodexObserverStateJsonContext.Default.WorkflowSpoolEntry)
                ?? throw new InvalidDataException($"Workflow spool '{EventsPath}' contains an invalid entry.");
            if (entry.Event.SourceSequence <= acknowledgedSourceSequence)
                continue;
            entries.Add(entry);
            if (entries.Count == limit)
                break;
        }
        return entries;
    }

    public ulong ReadAcknowledgedSourceSequence()
    {
        if (!File.Exists(AckPath))
            return 0;
        var text = File.ReadAllText(AckPath);
        return ulong.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"Workflow acknowledgement '{AckPath}' is invalid.");
    }

    public Task AcknowledgeAsync(ulong sourceSequence, CancellationToken cancellationToken) =>
        WriteTextAtomicallyAsync(
            AckPath,
            sourceSequence.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

    private async Task WriteEnvelopeAtomicallyAsync(
        string path,
        WorkflowSpoolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            envelope,
            CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope);
        await WriteTextAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteTextAtomicallyAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await _writeLock.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            WorkflowSpoolProtector.RestrictToCurrentUser(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
            WorkflowSpoolProtector.RestrictToCurrentUser(path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            _writeLock.Writer.TryWrite(0);
        }
    }

    private static Channel<byte> CreateWriteLock()
    {
        var channel = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        if (!channel.Writer.TryWrite(0))
            throw new InvalidOperationException("Failed to initialize the workflow spool lock.");
        return channel;
    }
}
