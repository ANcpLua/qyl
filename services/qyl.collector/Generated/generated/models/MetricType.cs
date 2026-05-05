
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Enums
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MetricType
    {

        [JsonStringEnumMemberName("gauge")]
        Gauge,

        [JsonStringEnumMemberName("sum")]
        Sum,

        [JsonStringEnumMemberName("histogram")]
        Histogram,

        [JsonStringEnumMemberName("exponential_histogram")]
        ExponentialHistogram,

        [JsonStringEnumMemberName("summary")]
        Summary
    }
}
