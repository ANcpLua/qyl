
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Common.Pagination
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TimeBucket
    {

        [JsonStringEnumMemberName("1m")]
        Minute,

        [JsonStringEnumMemberName("5m")]
        FiveMinutes,

        [JsonStringEnumMemberName("15m")]
        FifteenMinutes,

        [JsonStringEnumMemberName("1h")]
        Hour,

        [JsonStringEnumMemberName("1d")]
        Day,

        [JsonStringEnumMemberName("1w")]
        Week,

        [JsonStringEnumMemberName("auto")]
        Auto
    }
}
