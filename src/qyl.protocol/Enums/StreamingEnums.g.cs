// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9039030+00:00
//     Enumeration types for Qyl.Streaming
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Streaming;

/// <summary>Stream event types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StreamEventType>))]
public enum StreamEventType
{
    [System.Runtime.Serialization.EnumMember(Value = "traces")]
    Traces = 0,
    [System.Runtime.Serialization.EnumMember(Value = "spans")]
    Spans = 1,
    [System.Runtime.Serialization.EnumMember(Value = "logs")]
    Logs = 2,
    [System.Runtime.Serialization.EnumMember(Value = "metrics")]
    Metrics = 3,
    [System.Runtime.Serialization.EnumMember(Value = "exceptions")]
    Exceptions = 4,
    [System.Runtime.Serialization.EnumMember(Value = "deployments")]
    Deployments = 5,
    [System.Runtime.Serialization.EnumMember(Value = "all")]
    All = 6,
}

/// <summary>WebSocket message types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WebSocketMessageType>))]
public enum WebSocketMessageType
{
    [System.Runtime.Serialization.EnumMember(Value = "subscribe")]
    Subscribe = 0,
    [System.Runtime.Serialization.EnumMember(Value = "unsubscribe")]
    Unsubscribe = 1,
    [System.Runtime.Serialization.EnumMember(Value = "data")]
    Data = 2,
    [System.Runtime.Serialization.EnumMember(Value = "error")]
    Error = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ack")]
    Ack = 4,
    [System.Runtime.Serialization.EnumMember(Value = "ping")]
    Ping = 5,
    [System.Runtime.Serialization.EnumMember(Value = "pong")]
    Pong = 6,
}

