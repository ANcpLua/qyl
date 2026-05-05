
using Microsoft.Extensions.Hosting;

namespace Qyl.Run;

public sealed class QylApp(IHost host) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) return asyncDisposable.DisposeAsync();
        host.Dispose();
        return ValueTask.CompletedTask;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return host.RunAsync(cancellationToken);
    }
}
