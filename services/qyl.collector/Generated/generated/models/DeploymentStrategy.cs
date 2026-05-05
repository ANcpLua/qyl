
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Ops.Deployment
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentStrategy
    {

        [JsonStringEnumMemberName("rolling")]
        Rolling,

        [JsonStringEnumMemberName("blue_green")]
        BlueGreen,

        [JsonStringEnumMemberName("canary")]
        Canary,

        [JsonStringEnumMemberName("recreate")]
        Recreate,

        [JsonStringEnumMemberName("ab_test")]
        AbTest,

        [JsonStringEnumMemberName("shadow")]
        Shadow,

        [JsonStringEnumMemberName("feature_flag")]
        FeatureFlag
    }
}
