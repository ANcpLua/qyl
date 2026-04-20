namespace Qyl.Contracts.Intelligence;

using System.Text.Json.Serialization;

/// <summary>Classification category for diagnostic patterns.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PatternCategory>))]
public enum PatternCategory
{
    /// <summary>Exception and error patterns</summary>
    Error,

    /// <summary>Performance degradation</summary>
    Latency,

    /// <summary>Token/cost anomalies</summary>
    Cost,

    /// <summary>Service health patterns</summary>
    Availability,

    /// <summary>GenAI-specific failure modes</summary>
    GenAi,

    /// <summary>Database and storage patterns</summary>
    Data,

    /// <summary>Agent behavioral failure modes (AgentRx taxonomy)</summary>
    Agent
}
