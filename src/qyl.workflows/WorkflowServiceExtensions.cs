using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Workflows.Workflows;

namespace Qyl.Workflows;

/// <summary>
///     Extension methods for registering workflow services.
/// </summary>
public static class WorkflowServiceExtensions
{
    public static IServiceCollection AddQylWorkflows(
        this IServiceCollection services,
        string? workflowsDirectory = null,
        bool autoDiscover = true)
    {
        Guard.NotNull(services);

        services.AddSingleton<WorkflowEngineFactory>(sp =>
        {
            var agent = sp.GetRequiredService<AIAgent>();
            var executionStore = sp.GetService<IExecutionStore>();
            return new WorkflowEngineFactory(agent, workflowsDirectory, autoDiscover, executionStore);
        });

        return services;
    }
}

/// <summary>
///     Factory for creating WorkflowEngine instances.
/// </summary>
public sealed class WorkflowEngineFactory : IAsyncDisposable
{
    private readonly AIAgent _agent;
    private readonly bool _autoDiscover;
    private readonly IExecutionStore? _executionStore;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string? _workflowsDirectory;
    private bool _disposed;
    private WorkflowEngine? _engine;

    public WorkflowEngineFactory(
        AIAgent agent,
        string? workflowsDirectory = null,
        bool autoDiscover = true,
        IExecutionStore? executionStore = null)
    {
        _agent = Guard.NotNull(agent);
        _workflowsDirectory = workflowsDirectory;
        _autoDiscover = autoDiscover;
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
        var engine = new WorkflowEngine(
            _agent,
            _workflowsDirectory,
            TimeProvider.System,
            _executionStore);

        try
        {
            if (_autoDiscover)
                await engine.DiscoverWorkflowsAsync(ct).ConfigureAwait(false);

            return engine;
        }
        catch
        {
            await engine.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
