using Microsoft.Extensions.DependencyInjection;

namespace Qyl;

/// <summary>
/// The result of <see cref="QylApiServiceCollectionExtensions.AddQylApi(IServiceCollection, System.Text.Json.Serialization.JsonSerializerContext[])"/>.
/// </summary>
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

internal sealed class QylApiBuilder(IServiceCollection services) : IQylApiBuilder
{
    public IServiceCollection Services { get; } = services;
}
