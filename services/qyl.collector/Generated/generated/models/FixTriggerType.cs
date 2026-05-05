
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Alerting
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FixTriggerType
    {

        [JsonStringEnumMemberName("alert")]
        Alert,

        [JsonStringEnumMemberName("manual")]
        Manual,

        [JsonStringEnumMemberName("mcp")]
        Mcp,

        [JsonStringEnumMemberName("scheduled")]
        Scheduled
    }
}
