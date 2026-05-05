
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class GenerationSelectionSaveRequest
    {
        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; }

        [JsonPropertyName("selected_keys_json")]
        public string SelectedKeysJson { get; set; }


    }
}
