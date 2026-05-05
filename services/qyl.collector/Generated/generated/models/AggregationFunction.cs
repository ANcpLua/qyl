
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Metrics
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AggregationFunction
    {

        [JsonStringEnumMemberName("sum")]
        Sum,

        [JsonStringEnumMemberName("avg")]
        Avg,

        [JsonStringEnumMemberName("min")]
        Min,

        [JsonStringEnumMemberName("max")]
        Max,

        [JsonStringEnumMemberName("count")]
        Count,

        [JsonStringEnumMemberName("last")]
        Last,

        [JsonStringEnumMemberName("rate")]
        Rate,

        [JsonStringEnumMemberName("increase")]
        Increase
    }
}
