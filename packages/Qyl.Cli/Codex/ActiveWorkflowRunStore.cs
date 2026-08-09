using System.Text;
using System.Text.Json;

namespace Qyl.Cli.Codex;

internal sealed class ActiveWorkflowRunStore
{
    private const string ActiveFileName = "active-run.json";
    private const string LockFileName = "active-run.lock";

    private readonly string _activePath;
    private readonly string _lockPath;

    public ActiveWorkflowRunStore(string root)
    {
        Directory.CreateDirectory(root);
        Root = root;
        _activePath = Path.Combine(root, ActiveFileName);
        _lockPath = Path.Combine(root, LockFileName);
    }

    public string Root { get; }

    public FileStream Acquire()
    {
        try
        {
            var stream = new FileStream(
                _lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.WriteThrough);
            WorkflowSpoolProtector.RestrictToCurrentUser(_lockPath);
            return stream;
        }
        catch (IOException exception)
        {
            var active = Read();
            var suffix = active is null
                ? string.Empty
                : $" (run {active.RunId}, process {active.ProcessId})";
            throw new InvalidOperationException(
                $"Another `qyl codex` observer is active{suffix}. Only one live observed run is supported per user profile.",
                exception);
        }
    }

    public async Task WriteAsync(
        ActiveWorkflowRun active,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{_activePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(
                active,
                CodexObserverStateJsonContext.Default.ActiveWorkflowRun);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            WorkflowSpoolProtector.RestrictToCurrentUser(temporaryPath);
            File.Move(temporaryPath, _activePath, overwrite: true);
            WorkflowSpoolProtector.RestrictToCurrentUser(_activePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public ActiveWorkflowRun? Read()
    {
        if (!File.Exists(_activePath))
            return null;

        try
        {
            var active = JsonSerializer.Deserialize(
                File.ReadAllText(_activePath),
                CodexObserverStateJsonContext.Default.ActiveWorkflowRun);
            if (active is null || !IsProcessAlive(active.ProcessId))
                return null;
            return active;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Clear(string runId)
    {
        if (!File.Exists(_activePath))
            return;

        var active = Read();
        if (active?.RunId == runId)
            File.Delete(_activePath);
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
