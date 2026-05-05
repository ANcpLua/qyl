
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Error
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ErrorTrend
    {

        [JsonStringEnumMemberName("increasing")]
        Increasing,

        [JsonStringEnumMemberName("decreasing")]
        Decreasing,

        [JsonStringEnumMemberName("stable")]
        Stable,

        [JsonStringEnumMemberName("spike")]
        Spike
    }
}
