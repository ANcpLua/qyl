
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Issues
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IssueLevel
    {

        [JsonStringEnumMemberName("debug")]
        Debug,

        [JsonStringEnumMemberName("info")]
        Info,

        [JsonStringEnumMemberName("warning")]
        Warning,

        [JsonStringEnumMemberName("error")]
        Error,

        [JsonStringEnumMemberName("fatal")]
        Fatal
    }
}
