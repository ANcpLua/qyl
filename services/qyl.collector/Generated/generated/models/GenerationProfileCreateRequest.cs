
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class GenerationProfileCreateRequest
    {
        public string Name { get; set; }

        [JsonPropertyName("target_framework")]
        public string TargetFramework { get; set; }

        public string Description { get; set; }

        [JsonPropertyName("features_json")]
        public string FeaturesJson { get; set; }


    }
}
