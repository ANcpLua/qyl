using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

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
        using var diagnosticShutdown = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
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
        Task? diagnosticTask = null;
        WorkflowSpool? spool = null;
        WorkflowSpoolMetadata? metadata = null;
        DiagnosticSnapshotInbox? diagnosticInbox = null;
        var diagnosticsPrepared = false;
        var diagnosticsStopped = false;
        using var journalGate = new SemaphoreSlim(1, 1);
        var normalizer = new CodexEventNormalizer();
        var acceptingObserverMessages = true;
        var runCompleted = false;
        using var telemetry = WorkflowTelemetryProjection.Create(
            runId,
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
            diagnosticInbox = new DiagnosticSnapshotInbox(root);
            diagnosticInbox.PrepareRun(runId);
            diagnosticsPrepared = true;
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

            var startBatch = normalizer.StartRun(startedAt);
            await AppendBatchAsync(spool, startBatch, shutdown.Token).ConfigureAwait(false);
            foreach (var workflowEvent in startBatch.Events ?? [])
                telemetry.Record(workflowEvent);

#pragma warning disable CA2025 // Awaited during shutdown before journalGate leaves scope.
            diagnosticTask = RunDiagnosticDrainLoopAsync(
                diagnosticInbox,
                runId,
                normalizer,
                spool,
                telemetry.Record,
                journalGate,
                diagnosticShutdown.Token);
#pragma warning restore CA2025

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
                await journalGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
                try
                {
                    if (!acceptingObserverMessages)
                        return;
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
                }
                finally
                {
                    journalGate.Release();
                }
            };
            await observer.ConnectAsync(endpoint, token, shutdown.Token).ConfigureAwait(false);

            var collector = new WorkflowCollectorClient(
                httpClient,
                s_collectorApi,
                Environment.GetEnvironmentVariable(ApiKeyEnvironment));
            var pump = new WorkflowJournalPump(spoolStore, collector);
            uploadTask = pump.RunUploadLoopAsync(shutdown.Token);
#pragma warning disable CA2025 // Awaited during shutdown before journalGate leaves scope.
            controlTask = RunControlsWhenReadyAsync(
                rootThreadReady.Task,
                runId,
                pump,
                normalizer,
                observer,
                journalGate,
                shutdown.Token);
#pragma warning restore CA2025

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
            activeRuns.Clear(runId);
            diagnosticInbox.CloseRun(runId);
            await StopDiagnosticDrainAsync(
                diagnosticShutdown,
                diagnosticTask,
                diagnosticInbox,
                runId,
                normalizer,
                spool,
                telemetry.Record,
                journalGate).ConfigureAwait(false);
            diagnosticsStopped = true;

            await journalGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                acceptingObserverMessages = false;
                activeRuns.Clear(runId);
                var finalBatch = normalizer.CompleteRun(
                    TimeProvider.System.GetUtcNow(),
                    tui.ExitCode is 0);
                foreach (var workflowEvent in finalBatch.Events ?? [])
                    telemetry.Record(workflowEvent);
                await AppendBatchAsync(spool, finalBatch, CancellationToken.None).ConfigureAwait(false);
                metadata = metadata with { Sealed = true };
                await spool.WriteMetadataAsync(metadata, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                journalGate.Release();
            }
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
            activeRuns.Clear(runId);
            if (!diagnosticsStopped && diagnosticsPrepared)
            {
                diagnosticInbox!.CloseRun(runId);
                await StopDiagnosticDrainAsync(
                    diagnosticShutdown,
                    diagnosticTask,
                    diagnosticInbox,
                    runId,
                    normalizer,
                    spool,
                    telemetry.Record,
                    journalGate).ConfigureAwait(false);
            }

            if (!runCompleted && spool is not null && metadata is not null)
            {
                await journalGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    acceptingObserverMessages = false;
                    activeRuns.Clear(runId);
                    var failedBatch = normalizer.CompleteRun(TimeProvider.System.GetUtcNow(), succeeded: false);
                    foreach (var workflowEvent in failedBatch.Events ?? [])
                        telemetry.Record(workflowEvent);
                    await AppendBatchAsync(spool, failedBatch, CancellationToken.None).ConfigureAwait(false);
                    metadata = metadata with { Sealed = true };
                    await spool.WriteMetadataAsync(metadata, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    journalGate.Release();
                }
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
        SemaphoreSlim journalGate,
        CancellationToken cancellationToken)
    {
        await rootThreadReady.WaitAsync(cancellationToken).ConfigureAwait(false);
        await pump.RunControlLoopAsync(
            runId,
            normalizer,
            observer,
            journalGate,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunDiagnosticDrainLoopAsync(
        DiagnosticSnapshotInbox inbox,
        string runId,
        CodexEventNormalizer normalizer,
        WorkflowSpool spool,
        Action<Qyl.Api.Contracts.Workflow.WorkflowEventAppend> recordTelemetry,
        SemaphoreSlim journalGate,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await DrainDiagnosticsOnceAsync(
                        inbox,
                        runId,
                        normalizer,
                        spool,
                        recordTelemetry,
                        journalGate,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException or InvalidDataException or CryptographicException or JsonException)
            {
                Console.Error.WriteLine(
                    $"[qyl] Diagnostic inbox drain failed (diagnostic_inbox_unreadable): {exception.GetType().Name}");
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task StopDiagnosticDrainAsync(
        CancellationTokenSource diagnosticShutdown,
        Task? diagnosticTask,
        DiagnosticSnapshotInbox inbox,
        string runId,
        CodexEventNormalizer normalizer,
        WorkflowSpool? spool,
        Action<Qyl.Api.Contracts.Workflow.WorkflowEventAppend> recordTelemetry,
        SemaphoreSlim journalGate)
    {
        await diagnosticShutdown.CancelAsync().ConfigureAwait(false);
        if (diagnosticTask is not null)
            await IgnoreExpectedShutdownAsync(diagnosticTask).ConfigureAwait(false);
        if (spool is not null)
        {
            await DrainDiagnosticsOnceAsync(
                    inbox,
                    runId,
                    normalizer,
                    spool,
                    recordTelemetry,
                    journalGate,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    internal static async Task<int> DrainDiagnosticsOnceAsync(
        DiagnosticSnapshotInbox inbox,
        string runId,
        CodexEventNormalizer normalizer,
        WorkflowSpool spool,
        Action<Qyl.Api.Contracts.Workflow.WorkflowEventAppend>? recordTelemetry,
        SemaphoreSlim journalGate,
        CancellationToken cancellationToken)
    {
        var recorded = 0;
        foreach (var request in inbox.ReadPending(runId))
        {
            CodexNormalizedBatch batch;
            await journalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                batch = normalizer.NormalizeDiagnosticSnapshot(
                    request,
                    TimeProvider.System.GetUtcNow());
                if (batch.Events is { Count: > 0 })
                {
                    await AppendBatchAsync(spool, batch, CancellationToken.None).ConfigureAwait(false);
                    normalizer.MarkDiagnosticSnapshotRecorded(request.SnapshotId);
                    foreach (var workflowEvent in batch.Events)
                        recordTelemetry?.Invoke(workflowEvent);
                }
            }
            catch (DiagnosticSnapshotContextUnavailableException)
            {
                continue;
            }
            catch (DiagnosticSnapshotConflictException)
            {
                await inbox.AcknowledgeAsync(
                    request,
                    "failed",
                    "snapshot_conflict",
                    null,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            finally
            {
                journalGate.Release();
            }

            var eventId = batch.Events is { Count: > 0 }
                ? batch.Events[0].EventId.Value
                : $"diagnostic:{request.SnapshotId}";
            await inbox.AcknowledgeAsync(
                request,
                "recorded",
                "recorded",
                eventId,
                cancellationToken).ConfigureAwait(false);
            recorded++;
        }
        return recorded;
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
                    .Where(chunk => workflowEvent.ContentRefs.Contains(chunk.ContentRef))
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
