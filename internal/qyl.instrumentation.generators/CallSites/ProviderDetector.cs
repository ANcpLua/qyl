using Qyl.Instrumentation.Generators.Models;
using StringComparison = System.StringComparison;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class ProviderDetector
{
    public static string? GetGenAiProviderId(string typeName) =>
        ProviderRegistry.GenAiProviders
            .Where(p => typeName.Contains(p.TypeContains, StringComparison.OrdinalIgnoreCase))
            .Select(static p => p.ProviderId)
            .FirstOrDefault();
}
