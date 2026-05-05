
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Api
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HealthStatus
    {

        [JsonStringEnumMemberName("healthy")]
        Healthy,

        [JsonStringEnumMemberName("degraded")]
        Degraded,

        [JsonStringEnumMemberName("unhealthy")]
        Unhealthy
    }
}
