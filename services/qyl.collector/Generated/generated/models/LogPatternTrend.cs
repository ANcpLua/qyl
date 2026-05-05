
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Log
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogPatternTrend
    {

        [JsonStringEnumMemberName("increasing")]
        Increasing,

        [JsonStringEnumMemberName("decreasing")]
        Decreasing,

        [JsonStringEnumMemberName("stable")]
        Stable,

        [JsonStringEnumMemberName("new")]
        NewName,

        [JsonStringEnumMemberName("spike")]
        Spike
    }
}
