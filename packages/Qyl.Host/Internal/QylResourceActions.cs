using System.Threading.Channels;

namespace Qyl.Host.Internal;

internal enum QylResourceAction
{
    Restart,
    Stop
}

internal enum QylResourceActionStatus
{
    Accepted,
    NotFound,
    Conflict,
    Failed
}

internal readonly record struct QylResourceActionResult(QylResourceActionStatus Status, string? Reason = null);

internal sealed class QylResourceActionRequest(
    string resourceName,
    QylResourceAction action,
    TaskCompletionSource<QylResourceActionResult> completion)
{
    public string ResourceName { get; } = resourceName;

    public QylResourceAction Action { get; } = action;

    internal void Complete(QylResourceActionResult result) => completion.TrySetResult(result);
}

// One in-process command path shared by the TUI and loopback runner API. The orchestrator is the
// single reader and acknowledges only after it has validated the live lifecycle and issued the
// corresponding process action.
internal sealed class QylResourceActions
{
    internal const int Capacity = 32;

    private readonly Channel<QylResourceActionRequest> _channel = Channel.CreateBounded<QylResourceActionRequest>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<QylResourceActionRequest> Reader => _channel.Reader;

    public async Task<QylResourceActionResult> RequestAsync(
        string resourceName,
        QylResourceAction action,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<QylResourceActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(new QylResourceActionRequest(resourceName, action, completion)))
            return new QylResourceActionResult(
                QylResourceActionStatus.Conflict,
                "Resource action queue is at capacity; retry after an in-flight action completes.");

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
