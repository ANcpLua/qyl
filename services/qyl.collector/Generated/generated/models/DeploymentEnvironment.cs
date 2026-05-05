
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Ops.Deployment
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentEnvironment
    {

        [JsonStringEnumMemberName("development")]
        Development,

        [JsonStringEnumMemberName("testing")]
        Testing,

        [JsonStringEnumMemberName("staging")]
        Staging,

        [JsonStringEnumMemberName("production")]
        Production,

        [JsonStringEnumMemberName("preview")]
        Preview,

        [JsonStringEnumMemberName("canary")]
        Canary
    }
}
