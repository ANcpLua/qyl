
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Api.Streaming
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StreamEventType
    {

        [JsonStringEnumMemberName("traces")]
        Traces,

        [JsonStringEnumMemberName("spans")]
        Spans,

        [JsonStringEnumMemberName("logs")]
        Logs,

        [JsonStringEnumMemberName("metrics")]
        Metrics,

        [JsonStringEnumMemberName("deployments")]
        Deployments,

        [JsonStringEnumMemberName("all")]
        All
    }
}
