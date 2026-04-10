using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Scoped per-request capability set that controls which Loom capabilities
///     are granted and whether destructive tool invocations require explicit approval.
///     Register as scoped in DI so that each request scope receives its own grant set.
/// </summary>
public sealed class LoomCapabilityContext
{
    private readonly HashSet<string> _grantedCapabilities;
    private readonly Func<string, CancellationToken, ValueTask>? _approvalHandler;

    public LoomCapabilityContext(
        IEnumerable<string>? grantedCapabilities = null,
        Func<string, CancellationToken, ValueTask>? approvalHandler = null)
    {
        _grantedCapabilities = grantedCapabilities is not null
            ? new HashSet<string>(grantedCapabilities, StringComparer.Ordinal)
            : [];
        _approvalHandler = approvalHandler;
    }

    /// <summary>
    ///     Verifies that all <paramref name="requiredCapabilities"/> are currently granted.
    ///     Throws <see cref="LoomCapabilityDeniedException"/> on the first missing capability.
    /// </summary>
    public void Verify(IReadOnlyList<string> requiredCapabilities)
    {
        foreach (var capability in requiredCapabilities)
        {
            if (!_grantedCapabilities.Contains(capability))
                throw new LoomCapabilityDeniedException(
                    $"Loom capability denied: '{capability}' is required but not granted. Grant it via LoomCapabilityContext.");
        }
    }

    /// <summary>
    ///     Requests approval for <paramref name="toolName"/> via the configured handler.
    ///     Throws <see cref="LoomApprovalRequiredException"/> if no handler is registered.
    /// </summary>
    public async ValueTask RequestApprovalAsync(string toolName, CancellationToken cancellationToken)
    {
        if (_approvalHandler is null)
            throw new LoomApprovalRequiredException(toolName, false);
        await _approvalHandler(toolName, cancellationToken).ConfigureAwait(false);
    }

    public void Grant(string capability) => _grantedCapabilities.Add(capability);
    public void Revoke(string capability) => _grantedCapabilities.Remove(capability);
    public bool HasCapability(string capability) => _grantedCapabilities.Contains(capability);
}

/// <summary>
///     Thrown when a required Loom capability is not present in the current
///     <see cref="LoomCapabilityContext"/>. Grant the missing capability before retrying.
/// </summary>
public sealed class LoomCapabilityDeniedException : InvalidOperationException
{
    public LoomCapabilityDeniedException() : base("Loom capability denied.") { }
    public LoomCapabilityDeniedException(string message) : base(message) { }
    public LoomCapabilityDeniedException(string message, Exception innerException) : base(message, innerException) { }

    public LoomCapabilityDeniedException(string capability, string toolName, IReadOnlyList<string> grantedCapabilities)
        : base($"Loom capability denied: '{capability}' is required by tool '{toolName}' but not granted. " +
               $"Granted: [{string.Join(", ", grantedCapabilities)}]. Grant it via LoomCapabilityContext.")
    {
        Capability = capability;
        ToolName = toolName;
        GrantedCapabilities = grantedCapabilities;
    }

    public string? Capability { get; }
    public string? ToolName { get; }
    public IReadOnlyList<string>? GrantedCapabilities { get; }
}

/// <summary>
///     Thrown when a Loom tool requires approval but no approval handler is configured
///     in the current <see cref="LoomCapabilityContext"/>.
/// </summary>
public sealed class LoomApprovalRequiredException : InvalidOperationException
{
    public LoomApprovalRequiredException() : base("Loom approval required.") { }
    public LoomApprovalRequiredException(string message) : base(message) { }
    public LoomApprovalRequiredException(string message, Exception innerException) : base(message, innerException) { }

    public LoomApprovalRequiredException(string toolName, bool isDestructive)
        : base($"Loom tool '{toolName}' requires approval but no approval handler is configured. " +
               "Register an approval handler via LoomCapabilityContext.")
    {
        ToolName = toolName;
        IsDestructive = isDestructive;
    }

    public string? ToolName { get; }
    public bool IsDestructive { get; }
}

/// <summary>
///     <see cref="DelegatingAIFunction"/> that enforces compiler-emitted governance metadata
///     before delegating to the inner <see cref="AIFunction"/>.
///     Enforcement order: capability verification, approval gate, budget reservation, concurrency slot.
///     Budget is committed only on successful invocation; rolled back automatically on failure.
/// </summary>
public sealed class LoomGovernedAIFunction : DelegatingAIFunction
{
    private readonly LoomRuntimeMetadataDescriptor _metadata;
    private readonly LoomBudgetEnforcer _budget;
    private readonly LoomConcurrencyManager _concurrency;
    private readonly LoomCapabilityContext _capabilities;

    public LoomGovernedAIFunction(
        AIFunction inner,
        LoomRuntimeMetadataDescriptor metadata,
        LoomBudgetEnforcer budget,
        LoomConcurrencyManager concurrency,
        LoomCapabilityContext capabilities) : base(inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(concurrency);
        ArgumentNullException.ThrowIfNull(capabilities);
        _metadata = metadata;
        _budget = budget;
        _concurrency = concurrency;
        _capabilities = capabilities;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // 1. Capability verification
        if (_metadata.Policy.RequiredCapabilities.Count > 0)
            _capabilities.Verify(_metadata.Policy.RequiredCapabilities);

        // 2. Approval gate for destructive actions
        if (_metadata.Policy.RequiresApproval)
            await _capabilities.RequestApprovalAsync(_metadata.Name, cancellationToken).ConfigureAwait(false);

        // 3. Budget reservation with automatic rollback
        await using var reservation = _budget.ReserveAttempt(_metadata.Name, _metadata.Policy);

        // 4. Concurrency slot with automatic release
        await using var slot = await _concurrency.AcquireAsync(
            _metadata.Name, _metadata.Policy, cancellationToken).ConfigureAwait(false);

        // 5. Invoke the inner function (which may be InstrumentedAIFunction -> factory-created)
        var result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);

        // 6. Commit budget on success
        reservation.Commit();

        return result;
    }
}
