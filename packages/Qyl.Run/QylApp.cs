// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.Hosting;

namespace Qyl.Run;

/// <summary>
///     Runnable qyl.run application. Returned by <see cref="QylAppBuilder.Build" />; <c>await app.RunAsync()</c>
///     starts the orchestrator + Spectre UI and blocks until the user hits <c>[Esc]</c> or the
///     host is signaled to stop.
/// </summary>
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
