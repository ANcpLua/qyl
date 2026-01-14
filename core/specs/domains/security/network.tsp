// =============================================================================
// QYL v2.0 - Network Semantic Conventions (OTel v1.39)
// =============================================================================
// Network layer telemetry attributes following OTel specifications.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Security.Network;

// =============================================================================
// Network Span Attributes
// =============================================================================

@doc("Network-related span attributes")
model NetworkAttributes {
  @doc("OSI application layer protocol")
  @encodedName("application/json", "network.protocol.name")
  protocolName?: NetworkProtocolName;

  @doc("Version of the protocol")
  @encodedName("application/json", "network.protocol.version")
  protocolVersion?: string;

  @doc("OSI transport layer protocol")
  @encodedName("application/json", "network.transport")
  transport?: NetworkTransport;

  @doc("OSI network layer protocol (e.g., IPv4, IPv6)")
  @encodedName("application/json", "network.type")
  networkType?: NetworkType;

  @doc("Local socket address")
  @encodedName("application/json", "network.local.address")
  localAddress?: IpAddress;

  @doc("Local socket port")
  @encodedName("application/json", "network.local.port")
  localPort?: Port;

  @doc("Remote socket address")
  @encodedName("application/json", "network.peer.address")
  peerAddress?: IpAddress;

  @doc("Remote socket port")
  @encodedName("application/json", "network.peer.port")
  peerPort?: Port;

  @doc("Network connection type (for mobile)")
  @encodedName("application/json", "network.connection.type")
  connectionType?: NetworkConnectionType;

  @doc("Network connection subtype (e.g., LTE)")
  @encodedName("application/json", "network.connection.subtype")
  connectionSubtype?: NetworkConnectionSubtype;

  @doc("Mobile carrier name")
  @encodedName("application/json", "network.carrier.name")
  carrierName?: string;

  @doc("Mobile carrier MCC (country code)")
  @encodedName("application/json", "network.carrier.mcc")
  carrierMcc?: string;

  @doc("Mobile carrier MNC (network code)")
  @encodedName("application/json", "network.carrier.mnc")
  carrierMnc?: string;

  @doc("Mobile carrier ICC (country code)")
  @encodedName("application/json", "network.carrier.icc")
  carrierIcc?: string;

  @doc("Amount of data received in bytes")
  @encodedName("application/json", "network.io.receive.bytes")
  ioReceiveBytes?: ByteSize;

  @doc("Amount of data sent in bytes")
  @encodedName("application/json", "network.io.send.bytes")
  ioSendBytes?: ByteSize;

  @doc("Total packets sent")
  @encodedName("application/json", "network.io.send.packets")
  ioSendPackets?: Count;

  @doc("Total packets received")
  @encodedName("application/json", "network.io.receive.packets")
  ioReceivePackets?: Count;
}

// =============================================================================
// Network Enumerations
// =============================================================================

@doc("OSI application layer protocol names")
enum NetworkProtocolName {
  @doc("HTTP protocol")
  http: "http",

  @doc("HTTPS protocol")
  https: "https",

  @doc("HTTP/2 protocol")
  http2: "http2",

  @doc("HTTP/3 (QUIC)")
  http3: "http3",

  @doc("gRPC protocol")
  grpc: "grpc",

  @doc("WebSocket protocol")
  websocket: "websocket",

  @doc("AMQP protocol")
  amqp: "amqp",

  @doc("MQTT protocol")
  mqtt: "mqtt",

  @doc("Kafka protocol")
  kafka: "kafka",

  @doc("Redis protocol")
  redis: "redis",

  @doc("MySQL protocol")
  mysql: "mysql",

  @doc("PostgreSQL protocol")
  postgresql: "postgresql",

  @doc("MongoDB protocol")
  mongodb: "mongodb",

  @doc("FTP protocol")
  ftp: "ftp",

  @doc("SFTP protocol")
  sftp: "sftp",

  @doc("SSH protocol")
  ssh: "ssh",

  @doc("SMTP protocol")
  smtp: "smtp",

  @doc("DNS protocol")
  dns: "dns",

  @doc("NTP protocol")
  ntp: "ntp",
}

@doc("OSI transport layer protocols")
enum NetworkTransport {
  @doc("TCP transport")
  tcp: "tcp",

  @doc("UDP transport")
  udp: "udp",

  @doc("Unix domain socket")
  unix: "unix",

  @doc("Named pipe (Windows)")
  pipe: "pipe",

  @doc("QUIC transport")
  quic: "quic",
}

@doc("OSI network layer types")
enum NetworkType {
  @doc("IPv4 network")
  ipv4: "ipv4",

  @doc("IPv6 network")
  ipv6: "ipv6",
}

@doc("Network connection types (for mobile)")
enum NetworkConnectionType {
  @doc("WiFi connection")
  wifi: "wifi",

  @doc("Wired ethernet")
  wired: "wired",

  @doc("Cellular connection")
  cell: "cell",

  @doc("Unavailable")
  unavailable: "unavailable",

  @doc("Unknown")
  `unknown`: "unknown",
}

@doc("Network connection subtypes (cellular)")
enum NetworkConnectionSubtype {
  @doc("GPRS (2G)")
  gprs: "gprs",

  @doc("EDGE (2G)")
  edge: "edge",

  @doc("UMTS (3G)")
  umts: "umts",

  @doc("CDMA (2G/3G)")
  cdma: "cdma",

  @doc("EVDO revision 0")
  evdo0: "evdo_0",

  @doc("EVDO revision A")
  evdoA: "evdo_a",

  @doc("EVDO revision B")
  evdoB: "evdo_b",

  @doc("1xRTT")
  oneXrtt: "1xrtt",

  @doc("HSDPA (3.5G)")
  hsdpa: "hsdpa",

  @doc("HSUPA (3.5G)")
  hsupa: "hsupa",

  @doc("HSPA (3.5G)")
  hspa: "hspa",

  @doc("HSPA+ (3.5G)")
  hspap: "hspap",

  @doc("iDEN")
  iden: "iden",

  @doc("LTE (4G)")
  lte: "lte",

  @doc("LTE-CA (4G+)")
  lteCa: "lte_ca",

  @doc("NR (5G)")
  nr: "nr",

  @doc("NR NSA (5G)")
  nrNsa: "nr_nsa",

  @doc("NR SA (5G)")
  nrSa: "nr_sa",
}

// =============================================================================
// Connection State
// =============================================================================

@doc("Network connection state")
enum NetworkConnectionState {
  @doc("Connection is being established")
  connecting: "connecting",

  @doc("Connection is open and active")
  connected: "connected",

  @doc("Connection is being closed")
  closing: "closing",

  @doc("Connection is closed")
  closed: "closed",

  @doc("Connection failed")
  failed: "failed",
}

@doc("Network connection record")
model NetworkConnection {
  @doc("Local address")
  @encodedName("application/json", "local_address")
  localAddress: IpAddress;

  @doc("Local port")
  @encodedName("application/json", "local_port")
  localPort: Port;

  @doc("Remote address")
  @encodedName("application/json", "remote_address")
  remoteAddress: IpAddress;

  @doc("Remote port")
  @encodedName("application/json", "remote_port")
  remotePort: Port;

  @doc("Transport protocol")
  transport: NetworkTransport;

  @doc("Connection state")
  state: NetworkConnectionState;

  @doc("Connection start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("Bytes sent")
  @encodedName("application/json", "bytes_sent")
  bytesSent: ByteSize;

  @doc("Bytes received")
  @encodedName("application/json", "bytes_received")
  bytesReceived: ByteSize;
}
