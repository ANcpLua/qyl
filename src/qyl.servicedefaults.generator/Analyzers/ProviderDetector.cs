using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Detects instrumentation provider packages from compilation references.
/// </summary>
/// <remarks>
///     Uses <see cref="ProviderRegistry" /> as the Single Source of Truth for provider definitions.
/// </remarks>
internal static class ProviderDetector
{
    /// <summary>
    ///     Gets the provider ID for a GenAI system based on type name.
    /// </summary>
    public static string? GetGenAiProviderId(string typeName) =>
        ProviderRegistry.GenAiProviders
            .Where(p => typeName.Contains(p.TypeContains, StringComparison.OrdinalIgnoreCase))
            .Select(static p => p.ProviderId)
            .FirstOrDefault();
}
