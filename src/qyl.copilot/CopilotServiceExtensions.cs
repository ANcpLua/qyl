// =============================================================================
// qyl.copilot - Service Extensions
// DI registration and configuration for Copilot integration
// SDK's UseOpenTelemetry() handles gen_ai.* telemetry; these register qyl-specific sources
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using qyl.copilot.Adapters;
using qyl.copilot.Auth;
using qyl.copilot.Instrumentation;
using qyl.copilot.Workflows;

namespace qyl.copilot;

/// <summary>
///     Extension methods for registering qyl.copilot services.
/// </summary>
public static class CopilotServiceExtensions
{
    /// <summary>
    ///     Adds qyl Copilot services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQylCopilot(
        this IServiceCollection services,
        Action<CopilotOptions>? configure = null)
    {
        Throw.IfNull(services);

        var options = new CopilotOptions();
        configure?.Invoke(options);

        // Register options
        services.AddSingleton(options.AuthOptions);
        services.AddSingleton(options);

        // Register auth provider
        services.AddSingleton<CopilotAuthProvider>(static sp =>
        {
            var authOptions = sp.GetRequiredService<CopilotAuthOptions>();
            return new CopilotAuthProvider(authOptions, TimeProvider.System);
        });

        // Register adapter factory (lazy initialization, resolves tools from DI if available)
        services.AddSingleton<CopilotAdapterFactory>(static sp =>
        {
            var opts = sp.GetRequiredService<CopilotOptions>();
            var tools = sp.GetService<IReadOnlyList<AITool>>();
            return new CopilotAdapterFactory(opts, tools);
        });

        // Register workflow engine factory with adapter getter delegate
        // This avoids CA2213 - the delegate doesn't imply ownership
        services.AddSingleton<WorkflowEngineFactory>(static sp =>
        {
            var opts = sp.GetRequiredService<CopilotOptions>();
            var adapterFactory = sp.GetRequiredService<CopilotAdapterFactory>();
            var executionStore = sp.GetService<IExecutionStore>();
            return new WorkflowEngineFactory(opts, adapterFactory.GetAdapterAsync, executionStore);
        });

        return services;
    }

    /// <summary>
    ///     Adds qyl Copilot tracing instrumentation to OpenTelemetry.
    ///     SDK's UseOpenTelemetry() creates gen_ai.* spans using this source name.
    ///     Call this to capture both SDK gen_ai.* spans and qyl-specific spans.
    /// </summary>
    public static TracerProviderBuilder AddQylCopilotInstrumentation(this TracerProviderBuilder builder)
    {
        Throw.IfNull(builder);

        return builder.AddSource(CopilotInstrumentation.SourceName);
    }

    /// <summary>
    ///     Adds qyl Copilot metrics to OpenTelemetry.
    ///     Includes qyl.copilot.workflow.duration and qyl.copilot.workflow.executions.
    ///     SDK handles gen_ai.client.* metrics automatically.
    /// </summary>
    public static MeterProviderBuilder AddQylCopilotMetrics(this MeterProviderBuilder builder)
    {
        Throw.IfNull(builder);

        return builder.AddMeter(CopilotInstrumentation.MeterName);
    }

    /// <summary>
    ///     Adds qyl Copilot telemetry (both tracing and metrics) to the service collection.
    ///     SDK's UseOpenTelemetry() handles gen_ai.* spans automatically;
    ///     this ensures qyl.copilot sources are registered for capture.
    /// </summary>
    /// <example>
    ///     <code>
    /// // Simple registration:
    /// builder.Services.AddQylCopilotTelemetry();
    /// 
    /// // Or with explicit builder configuration:
    /// builder.Services.AddOpenTelemetry()
    ///     .WithTracing(t => t.AddQylCopilotInstrumentation())
    ///     .WithMetrics(m => m.AddQylCopilotMetrics());
    /// </code>
    /// </example>
    public static IServiceCollection AddQylCopilotTelemetry(this IServiceCollection services)
    {
        Throw.IfNull(services);

        services.AddOpenTelemetry()
            .WithTracing(static builder => builder.AddSource(CopilotInstrumentation.SourceName))
            .WithMetrics(static builder => builder.AddMeter(CopilotInstrumentation.MeterName));

        return services;
    }
}

/// <summary>
///     Configuration options for qyl Copilot integration.
/// </summary>
public sealed class CopilotOptions
{
    /// <summary>
    ///     Authentication options.
    /// </summary>
    public CopilotAuthOptions AuthOptions { get; set; } = new();

    /// <summary>
    ///     Directory containing workflow definitions.
    /// </summary>
    public string? WorkflowsDirectory { get; set; }

    /// <summary>
    ///     Default system instructions for the Copilot agent.
    /// </summary>
    public string? DefaultInstructions { get; set; }

    /// <summary>
    ///     Whether to auto-discover workflows on startup.
    /// </summary>
    public bool AutoDiscoverWorkflows { get; set; } = true;
}

/// <summary>
///     Factory for creating QylCopilotAdapter instances.
///     Implements only IAsyncDisposable because QylCopilotAdapter is async-only.
///     The DI container in .NET 6+ fully supports IAsyncDisposable.
/// </summary>
public sealed class CopilotAdapterFactory : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CopilotOptions _options;
    private readonly IReadOnlyList<AITool>? _tools;
    private QylCopilotAdapter? _adapter;
    private bool _disposed;

    /// <summary>
    ///     Creates a new adapter factory.
    /// </summary>
    public CopilotAdapterFactory(CopilotOptions options, IReadOnlyList<AITool>? tools = null)
    {
        _options = Throw.IfNull(options);
        _tools = tools;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.Dispose();

        if (_adapter is not null)
        {
            await _adapter.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets or creates the shared adapter instance.
    /// </summary>
    public async ValueTask<QylCopilotAdapter> GetAdapterAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var adapter = Volatile.Read(ref _adapter);
        if (adapter is not null) return adapter;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            adapter = Volatile.Read(ref _adapter);
            if (adapter is not null) return adapter;

            adapter = await QylCopilotAdapter.CreateAsync(
                _options.AuthOptions,
                _options.DefaultInstructions,
                _tools,
                TimeProvider.System,
                ct).ConfigureAwait(false);

            Volatile.Write(ref _adapter, adapter);
            return adapter;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Creates a new adapter with custom options.
    /// </summary>
    public Task<QylCopilotAdapter> CreateAdapterAsync(
        CopilotAuthOptions? authOptions = null,
        string? instructions = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return QylCopilotAdapter.CreateAsync(
            authOptions ?? _options.AuthOptions,
            instructions ?? _options.DefaultInstructions,
            _tools,
            TimeProvider.System,
            ct);
    }
}

/// <summary>
///     Factory for creating WorkflowEngine instances.
///     Implements only IAsyncDisposable because WorkflowEngine is async-only.
///     Uses a delegate to get adapters - this avoids CA2213 ownership issues
///     since delegates don't imply ownership of the underlying service.
/// </summary>
public sealed class WorkflowEngineFactory : IAsyncDisposable
{
    private readonly IExecutionStore? _executionStore;
    private readonly Func<CancellationToken, ValueTask<QylCopilotAdapter>> _getAdapterAsync;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CopilotOptions _options;
    private bool _disposed;
    private WorkflowEngine? _engine;

    /// <summary>
    ///     Creates a new workflow engine factory.
    /// </summary>
    /// <param name="options">Copilot configuration options.</param>
    /// <param name="getAdapterAsync">Delegate to get the adapter (typically from CopilotAdapterFactory).</param>
    /// <param name="executionStore">Optional persistent store for workflow executions.</param>
    public WorkflowEngineFactory(
        CopilotOptions options,
        Func<CancellationToken, ValueTask<QylCopilotAdapter>> getAdapterAsync,
        IExecutionStore? executionStore = null)
    {
        _options = Throw.IfNull(options);
        _getAdapterAsync = Throw.IfNull(getAdapterAsync);
        _executionStore = executionStore;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.Dispose();

        if (_engine is not null)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets or creates the shared workflow engine.
    /// </summary>
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
