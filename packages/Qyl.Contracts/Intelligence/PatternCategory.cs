using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

[JsonConverter(typeof(JsonStringEnumConverter<PatternCategory>))]
public enum PatternCategory
{
    Error,

    Latency,

    Cost,

    Availability,

    GenAi,

    Data,

    Agent
}
