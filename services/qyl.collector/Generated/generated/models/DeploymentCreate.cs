
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api
{

    public partial class DeploymentCreate
    {
        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("service_version")]
        public string ServiceVersion { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentEnvironment Environment { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentStrategy Strategy { get; set; }

        [JsonPropertyName("deployed_by")]
        public string DeployedBy { get; set; }

        [JsonPropertyName("git_commit")]
        public string GitCommit { get; set; }

        [JsonPropertyName("git_branch")]
        public string GitBranch { get; set; }


    }
}
