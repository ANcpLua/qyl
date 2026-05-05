
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Api.Streaming
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WebSocketMessageType
    {

        [JsonStringEnumMemberName("subscribe")]
        Subscribe,

        [JsonStringEnumMemberName("unsubscribe")]
        Unsubscribe,

        [JsonStringEnumMemberName("data")]
        Data,

        [JsonStringEnumMemberName("error")]
        Error,

        [JsonStringEnumMemberName("ack")]
        Ack,

        [JsonStringEnumMemberName("ping")]
        Ping,

        [JsonStringEnumMemberName("pong")]
        Pong
    }
}
