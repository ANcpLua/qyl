
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Log
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogOrderBy
    {

        [JsonStringEnumMemberName("timestamp_asc")]
        TimestampAsc,

        [JsonStringEnumMemberName("timestamp_desc")]
        TimestampDesc,

        [JsonStringEnumMemberName("severity_asc")]
        SeverityAsc,

        [JsonStringEnumMemberName("severity_desc")]
        SeverityDesc
    }
}
