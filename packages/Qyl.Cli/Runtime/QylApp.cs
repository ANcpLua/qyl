
using Microsoft.Extensions.Hosting;

namespace Qyl.Cli.Runtime;

internal sealed class QylApp : IAsyncDisposable
{
    private readonly IHost _host;

    internal QylApp(IHost host)
    {
        _host = host;
    }

    public ValueTask DisposeAsync()
    {
        return ((IAsyncDisposable)_host).DisposeAsync();
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return _host.RunAsync(cancellationToken);
    }
}
