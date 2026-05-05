
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class HandshakeStartRequest
    {
        [JsonPropertyName("code_challenge")]
        public string CodeChallenge { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }


    }
}
