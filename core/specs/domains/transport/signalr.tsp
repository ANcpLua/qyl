// =============================================================================
// QYL v2.0 - SignalR Semantic Conventions (OTel v1.38)
// =============================================================================
// ASP.NET Core SignalR telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Transport.SignalR;

// =============================================================================
// SignalR Span Attributes
// =============================================================================

@doc("SignalR span attributes")
model SignalRAttributes {
  @doc("SignalR connection ID")
  @encodedName("application/json", "signalr.connection.id")
  connectionId?: string;

  @doc("SignalR hub name")
  @encodedName("application/json", "signalr.hub.name")
  hubName?: string;

  @doc("SignalR hub method name")
  @encodedName("application/json", "signalr.hub.method")
  hubMethod?: string;

  @doc("SignalR transport type")
  @encodedName("application/json", "signalr.transport")
  transport?: SignalRTransport;

  @doc("Connection status")
  @encodedName("application/json", "signalr.connection.status")
  connectionStatus?: SignalRConnectionStatus;

  @doc("Message type being sent/received")
  @encodedName("application/json", "signalr.message.type")
  messageType?: SignalRMessageType;

  @doc("Message size in bytes")
  @encodedName("application/json", "signalr.message.size")
  messageSize?: ByteSize;

  @doc("User identifier (if authenticated)")
  @encodedName("application/json", "signalr.user.id")
  userId?: string;

  @doc("Group name (for group operations)")
  @encodedName("application/json", "signalr.group.name")
  groupName?: string;

  @doc("Client result (for hub method calls)")
  @encodedName("application/json", "signalr.result")
  result?: SignalRResult;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: string;
}

// =============================================================================
// SignalR Enumerations
// =============================================================================

@doc("SignalR transport types")
enum SignalRTransport {
  @doc("WebSocket transport")
  webSockets: "web_sockets",

  @doc("Server-Sent Events transport")
  serverSentEvents: "server_sent_events",

  @doc("Long polling transport")
  longPolling: "long_polling",
}

@doc("SignalR connection status")
enum SignalRConnectionStatus {
  @doc("Connecting")
  connecting: "connecting",

  @doc("Connected")
  connected: "connected",

  @doc("Reconnecting")
  reconnecting: "reconnecting",

  @doc("Disconnected")
  disconnected: "disconnected",
}

@doc("SignalR message types")
enum SignalRMessageType {
  @doc("Invocation message")
  invocation: "invocation",

  @doc("Stream item")
  streamItem: "stream_item",

  @doc("Completion message")
  completion: "completion",

  @doc("Stream invocation")
  streamInvocation: "stream_invocation",

  @doc("Cancel invocation")
  cancelInvocation: "cancel_invocation",

  @doc("Ping message")
  ping: "ping",

  @doc("Close message")
  close: "close",

  @doc("Acknowledgement")
  ack: "ack",

  @doc("Sequence message")
  sequence: "sequence",
}

@doc("SignalR result status")
enum SignalRResult {
  @doc("Success")
  success: "success",

  @doc("Failure")
  failure: "failure",

  @doc("Cancelled")
  cancelled: "cancelled",
}

// =============================================================================
// SignalR Metrics
// =============================================================================

@doc("SignalR active connections metric")
model SignalRActiveConnectionsMetric {
  @doc("Metric name")
  name: "signalr.server.active_connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Hub name")
  @encodedName("application/json", "signalr.hub.name")
  hubName?: string;

  @doc("Transport type")
  @encodedName("application/json", "signalr.transport")
  transport?: SignalRTransport;
}

@doc("SignalR connection duration metric")
model SignalRConnectionDurationMetric {
  @doc("Metric name")
  name: "signalr.server.connection.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Hub name")
  @encodedName("application/json", "signalr.hub.name")
  hubName?: string;

  @doc("Transport type")
  @encodedName("application/json", "signalr.transport")
  transport?: SignalRTransport;

  @doc("Connection status")
  @encodedName("application/json", "signalr.connection.status")
  connectionStatus: SignalRConnectionStatus;
}
