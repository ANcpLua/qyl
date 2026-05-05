
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api
{

    public partial class DeploymentUpdate
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentStatus? Status { get; set; }

        [JsonPropertyName("healthy_replicas")]
        public int? HealthyReplicas { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }


    }
}
