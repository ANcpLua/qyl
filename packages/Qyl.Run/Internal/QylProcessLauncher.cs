using Microsoft.Extensions.Logging;

namespace Qyl.Run.Internal;

// Spawns a child process for a launchable resource and keeps its redirected stdout/stderr drained.
// The orchestrator owns the returned process's lifecycle (tracking + shutdown); this type only starts it.
internal sealed partial class QylProcessLauncher(
    QylLogStore logStore,
    ILogger<QylProcessLauncher> logger)
{
    // Child stdout/stderr are drained on a Debug channel so they never block the child's pipe buffer
    // while staying out of the Spectre.Console live table at the default Information level.
    [LoggerMessage(EventId = QylConstants.LogEvents.ChildStdout, Level = LogLevel.Debug,
        Message = "[{Name}] {Line}")]
    private static partial void LogChildStdout(ILogger logger, string name, string line);

    [LoggerMessage(EventId = QylConstants.LogEvents.ChildStderr, Level = LogLevel.Debug,
        Message = "[{Name}] {Line}")]
    private static partial void LogChildStderr(ILogger logger, string name, string line);

    // Starts the process synchronously and returns it already draining. Ownership transfers to the caller:
    // on success the caller must track and dispose it; on failure this method disposes before throwing.
    public Process Launch(QylResource resource, Uri endpoint)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resource.Launch.Executable,
            WorkingDirectory = resource.Launch.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in resource.Launch.Args) startInfo.ArgumentList.Add(arg);
        foreach (var kv in resource.Launch.Env) startInfo.Environment[kv.Key] = kv.Value;
        startInfo.Environment[QylConstants.Env.AspNetCoreUrls] = endpoint.ToString();

        var process = new Process { StartInfo = startInfo };
        var childName = resource.Name;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { } line) return;
            LogChildStdout(logger, childName, line);
            logStore.Append(childName, isError: false, line);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { } line) return;
            LogChildStderr(logger, childName, line);
            logStore.Append(childName, isError: true, line);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Process.Start returned false for '{resource.Name}'");
            }

            // Begin draining both pipes immediately: a redirected stream that is never read fills the OS
            // pipe buffer and blocks the child on write, so it would never reach its health endpoint.
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return process;
    }
}
