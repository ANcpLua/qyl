
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Workflow
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WorkflowTriggerType
    {

        [JsonStringEnumMemberName("manual")]
        Manual,

        [JsonStringEnumMemberName("alert")]
        Alert,

        [JsonStringEnumMemberName("schedule")]
        Schedule,

        [JsonStringEnumMemberName("event")]
        EventName,

        [JsonStringEnumMemberName("api")]
        Api,

        [JsonStringEnumMemberName("mcp")]
        Mcp
    }
}
