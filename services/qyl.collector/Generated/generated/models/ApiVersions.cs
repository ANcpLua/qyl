
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Api
{


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApiVersions
    {

        [JsonStringEnumMemberName("2025-12-01")]
        V1,

        [JsonStringEnumMemberName("2026-01-15")]
        V2,

        [JsonStringEnumMemberName("2026-01-26")]
        V3
    }
}
