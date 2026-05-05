
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Alerting
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AlertFiringStatus
    {

        [JsonStringEnumMemberName("firing")]
        Firing,

        [JsonStringEnumMemberName("acknowledged")]
        Acknowledged,

        [JsonStringEnumMemberName("resolved")]
        Resolved,

        [JsonStringEnumMemberName("suppressed")]
        Suppressed
    }
}
