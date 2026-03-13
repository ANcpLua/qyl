using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators.Models;

/// <summary>
///     Canonical compile-time capability manifest discovered by the generator.
/// </summary>
internal readonly record struct CapabilityRegistration(
    EquatableArray<string> AgentNames,
    EquatableArray<string> GenAiProviders,
    EquatableArray<string> GenAiModels,
    EquatableArray<string> GenAiOperations);

/// <summary>
///     A concrete capability bucket with both assembly-attribute and resource-attribute identities.
/// </summary>
internal readonly record struct CapabilityEntry(
    string Kind,
    string ResourceKey,
    EquatableArray<string> Values);

/// <summary>
///     Shared capability naming and projection logic used by all generator pipelines.
/// </summary>
internal static class CapabilityManifest
{
    public const string GeneratedCapabilityAttributeTypeName = "Qyl.Instrumentation.GeneratedCapabilityAttribute";

    private const string AgentKind = "agent";
    private const string GenAiProviderKind = "genai.provider";
    private const string GenAiModelKind = "genai.model";
    private const string GenAiOperationKind = "genai.operation";

    private const string AgentResourceKey = "qyl.capability.agents";
    private const string GenAiProviderResourceKey = "qyl.capability.genai.providers";
    private const string GenAiModelResourceKey = "qyl.capability.genai.models";
    private const string GenAiOperationResourceKey = "qyl.capability.genai.operations";

    public static CapabilityRegistration FromCallSites(
        EquatableArray<GenAiCallSite> genAi,
        EquatableArray<AgentCallSite> agents) =>
        new(
            DistinctOrdered(agents, static callSite => callSite.AgentName),
            DistinctOrdered(genAi, static callSite => callSite.Provider),
            DistinctOrdered(genAi, static callSite => callSite.Model),
            DistinctOrdered(genAi, static callSite => callSite.Operation));

    public static bool IsEmpty(CapabilityRegistration registration) =>
        registration.AgentNames.IsDefaultOrEmpty &&
        registration.GenAiProviders.IsDefaultOrEmpty &&
        registration.GenAiModels.IsDefaultOrEmpty &&
        registration.GenAiOperations.IsDefaultOrEmpty;

    public static IEnumerable<CapabilityEntry> Enumerate(CapabilityRegistration registration)
    {
        yield return new CapabilityEntry(AgentKind, AgentResourceKey, registration.AgentNames);
        yield return new CapabilityEntry(GenAiProviderKind, GenAiProviderResourceKey, registration.GenAiProviders);
        yield return new CapabilityEntry(GenAiModelKind, GenAiModelResourceKey, registration.GenAiModels);
        yield return new CapabilityEntry(GenAiOperationKind, GenAiOperationResourceKey, registration.GenAiOperations);
    }

    public static void CollectGeneratedAttribute(
        AttributeData attribute,
        SortedSet<string> agents,
        SortedSet<string> genAiProviders,
        SortedSet<string> genAiModels,
        SortedSet<string> genAiOperations)
    {
        if (!string.Equals(
                attribute.AttributeClass?.ToDisplayString(),
                GeneratedCapabilityAttributeTypeName,
                StringComparison.Ordinal))
            return;

        if (attribute.ConstructorArguments.Length is not 2 ||
            attribute.ConstructorArguments[0].Value is not string { Length: > 0 } kind ||
            attribute.ConstructorArguments[1].Value is not string { Length: > 0 } value)
            return;

        switch (kind)
        {
            case AgentKind: agents.Add(value); break;
            case GenAiProviderKind: genAiProviders.Add(value); break;
            case GenAiModelKind: genAiModels.Add(value); break;
            case GenAiOperationKind: genAiOperations.Add(value); break;
        }
    }

    private static EquatableArray<string> DistinctOrdered<T>(
        EquatableArray<T> values,
        Func<T, string?> selector)
        where T : class, IEquatable<T>
    {
        if (values.IsDefaultOrEmpty)
            return default;

        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (selector(value) is { Length: > 0 } selected)
                normalized.Add(selected);
        }

        return normalized.Count is 0 ? default : normalized.ToArray().ToEquatableArray();
    }
}
