using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal sealed class WorkflowJournalPump(
    WorkflowSpoolStore spoolStore,
    WorkflowCollectorClient collector)
{
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(2);
    // Command id → the moment the control action actually executed. Held across ack
    // retries so a delayed acknowledgement cannot re-stamp the transition later.
    private readonly Dictionary<WorkflowCommandId, DateTimeOffset> _appliedControls = [];
    private string? _lastUploadError;
    private string? _lastControlError;

    public async Task RunUploadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var madeProgress = false;
            foreach (var spool in spoolStore.Enumerate())
            {
                try
                {
                    madeProgress |= await UploadOnceAsync(spool, cancellationToken).ConfigureAwait(false);
                    _lastUploadError = null;
                }
                catch (Exception ex) when (
                    ex is HttpRequestException or IOException or InvalidDataException ||
                    ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    var description = ex.Message;
                    if (_lastUploadError != description)
                    {
                        Console.Error.WriteLine(
                            $"[qyl] Workflow upload is offline; the encrypted spool will retry: {description}");
                        _lastUploadError = description;
                    }
                }
            }

            if (!madeProgress)
                await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<bool> UploadOnceAsync(
        WorkflowSpool spool,
        CancellationToken cancellationToken)
    {
        var metadata = spool.ReadMetadata();
        if (metadata is null || metadata.ThreadId is null && !metadata.Sealed)
            return false;

        await collector.CreateRunAsync(metadata, cancellationToken).ConfigureAwait(false);
        var acknowledged = spool.ReadAcknowledgedSourceSequence();
        var entries = spool.ReadAfter(acknowledged, 100);
        if (entries.Count is 0)
            return false;

        var result = await collector.AppendAsync(
            metadata.RunId,
            entries,
            cancellationToken).ConfigureAwait(false);
        if (result.AcknowledgedSourceSequence < acknowledged)
        {
            throw new InvalidDataException(
                $"Collector acknowledgement moved backward from {acknowledged} to {result.AcknowledgedSourceSequence}.");
        }
        await spool.AcknowledgeAsync(result.AcknowledgedSourceSequence, cancellationToken)
            .ConfigureAwait(false);
        return result.AcknowledgedSourceSequence > acknowledged;
    }

    public async Task RunControlLoopAsync(
        string runId,
        CodexEventNormalizer normalizer,
        ICodexControlClient appServer,
        SemaphoreSlim? normalizerGate,
        CancellationToken cancellationToken)
    {
        ulong cursor = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            WorkflowControlCommandPage page;
            try
            {
                page = await collector.PollControlsAsync(
                    runId,
                    cursor,
                    20_000,
                    cancellationToken).ConfigureAwait(false);
                _lastControlError = null;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or IOException ||
                ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                var description = ex.Message;
                if (_lastControlError != description)
                {
                    Console.Error.WriteLine(
                        $"[qyl] Workflow controls are offline and will retry: {description}");
                    _lastControlError = description;
                }
                await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var pageProcessed = true;
            foreach (var command in page.Commands)
            {
                try
                {
                    if (command.Status is WorkflowControlStatus.Requested)
                    {
                        await collector.UpdateControlAsync(
                            runId,
                            command.CommandId,
                            WorkflowControlStatus.Accepted,
                            null,
                            TimeProvider.System.GetUtcNow(),
                            cancellationToken).ConfigureAwait(false);
                    }
                    if (command.Status is not (WorkflowControlStatus.Requested or WorkflowControlStatus.Accepted))
                        continue;

                    if (!_appliedControls.TryGetValue(command.CommandId, out var appliedAt))
                    {
                        CodexControlTarget target;
                        if (normalizerGate is null)
                        {
                            target = normalizer.ControlTarget;
                        }
                        else
                        {
                            await normalizerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                target = normalizer.ControlTarget;
                            }
                            finally
                            {
                                normalizerGate.Release();
                            }
                        }
                        await ApplyControlAsync(
                            command,
                            target,
                            appServer,
                            cancellationToken).ConfigureAwait(false);
                        appliedAt = TimeProvider.System.GetUtcNow();
                        _appliedControls[command.CommandId] = appliedAt;
                    }
                    await collector.UpdateControlAsync(
                        runId,
                        command.CommandId,
                        WorkflowControlStatus.Applied,
                        null,
                        appliedAt,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is CodexAppServerRequestException or InvalidOperationException)
                {
                    try
                    {
                        await collector.UpdateControlAsync(
                            runId,
                            command.CommandId,
                            WorkflowControlStatus.Failed,
                            ex.Message,
                            TimeProvider.System.GetUtcNow(),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception updateException) when (IsTransient(updateException, cancellationToken))
                    {
                        pageProcessed = false;
                        ReportControlOffline(updateException);
                        break;
                    }
                }
                catch (Exception ex) when (IsTransient(ex, cancellationToken))
                {
                    pageProcessed = false;
                    ReportControlOffline(ex);
                    break;
                }
            }

            if (pageProcessed)
                cursor = page.NextSequence;
            else
                await Task.Delay(s_retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task ApplyControlAsync(
        WorkflowControlCommand command,
        CodexControlTarget target,
        ICodexControlClient appServer,
        CancellationToken cancellationToken)
    {
        if (target.ThreadId is null)
            throw new InvalidOperationException("The observed Codex run has no active root thread.");

        switch (command.Action)
        {
            case WorkflowControlAction.Steer:
                if (target.TurnId is null)
                    throw new InvalidOperationException("Steer requires an active Codex turn.");
                if (string.IsNullOrWhiteSpace(command.Input))
                    throw new InvalidOperationException("Steer requires non-empty input.");
                await appServer.SteerAsync(
                    target.ThreadId,
                    target.TurnId,
                    command.CommandId.Value,
                    command.Input,
                    cancellationToken).ConfigureAwait(false);
                break;
            case WorkflowControlAction.Interrupt:
                if (target.TurnId is null)
                    throw new InvalidOperationException("Interrupt requires an active Codex turn.");
                await appServer.InterruptAsync(
                    target.ThreadId,
                    target.TurnId,
                    cancellationToken).ConfigureAwait(false);
                break;
            case WorkflowControlAction.Resume:
                if (target.TurnId is not null)
                    throw new InvalidOperationException("Resume requires the previous Codex turn to be inactive.");
                if (string.IsNullOrWhiteSpace(command.Input))
                    throw new InvalidOperationException("Resume requires non-empty input.");
                await appServer.ResumeAsync(
                    target.ThreadId,
                    command.CommandId.Value,
                    command.Input,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported workflow control '{command.Action}'.");
        }
    }

    private void ReportControlOffline(Exception exception)
    {
        var description = exception.Message;
        if (_lastControlError == description)
            return;
        Console.Error.WriteLine(
            $"[qyl] Workflow control acknowledgement is offline and will retry: {description}");
        _lastControlError = description;
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or IOException ||
        exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
}
