
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Search
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SearchEntityType
    {

        [JsonStringEnumMemberName("span")]
        Span,

        [JsonStringEnumMemberName("log")]
        Log,

        [JsonStringEnumMemberName("issue")]
        Issue,

        [JsonStringEnumMemberName("workflow")]
        Workflow,

        [JsonStringEnumMemberName("deployment")]
        Deployment,

        [JsonStringEnumMemberName("session")]
        Session,

        [JsonStringEnumMemberName("alert")]
        Alert,

        [JsonStringEnumMemberName("fix")]
        Fix
    }
}
