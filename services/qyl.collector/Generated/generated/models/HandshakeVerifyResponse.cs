
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class HandshakeVerifyResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }


    }
}
