
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Workspace
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HandshakeState
    {

        [JsonStringEnumMemberName("pending")]
        Pending,

        [JsonStringEnumMemberName("verified")]
        Verified,

        [JsonStringEnumMemberName("expired")]
        Expired,

        [JsonStringEnumMemberName("rejected")]
        Rejected
    }
}
