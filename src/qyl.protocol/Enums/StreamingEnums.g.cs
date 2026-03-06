// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2396570+00:00
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
    [System.Text.Json.Serialization.JsonStringEnumMemberName("traces")]
    Traces = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("spans")]
    Spans = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("logs")]
    Logs = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("metrics")]
    Metrics = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployments")]
    Deployments = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("all")]
    All = 5,
}

/// <summary>WebSocket message types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WebSocketMessageType>))]
public enum WebSocketMessageType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("subscribe")]
    Subscribe = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unsubscribe")]
    Unsubscribe = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("data")]
    Data = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("error")]
    Error = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ack")]
    Ack = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ping")]
    Ping = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("pong")]
    Pong = 6,
}

