
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Alerting
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AlertSeverity
    {

        [JsonStringEnumMemberName("critical")]
        Critical,

        [JsonStringEnumMemberName("warning")]
        Warning,

        [JsonStringEnumMemberName("info")]
        Info
    }
}
