
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Configurator;

namespace Qyl.Api
{

    public partial class GenerationJobCreateRequest
    {
        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; }

        [JsonPropertyName("job_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public GenerationJobType JobType { get; set; }


    }
}
