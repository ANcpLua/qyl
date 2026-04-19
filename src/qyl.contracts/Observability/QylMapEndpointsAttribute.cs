namespace Qyl.Contracts.Observability;

/// <summary>
///     Marks a <c>public static</c> extension method (shaped
///     <c>this WebApplication app</c> or <c>this IEndpointRouteBuilder app</c>) as part of
///     the qyl endpoint aggregator. The <c>qyl.instrumentation.generators</c> source generator
///     discovers every tagged method and emits
///     <c>QylGeneratedRegistry.MapQylGeneratedEndpoints(this WebApplication)</c> which calls
///     them all in <see cref="Order" />-ascending, name-stable sequence — replacing a
///     hand-maintained dispatch block.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class QylMapEndpointsAttribute : Attribute
{
    public QylMapEndpointsAttribute(int order = 100) => Order = order;

    /// <summary>
    ///     Ascending order within the aggregator.
    ///     Convention: 0-99 infrastructure (auth, CORS, openapi),
    ///     100-899 feature endpoints (default),
    ///     900-999 late handlers (fallback, SPA).
    /// </summary>
    public int Order { get; }
}
