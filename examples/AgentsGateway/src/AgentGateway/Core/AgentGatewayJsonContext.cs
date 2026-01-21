using System.Text.Json.Serialization;

namespace AgentGateway.Core;

[JsonSerializable(typeof(RegisteredProvider))]
[JsonSerializable(typeof(RegisteredProvider[]))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(ModelInfo[]))]
[JsonSerializable(typeof(ProviderCapabilities))]
internal sealed partial class AgentGatewayJsonContext : JsonSerializerContext
{
}