// Copyright (c) 2025-2026 ancplua

using System.Diagnostics;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Owns the <see cref="Process" /> of an LSP server and exposes its stdin / stdout as streams.
///     Disposes via <see cref="IAsyncDisposable" />; <see cref="ExitAsync" /> requests graceful
///     shutdown with a timeout before killing the process.
/// </summary>
internal sealed class LspProcess : IAsyncDisposable
{
    private readonly Process _process;
    private int _disposed;

    private LspProcess(Process process)
    {
        _process = process;
    }

    /// <summary>
    ///     Standard input of the LSP server process — writes go to the server's stdin.
    /// </summary>
    public Stream Stdin => _process.StandardInput.BaseStream;

    /// <summary>
    ///     Standard output of the LSP server process — reads come from the server's stdout.
    /// </summary>
    public Stream Stdout => _process.StandardOutput.BaseStream;

    /// <summary>
    ///     Process id, useful for logging and cleanup hooks.
    /// </summary>
    public int Id => _process.Id;

    /// <summary>
    ///     Starts an LSP server process in the given working directory.
    /// </summary>
    /// <param name="resolution">Resolved server + binary + workspace root.</param>
    /// <returns>A running <see cref="LspProcess" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process fails to start.</exception>
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
            CreateNoWindow = true,
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

    /// <summary>
    ///     Requests graceful shutdown: waits up to <paramref name="timeout" /> for exit,
    ///     then kills the process tree.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for natural exit before killing.</param>
    /// <param name="ct">Cancellation token.</param>
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
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the HasExited check and Kill — benign.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
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
}
