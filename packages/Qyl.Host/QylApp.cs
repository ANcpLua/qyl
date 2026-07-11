
using Microsoft.Extensions.Hosting;

namespace Qyl.Host;

public sealed class QylApp(IHost host) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public ValueTask DisposeAsync()
    {
        return ((IAsyncDisposable)host).DisposeAsync();
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return host.RunAsync(cancellationToken);
    }
}
