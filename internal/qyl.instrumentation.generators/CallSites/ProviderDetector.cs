using Qyl.Instrumentation.Generators.Models;
using StringComparison = System.StringComparison;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class ProviderDetector
{
    public static string? GetGenAiProviderId(string typeName)
    {
        foreach (var provider in ProviderRegistry.GenAiProviders)
        {
            if (typeName.Contains(provider.TypeContains, StringComparison.OrdinalIgnoreCase))
                return provider.ProviderId;
        }

        return null;
    }
}
