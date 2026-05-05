
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Api
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DoraPerformanceLevel
    {

        [JsonStringEnumMemberName("elite")]
        Elite,

        [JsonStringEnumMemberName("high")]
        High,

        [JsonStringEnumMemberName("medium")]
        Medium,

        [JsonStringEnumMemberName("low")]
        Low
    }
}
