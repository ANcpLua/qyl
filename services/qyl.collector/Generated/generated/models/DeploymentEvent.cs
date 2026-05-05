
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Ops.Deployment
{

    public partial class DeploymentEvent
    {
        [JsonPropertyName("event.name")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentEventName EventName { get; set; }

        [JsonPropertyName("deployment.id")]
        public string DeploymentId { get; set; }

        [JsonPropertyName("service.name")]
        public string ServiceName { get; set; }

        [JsonPropertyName("deployment.environment.name")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentEnvironment Environment { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentStatus Status { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
