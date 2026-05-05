
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Configurator
{

    public partial class GenerationSelectionEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; }

        [JsonPropertyName("selection_type")]
        public string SelectionType { get; set; }

        [JsonPropertyName("selection_key")]
        public string SelectionKey { get; set; }

        public bool Enabled { get; set; }

        [JsonPropertyName("config_json")]
        public string ConfigJson { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }


    }
}
