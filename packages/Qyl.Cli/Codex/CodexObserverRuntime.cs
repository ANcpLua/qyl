using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Qyl.Cli.Codex;

internal static class CodexObserverRuntime
{
    private const string CodexExecutableEnvironment = "QYL_CODEX_EXECUTABLE";
    private const string ApiKeyEnvironment = "QYL_API_KEY";
    private const string RemoteTokenEnvironment = "CODEX_REMOTE_AUTH_TOKEN";
    private static readonly Uri s_collectorApi = new("https://api.qyl.at/api/v1/");

    public static async Task<int> RunAsync(
        IReadOnlyList<string> codexArguments,
        CancellationToken cancellationToken = default)
    {
        var codexExecutable =
            Environment.GetEnvironmentVariable(CodexExecutableEnvironment) ?? "codex";
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            throw new InvalidOperationException("The current user profile directory cannot be resolved.");

        var root = Path.Combine(userProfile, ".qyl", "codex");
        var activeRuns = new ActiveWorkflowRunStore(root);
        var activeLock = activeRuns.Acquire();
        await using var activeLockScope = activeLock.ConfigureAwait(false);

        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        var runId = $"run_{Guid.NewGuid():N}";
        var startedAt = TimeProvider.System.GetUtcNow();
        var runtimeDirectory = Path.Combine(root, "runtime", runId);
        Directory.CreateDirectory(runtimeDirectory);
        RestrictDirectory(runtimeDirectory);

        CapturedProcess? appServer = null;
        Process? tui = null;
        CodexAppServerClient? observer = null;
        Task? uploadTask = null;
        Task? controlTask = null;
        WorkflowSpool? spool = null;
        WorkflowSpoolMetadata? metadata = null;
        var normalizer = new CodexEventNormalizer();
        var runCompleted = false;
        using var telemetry = WorkflowTelemetryProjection.Create(
            Environment.GetEnvironmentVariable(ApiKeyEnvironment));
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        try
        {
            var schema = await CodexSchemaVerifier.GenerateAndVerifyAsync(
                codexExecutable,
                Path.Combine(root, "schemas"),
                shutdown.Token).ConfigureAwait(false);

            var spoolStore = new WorkflowSpoolStore(root);
            spool = spoolStore.Open(runId);
            metadata = new WorkflowSpoolMetadata(
                runId,
                null,
                null,
                startedAt,
                schema.CodexVersion,
                schema.SchemaDigest,
                Environment.CurrentDirectory,
                false);
            await spool.WriteMetadataAsync(metadata, shutdown.Token).ConfigureAwait(false);
            await activeRuns.WriteAsync(
                new ActiveWorkflowRun(runId, null, startedAt, Environment.ProcessId),
                shutdown.Token).ConfigureAwait(false);

            await AppendBatchAsync(
                spool,
                normalizer.StartRun(startedAt),
                shutdown.Token).ConfigureAwait(false);

            var token = Base64Url(RandomNumberGenerator.GetBytes(32));
            var tokenPath = Path.Combine(runtimeDirectory, "capability-token");
            await File.WriteAllTextAsync(tokenPath, token, shutdown.Token).ConfigureAwait(false);
            WorkflowSpoolProtector.RestrictToCurrentUser(tokenPath);

            var port = ReserveLoopbackPort();
            var endpoint = new Uri($"ws://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}");
            appServer = StartAppServer(
                codexExecutable,
                endpoint,
                tokenPath,
                Environment.CurrentDirectory);
            await WaitForListenerAsync(port, appServer, shutdown.Token).ConfigureAwait(false);

            observer = new CodexAppServerClient();
            var rootThreadReady = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            observer.MessageReceived += async message =>
            {
                var batch = normalizer.Normalize(message, TimeProvider.System.GetUtcNow());
                if (batch.Events is null || batch.Events.Count is 0)
                    return;

                if (normalizer.RootThreadId is not null &&
                    metadata.ThreadId != normalizer.RootThreadId)
                {
                    metadata = metadata with
                    {
                        ThreadId = normalizer.RootThreadId,
                        Title = normalizer.RootTitle
                    };
                    await spool.WriteMetadataAsync(metadata, shutdown.Token).ConfigureAwait(false);
                    await activeRuns.WriteAsync(
                        new ActiveWorkflowRun(
                            runId,
                            normalizer.RootThreadId,
                            startedAt,
                            Environment.ProcessId),
                        shutdown.Token).ConfigureAwait(false);
                    rootThreadReady.TrySetResult();
                }

                foreach (var workflowEvent in batch.Events)
                    telemetry.Record(workflowEvent);
                await AppendBatchAsync(spool, batch, shutdown.Token).ConfigureAwait(false);
            };
            await observer.ConnectAsync(endpoint, token, shutdown.Token).ConfigureAwait(false);

            var collector = new WorkflowCollectorClient(
                httpClient,
                s_collectorApi,
                Environment.GetEnvironmentVariable(ApiKeyEnvironment));
            var pump = new WorkflowJournalPump(spoolStore, collector);
            uploadTask = pump.RunUploadLoopAsync(shutdown.Token);
            controlTask = RunControlsWhenReadyAsync(
                rootThreadReady.Task,
                runId,
                pump,
                normalizer,
                observer,
                shutdown.Token);

            tui = StartTui(codexExecutable, endpoint, token, codexArguments);
            var tuiExit = tui.WaitForExitAsync(shutdown.Token);
            var appServerExit = appServer.Process.WaitForExitAsync(shutdown.Token);
            var completed = await Task.WhenAny(
                tuiExit,
                appServerExit,
                observer.Completion).ConfigureAwait(false);
            if (completed == appServerExit)
            {
                var error = await appServer.ReadErrorAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Codex app-server exited before the TUI (code {appServer.Process.ExitCode}): {error}");
            }
            if (completed == observer.Completion)
            {
                await observer.Completion.ConfigureAwait(false);
                throw new IOException("The qyl observer connection closed before the Codex TUI exited.");
            }

            await tuiExit.ConfigureAwait(false);
            var finalBatch = normalizer.CompleteRun(
                TimeProvider.System.GetUtcNow(),
                tui.ExitCode is 0);
            foreach (var workflowEvent in finalBatch.Events ?? [])
                telemetry.Record(workflowEvent);
            await AppendBatchAsync(spool, finalBatch, CancellationToken.None).ConfigureAwait(false);
            metadata = metadata with { Sealed = true };
            await spool.WriteMetadataAsync(metadata, CancellationToken.None).ConfigureAwait(false);
            runCompleted = true;

            using var flushTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                while (await pump.UploadOnceAsync(spool, flushTimeout.Token).ConfigureAwait(false))
                {
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or TaskCanceledException)
            {
                Console.Error.WriteLine(
                    $"[qyl] Final workflow events remain safely encrypted in the local spool: {exception.Message}");
            }

            return tui.ExitCode;
        }
        finally
        {
            if (!runCompleted && spool is not null && metadata is not null)
            {
                var failedBatch = normalizer.CompleteRun(TimeProvider.System.GetUtcNow(), succeeded: false);
                foreach (var workflowEvent in failedBatch.Events ?? [])
                    telemetry.Record(workflowEvent);
                await AppendBatchAsync(spool, failedBatch, CancellationToken.None).ConfigureAwait(false);
                metadata = metadata with { Sealed = true };
                await spool.WriteMetadataAsync(metadata, CancellationToken.None).ConfigureAwait(false);
            }

            await shutdown.CancelAsync().ConfigureAwait(false);
            await StopProcessAsync(tui).ConfigureAwait(false);
            tui?.Dispose();
            await StopProcessAsync(appServer?.Process).ConfigureAwait(false);

            if (controlTask is not null)
                await IgnoreExpectedShutdownAsync(controlTask).ConfigureAwait(false);
            if (uploadTask is not null)
                await IgnoreExpectedShutdownAsync(uploadTask).ConfigureAwait(false);
            await DisposeObserverAsync(observer).ConfigureAwait(false);
            if (appServer is not null)
                await appServer.DisposeAsync().ConfigureAwait(false);
            activeRuns.Clear(runId);
            Console.CancelKeyPress -= cancelHandler;
            DeleteRuntimeDirectory(runtimeDirectory);
        }
    }

    public static Task<int> RunBridgeAsync(CancellationToken cancellationToken = default)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            throw new InvalidOperationException("The current user profile directory cannot be resolved.");
        var store = new ActiveWorkflowRunStore(Path.Combine(userProfile, ".qyl", "codex"));
        return ObserverBridgeServer.RunAsync(
            store,
            Console.In,
            Console.Out,
            cancellationToken);
    }

    private static async Task RunControlsWhenReadyAsync(
        Task rootThreadReady,
        string runId,
        WorkflowJournalPump pump,
        CodexEventNormalizer normalizer,
        CodexAppServerClient observer,
        CancellationToken cancellationToken)
    {
        await rootThreadReady.WaitAsync(cancellationToken).ConfigureAwait(false);
        await pump.RunControlLoopAsync(
            runId,
            normalizer,
            observer,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task AppendBatchAsync(
        WorkflowSpool spool,
        CodexNormalizedBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch.Events is null)
            return;

        foreach (var workflowEvent in batch.Events)
        {
            var content = workflowEvent.ContentRefs is null || batch.Content is null
                ? []
                : batch.Content
                    .Where(chunk => workflowEvent.ContentRefs.Contains(
                        chunk.ContentRef,
                        StringComparer.Ordinal))
                    .ToArray();
            await spool.AppendAsync(
                new WorkflowSpoolEntry(workflowEvent, content),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static CapturedProcess StartAppServer(
        string executable,
        Uri endpoint,
        string tokenPath,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "app-server",
                     "--listen",
                     endpoint.ToString().TrimEnd('/'),
                     "--ws-auth",
                     "capability-token",
                     "--ws-token-file",
                     tokenPath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Codex app-server.");
        return new CapturedProcess(process);
    }

    private static Process StartTui(
        string executable,
        Uri endpoint,
        string token,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--remote");
        startInfo.ArgumentList.Add(endpoint.ToString().TrimEnd('/'));
        startInfo.ArgumentList.Add("--remote-auth-token-env");
        startInfo.ArgumentList.Add(RemoteTokenEnvironment);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment[RemoteTokenEnvironment] = token;

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Codex TUI.");
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForListenerAsync(
        int port,
        CapturedProcess appServer,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            if (appServer.Process.HasExited)
            {
                var error = await appServer.ReadErrorAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Codex app-server failed to start (code {appServer.Process.ExitCode}): {error}");
            }

            try
            {
                using var client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(
                    IPAddress.Loopback,
                    port,
                    timeout.Token).ConfigureAwait(false);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50, timeout.Token).ConfigureAwait(false);
            }
        }
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return;
        }
    }

    private static async Task IgnoreExpectedShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private static async Task DisposeObserverAsync(CodexAppServerClient? observer)
    {
        if (observer is not null)
            await observer.DisposeAsync().ConfigureAwait(false);
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void DeleteRuntimeDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private sealed class CapturedProcess : IAsyncDisposable
    {
        private const int MaximumCapturedLines = 64;
        private const int MaximumLineLength = 2_048;

        private readonly Task<string> _stdout;
        private readonly Task<string> _stderr;

        public CapturedProcess(Process process)
        {
            Process = process;
            _stdout = CaptureTailAsync(process.StandardOutput);
            _stderr = CaptureTailAsync(process.StandardError);
        }

        public Process Process { get; }

        public async Task<string> ReadErrorAsync()
        {
            var error = await _stderr.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(error))
                return error;
            return await _stdout.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(_stdout, _stderr).ConfigureAwait(false);
            Process.Dispose();
        }

        private static async Task<string> CaptureTailAsync(StreamReader reader)
        {
            var lines = new Queue<string>(MaximumCapturedLines);
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > MaximumLineLength)
                    line = line[..MaximumLineLength];
                if (lines.Count == MaximumCapturedLines)
                    lines.Dequeue();
                lines.Enqueue(line);
            }
            return string.Join(Environment.NewLine, lines).Trim();
        }
    }
}
