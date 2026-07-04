using System.Threading.Channels;

namespace Qyl.Run.Internal;

// TUI → orchestrator control channel for user-requested restarts. Deliberately in-process only and never
// exposed over the /runner HTTP surface: control verbs stay on the keyboard, so the read-only API can
// never grow into a mutable product contract.
internal sealed class QylRestartRequests
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<string> Reader => _channel.Reader;

    public void Request(string resourceName)
    {
        _channel.Writer.TryWrite(resourceName);
    }
}
