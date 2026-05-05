
namespace qyl.mcp.Tools.Lsp;

internal sealed class LspProcess : IAsyncDisposable
{
    private readonly Process _process;
    private int _disposed;

    private LspProcess(Process process) => _process = process;

    public Stream Stdin => _process.StandardInput.BaseStream;

    public Stream Stdout => _process.StandardOutput.BaseStream;

    public int Id => _process.Id;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        try
        {
            await ExitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _process.Dispose();
        }
    }

    public static LspProcess Start(LspServerResolutionResult resolution)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resolution.BinaryPath,
            WorkingDirectory = resolution.WorkspaceRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in resolution.Definition.Arguments)
            startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException(
                $"Failed to start LSP server '{resolution.Definition.Id}' at {resolution.BinaryPath}.");
        }

        return new LspProcess(process);
    }

    public async Task ExitAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (_process.HasExited)
            return;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await _process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill();
        }
    }

    private void TryKill()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
