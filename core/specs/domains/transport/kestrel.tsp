// =============================================================================
// QYL v2.0 - Kestrel Semantic Conventions (OTel v1.38)
// =============================================================================
// ASP.NET Core Kestrel web server telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Transport.Kestrel;

// =============================================================================
// Kestrel Span Attributes
// =============================================================================

@doc("Kestrel span attributes")
model KestrelAttributes {
  @doc("Connection ID")
  @encodedName("application/json", "kestrel.connection.id")
  connectionId?: string;

  @doc("Connection status")
  @encodedName("application/json", "kestrel.connection.status")
  connectionStatus?: KestrelConnectionStatus;

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;

  @doc("TLS protocol version")
  @encodedName("application/json", "kestrel.tls.protocol")
  tlsProtocol?: string;

  @doc("Application protocol (HTTP/1.1, HTTP/2, HTTP/3)")
  @encodedName("application/json", "kestrel.application.protocol")
  applicationProtocol?: string;

  @doc("Local endpoint address")
  @encodedName("application/json", "kestrel.local.address")
  localAddress?: IpAddress;

  @doc("Local endpoint port")
  @encodedName("application/json", "kestrel.local.port")
  localPort?: Port;

  @doc("Remote endpoint address")
  @encodedName("application/json", "kestrel.remote.address")
  remoteAddress?: IpAddress;

  @doc("Remote endpoint port")
  @encodedName("application/json", "kestrel.remote.port")
  remotePort?: Port;

  @doc("Request queue length")
  @encodedName("application/json", "kestrel.request_queue.length")
  requestQueueLength?: int32;

  @doc("Connection queue length")
  @encodedName("application/json", "kestrel.connection_queue.length")
  connectionQueueLength?: int32;
}

// =============================================================================
// Kestrel Enumerations
// =============================================================================

@doc("Kestrel connection status")
enum KestrelConnectionStatus {
  @doc("Connection is starting")
  starting: "starting",

  @doc("Connection is active")
  active: "active",

  @doc("Connection is closing")
  closing: "closing",

  @doc("Connection is closed")
  closed: "closed",

  @doc("Connection aborted")
  aborted: "aborted",

  @doc("Connection timed out")
  timedOut: "timed_out",
}

@doc("Kestrel transport types")
enum KestrelTransport {
  @doc("TCP/IP transport")
  sockets: "sockets",

  @doc("Named pipes transport")
  namedPipes: "named_pipes",

  @doc("Unix domain sockets")
  unixDomainSocket: "unix_domain_socket",

  @doc("QUIC transport")
  quic: "quic",
}

// =============================================================================
// Kestrel Metrics
// =============================================================================

@doc("Kestrel active connections metric")
model KestrelActiveConnectionsMetric {
  @doc("Metric name")
  name: "kestrel.active_connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;

  @doc("Local address")
  @encodedName("application/json", "kestrel.local.address")
  localAddress?: IpAddress;

  @doc("Local port")
  @encodedName("application/json", "kestrel.local.port")
  localPort?: Port;
}

@doc("Kestrel connection duration metric")
model KestrelConnectionDurationMetric {
  @doc("Metric name")
  name: "kestrel.connection.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;

  @doc("Connection status")
  @encodedName("application/json", "kestrel.connection.status")
  connectionStatus: KestrelConnectionStatus;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: string;
}

@doc("Kestrel queue length metric")
model KestrelQueueLengthMetric {
  @doc("Metric name")
  name: "kestrel.queued_connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;

  @doc("Local address")
  @encodedName("application/json", "kestrel.local.address")
  localAddress?: IpAddress;

  @doc("Local port")
  @encodedName("application/json", "kestrel.local.port")
  localPort?: Port;
}

@doc("Kestrel request queue length metric")
model KestrelRequestQueueLengthMetric {
  @doc("Metric name")
  name: "kestrel.queued_requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;
}

@doc("Kestrel upgraded connections metric (WebSocket)")
model KestrelUpgradedConnectionsMetric {
  @doc("Metric name")
  name: "kestrel.upgraded_connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;
}

@doc("Kestrel TLS handshake duration metric")
model KestrelTlsHandshakeDurationMetric {
  @doc("Metric name")
  name: "kestrel.tls_handshake.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Transport type")
  @encodedName("application/json", "kestrel.transport")
  transport?: KestrelTransport;

  @doc("TLS protocol version")
  @encodedName("application/json", "kestrel.tls.protocol")
  tlsProtocol?: string;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: string;
}
