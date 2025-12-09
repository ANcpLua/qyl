using qyl.Grpc.SemanticConventions;

namespace qyl.Grpc.Models;

public sealed record ResourceModel(IReadOnlyDictionary<string, AttributeValue> Attributes)
{
    public string ServiceName => GetString(ResourceAttributes.Service.Name) ?? "unknown";
    public string? ServiceVersion => GetString(ResourceAttributes.Service.Version);
    public string? ServiceNamespace => GetString(ResourceAttributes.Service.Namespace);
    public string? DeploymentEnvironment => GetString(ResourceAttributes.Deployment.Environment);
    public string? SdkLanguage => GetString(ResourceAttributes.Telemetry.SdkLanguage);
    public string? SdkVersion => GetString(ResourceAttributes.Telemetry.SdkVersion);
    public string? AutoVersion => GetString(ResourceAttributes.Telemetry.AutoVersion);
    public string? HostName => GetString(ResourceAttributes.Host.Name);
    public string? ContainerId => GetString(ResourceAttributes.Container.Id);
    public string? K8SPodName => GetString(ResourceAttributes.K8S.PodName);

    private string? GetString(string key) =>
        Attributes.TryGetValue(key, out var value) && value is StringValue sv ? sv.Value : null;
}
