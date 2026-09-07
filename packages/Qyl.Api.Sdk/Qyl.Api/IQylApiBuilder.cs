using Microsoft.Extensions.DependencyInjection;

namespace Qyl;

/// <summary>The result of <c>AddQylApi</c>.</summary>
/// <remarks>
/// Deliberately empty. Everything a Qyl API needs (reflection-free JSON, validation, problem details, the OpenAPI contract) is
/// registered by <c>AddQylApi</c> itself, because none of it is optional. This builder is the place for genuine choices that
/// arrive later, such as a telemetry exporter; a mandatory step is never surfaced here as a <c>With*</c> method.
/// </remarks>
public interface IQylApiBuilder
{
    /// <summary>The service collection the API is registered into.</summary>
    IServiceCollection Services { get; }
}

/// <summary>The <see cref="IQylApiBuilder"/> <c>AddQylApi</c> returns; public only so the linked <c>AddQylApi</c> can create it.</summary>
/// <param name="services">The service collection the API is registered into.</param>
public sealed class QylApiBuilder(IServiceCollection services) : IQylApiBuilder
{
    /// <inheritdoc />
    public IServiceCollection Services { get; } = services;
}
