
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Workspace
{

    public partial class HandshakeSessionEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; set; }

        public string Challenge { get; set; }

        [JsonPropertyName("challenge_method")]
        public string ChallengeMethod { get; set; }

        [JsonPropertyName("browser_fingerprint")]
        public string BrowserFingerprint { get; set; }

        [JsonPropertyName("origin_url")]
        public string OriginUrl { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HandshakeState State { get; set; }

        [JsonPropertyName("verified_at")]
        public DateTimeOffset? VerifiedAt { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }


    }
}
