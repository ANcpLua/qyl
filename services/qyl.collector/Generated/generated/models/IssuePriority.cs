
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Issues
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IssuePriority
    {

        [JsonStringEnumMemberName("critical")]
        Critical,

        [JsonStringEnumMemberName("high")]
        High,

        [JsonStringEnumMemberName("medium")]
        Medium,

        [JsonStringEnumMemberName("low")]
        Low
    }
}
