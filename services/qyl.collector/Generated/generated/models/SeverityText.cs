
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Enums
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SeverityText
    {

        [JsonStringEnumMemberName("TRACE")]
        Trace,

        [JsonStringEnumMemberName("DEBUG")]
        Debug,

        [JsonStringEnumMemberName("INFO")]
        Info,

        [JsonStringEnumMemberName("WARN")]
        Warn,

        [JsonStringEnumMemberName("ERROR")]
        Error,

        [JsonStringEnumMemberName("FATAL")]
        Fatal
    }
}
