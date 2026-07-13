
using Microsoft.Extensions.Hosting;

namespace Qyl.Host;

public sealed class QylApp : IAsyncDisposable
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
