using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AgentGateway.Core;

[Flags]
public enum ProviderCapabilities
{
    None = 0,
    Chat = 1,
    Tools = 2,
    Streaming = 4,
    Images = 8,
    Audio = 16,
    StructuredOutputs = 32
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModelProviderAttribute : Attribute
{
    public string ProviderId { get; }
    public string DisplayName { get; }
    public ProviderCapabilities Capabilities { get; }
    public string[] AuthSchemes { get; }

    public ModelProviderAttribute(
        string providerId,
        string displayName,
        ProviderCapabilities capabilities,
        params string[] authSchemes)
    {
        ProviderId = providerId;
        DisplayName = displayName;
        Capabilities = capabilities;
        AuthSchemes = authSchemes;
    }
}

public sealed record ModelInfo(
    string ModelId,
    ProviderCapabilities Capabilities,
    IReadOnlyDictionary<string, string> Metadata);

public interface IModelCatalog
{
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
}
