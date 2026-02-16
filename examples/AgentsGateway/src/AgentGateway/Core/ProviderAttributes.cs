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

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModelProviderAttribute(
    string providerId,
    string displayName,
    ProviderCapabilities capabilities,
    params string[] authSchemes)
    : Attribute
{
    public string ProviderId { get; } = providerId;
    public string DisplayName { get; } = displayName;
    public ProviderCapabilities Capabilities { get; } = capabilities;
    public string[] AuthSchemes { get; } = authSchemes;
}

public sealed record ModelInfo(
    string ModelId,
    ProviderCapabilities Capabilities,
    IReadOnlyDictionary<string, string> Metadata);

public interface IModelCatalog
{
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
}