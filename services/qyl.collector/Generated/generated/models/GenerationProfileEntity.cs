
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Configurator
{

    public partial class GenerationProfileEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        [JsonPropertyName("target_framework")]
        public string TargetFramework { get; set; }

        [JsonPropertyName("target_language")]
        public string TargetLanguage { get; set; }

        [JsonPropertyName("semconv_version")]
        public string SemconvVersion { get; set; }

        [JsonPropertyName("features_json")]
        public string FeaturesJson { get; set; }

        [JsonPropertyName("template_overrides_json")]
        public string TemplateOverridesJson { get; set; }

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }


    }
}
