
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class HandshakeVerifyRequest
    {
        [JsonPropertyName("code_verifier")]
        public string CodeVerifier { get; set; }

        public string Code { get; set; }


    }
}
