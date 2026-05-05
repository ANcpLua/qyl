
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api.Streaming
{

    public partial class DeploymentStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeName { get; } = "deployment";

        public DeploymentEvent Data { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
