using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qyl.Cli.Codex;

internal sealed class DiagnosticSnapshotInbox
{
    private const string RequestSuffix = ".request.qyl";
    private const string AcknowledgementSuffix = ".ack.qyl";
    private const string LockFileName = "inbox.lock";
    private const string CurrentRunFileName = "current-run";
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(25);

    private readonly string _inboxDirectory;
    private readonly WorkflowSpoolProtector _protector;

    public DiagnosticSnapshotInbox(string root)
    {
        _inboxDirectory = Path.Combine(root, "diagnostic-inbox");
        Directory.CreateDirectory(_inboxDirectory);
        RestrictDirectory(_inboxDirectory);
        _protector = WorkflowSpoolProtector.Open(root);
    }

    public WorkflowSpoolProtector Protector => _protector;

    public void PrepareRun(string runId)
    {
        using var inboxLock = AcquireLock();
        foreach (var pattern in new[]
                 {
                     $"*{RequestSuffix}",
                     $"*{AcknowledgementSuffix}",
                     $"*{RequestSuffix}.corrupt"
                 })
        {
            foreach (var path in Directory.EnumerateFiles(
                         _inboxDirectory,
                         pattern,
                         SearchOption.TopDirectoryOnly))
            {
                File.Delete(path);
            }
        }
        File.WriteAllText(CurrentRunPath, RunKey(runId), Encoding.ASCII);
        WorkflowSpoolProtector.RestrictToCurrentUser(CurrentRunPath);
    }

    public void CloseRun(string runId)
    {
        using var inboxLock = AcquireLock();
        if (IsCurrentRun(runId) && File.Exists(CurrentRunPath))
            File.Delete(CurrentRunPath);
    }

    public async Task<DiagnosticSnapshotSubmissionResult> SubmitAsync(
        DiagnosticSnapshotInboxRequest request,
        TimeSpan acknowledgementTimeout,
        CancellationToken cancellationToken)
    {
        var key = RequestKey(request.RunId, request.SnapshotId);
        var requestPath = Path.Combine(_inboxDirectory, key + RequestSuffix);
        var acknowledgementPath = Path.Combine(_inboxDirectory, key + AcknowledgementSuffix);

        await using (AcquireLock().ConfigureAwait(false))
        {
            if (!IsCurrentRun(request.RunId))
                return Failure(request.SnapshotId, "run_closing");

            var acknowledgement = ReadAcknowledgement(acknowledgementPath);
            if (acknowledgement is not null)
                return SubmissionFromAcknowledgement(request, acknowledgement);

            var pending = ReadRequest(requestPath);
            if (pending is not null)
            {
                if (!string.Equals(pending.PayloadDigest, request.PayloadDigest, StringComparison.Ordinal))
                    return Failure(request.SnapshotId, "snapshot_conflict");
            }
            else
            {
                await WriteProtectedAtomicallyAsync(
                    requestPath,
                    JsonSerializer.SerializeToUtf8Bytes(
                        request,
                        CodexObserverStateJsonContext.Default.DiagnosticSnapshotInboxRequest),
                    overwrite: false,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(acknowledgementTimeout);
        try
        {
            while (true)
            {
                var acknowledgement = ReadAcknowledgement(acknowledgementPath);
                if (acknowledgement is not null)
                    return SubmissionFromAcknowledgement(request, acknowledgement);
                await Task.Delay(s_pollInterval, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(request.SnapshotId, "ack_timeout");
        }
    }

    public IReadOnlyList<DiagnosticSnapshotInboxRequest> ReadPending(string runId)
    {
        var requests = new List<DiagnosticSnapshotInboxRequest>();
        foreach (var path in Directory.EnumerateFiles(
                     _inboxDirectory,
                     $"*{RequestSuffix}",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var request = ReadRequest(path);
                if (request is not null && request.RunId == runId)
                    requests.Add(request);
            }
            catch (Exception exception) when (IsUnreadableEnvelope(exception))
            {
                Quarantine(path);
                Console.Error.WriteLine(
                    "[qyl] Ignored an unreadable diagnostic inbox request (diagnostic_inbox_unreadable).");
            }
        }
        return requests
            .OrderBy(static request => request.SubmittedAt)
            .ThenBy(static request => request.SnapshotId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task AcknowledgeAsync(
        DiagnosticSnapshotInboxRequest request,
        string status,
        string code,
        string? eventId,
        CancellationToken cancellationToken)
    {
        var acknowledgement = new DiagnosticSnapshotInboxAcknowledgement(
            request.RunId,
            request.SnapshotId,
            request.PayloadDigest,
            status,
            code,
            eventId);
        var key = RequestKey(request.RunId, request.SnapshotId);
        var acknowledgementPath = Path.Combine(_inboxDirectory, key + AcknowledgementSuffix);
        await WriteProtectedAtomicallyAsync(
            acknowledgementPath,
            JsonSerializer.SerializeToUtf8Bytes(
                acknowledgement,
                CodexObserverStateJsonContext.Default.DiagnosticSnapshotInboxAcknowledgement),
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
        var requestPath = Path.Combine(_inboxDirectory, key + RequestSuffix);
        if (File.Exists(requestPath))
            File.Delete(requestPath);
    }

    private static DiagnosticSnapshotSubmissionResult SubmissionFromAcknowledgement(
        DiagnosticSnapshotInboxRequest request,
        DiagnosticSnapshotInboxAcknowledgement acknowledgement)
    {
        if (!string.Equals(acknowledgement.RunId, request.RunId, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.SnapshotId, request.SnapshotId, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.PayloadDigest, request.PayloadDigest, StringComparison.Ordinal))
        {
            return Failure(request.SnapshotId, "snapshot_conflict");
        }
        return new DiagnosticSnapshotSubmissionResult(
            acknowledgement.Status == "recorded",
            acknowledgement.Code,
            request.SnapshotId,
            acknowledgement.EventId);
    }

    private DiagnosticSnapshotInboxRequest? ReadRequest(string path)
    {
        if (!File.Exists(path))
            return null;
        var plaintext = ReadProtected(path);
        try
        {
            return JsonSerializer.Deserialize(
                plaintext,
                CodexObserverStateJsonContext.Default.DiagnosticSnapshotInboxRequest)
                ?? throw new InvalidDataException("The diagnostic inbox contains an empty request.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private DiagnosticSnapshotInboxAcknowledgement? ReadAcknowledgement(string path)
    {
        if (!File.Exists(path))
            return null;
        var plaintext = ReadProtected(path);
        try
        {
            return JsonSerializer.Deserialize(
                plaintext,
                CodexObserverStateJsonContext.Default.DiagnosticSnapshotInboxAcknowledgement)
                ?? throw new InvalidDataException("The diagnostic inbox contains an empty acknowledgement.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private byte[] ReadProtected(string path)
    {
        var envelope = JsonSerializer.Deserialize(
            File.ReadAllText(path),
            CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope)
            ?? throw new InvalidDataException("The diagnostic inbox contains an empty encrypted envelope.");
        return _protector.Unprotect(envelope);
    }

    private async Task WriteProtectedAtomicallyAsync(
        string path,
        byte[] plaintext,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var envelope = _protector.Protect(plaintext);
        var encodedEnvelope = JsonSerializer.Serialize(
            envelope,
            CodexObserverStateJsonContext.Default.WorkflowSpoolEnvelope);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                encodedEnvelope,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            WorkflowSpoolProtector.RestrictToCurrentUser(temporaryPath);
            try
            {
                File.Move(temporaryPath, path, overwrite);
            }
            catch (IOException) when (!overwrite && File.Exists(path))
            {
                return;
            }
            WorkflowSpoolProtector.RestrictToCurrentUser(path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private string CurrentRunPath => Path.Combine(_inboxDirectory, CurrentRunFileName);

    private bool IsCurrentRun(string runId) =>
        File.Exists(CurrentRunPath) &&
        string.Equals(File.ReadAllText(CurrentRunPath), RunKey(runId), StringComparison.Ordinal);

    private FileStream AcquireLock()
    {
        var path = Path.Combine(_inboxDirectory, LockFileName);
        while (true)
        {
            try
            {
                var stream = new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.WriteThrough);
                WorkflowSpoolProtector.RestrictToCurrentUser(path);
                return stream;
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }
        }
    }

    private static string RequestKey(string runId, string snapshotId) =>
        Hash($"{runId}\n{snapshotId}");

    private static string RunKey(string runId) => Hash(runId);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DiagnosticSnapshotSubmissionResult Failure(string snapshotId, string code) =>
        new(false, code, snapshotId, null);

    private static bool IsUnreadableEnvelope(Exception exception) =>
        exception is IOException or InvalidDataException or CryptographicException or JsonException or
            FormatException or ArgumentException or UnauthorizedAccessException;

    private static void Quarantine(string path)
    {
        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A later scan will ignore the same unreadable request again without aborting shutdown.
        }
    }

    private static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
