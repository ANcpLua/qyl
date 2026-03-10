using Microsoft.Extensions.DependencyInjection;
using Qyl.Agents;
using Qyl.Agents.Adapters;

namespace Qyl.Workflows;

/// <summary>
///     Extension methods for registering workflow services.
/// </summary>
public static class WorkflowServiceExtensions
{
    public static IServiceCollection AddQylWorkflows(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddSingleton<WorkflowEngineFactory>(static sp =>
        {
            var opts = sp.GetRequiredService<CopilotOptions>();
            var adapterFactory = sp.GetRequiredService<CopilotAdapterFactory>();
            var executionStore = sp.GetService<IExecutionStore>();
            return new WorkflowEngineFactory(opts, adapterFactory.GetAdapterAsync, executionStore);
        });

        return services;
    }
}

/// <summary>
///     Factory for creating WorkflowEngine instances.
/// </summary>
public sealed class WorkflowEngineFactory : IAsyncDisposable
{
    private readonly IExecutionStore? _executionStore;
    private readonly Func<CancellationToken, ValueTask<QylCopilotAdapter>> _getAdapterAsync;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CopilotOptions _options;
    private bool _disposed;
    private WorkflowEngine? _engine;

    public WorkflowEngineFactory(
        CopilotOptions options,
        Func<CancellationToken, ValueTask<QylCopilotAdapter>> getAdapterAsync,
        IExecutionStore? executionStore = null)
    {
        _options = Guard.NotNull(options);
        _getAdapterAsync = Guard.NotNull(getAdapterAsync);
        _executionStore = executionStore;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return default;
        _disposed = true;

        _lock.Dispose();

        return _engine?.DisposeAsync() ?? default;
    }

    public async ValueTask<WorkflowEngine> GetEngineAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var engine = Volatile.Read(ref _engine);
        if (engine is not null) return engine;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            engine = Volatile.Read(ref _engine);
            if (engine is not null) return engine;

            engine = await CreateAndInitializeEngineAsync(ct).ConfigureAwait(false);
            Volatile.Write(ref _engine, engine);
            return engine;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<WorkflowEngine> CreateAndInitializeEngineAsync(CancellationToken ct)
    {
        var adapter = await _getAdapterAsync(ct).ConfigureAwait(false);

        var engine = new WorkflowEngine(
            adapter,
            _options.WorkflowsDirectory,
            TimeProvider.System,
            _executionStore);

        try
        {
            if (_options.AutoDiscoverWorkflows)
            {
                await engine.DiscoverWorkflowsAsync(ct).ConfigureAwait(false);
            }

            return engine;
        }
        catch
        {
            await engine.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
