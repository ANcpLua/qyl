// =============================================================================
// QYL v2.0 - User/Client/Service Semantic Conventions (OTel v1.39)
// =============================================================================
// User, client, service, and app identity attributes.
// =============================================================================

import "../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Identity;

// =============================================================================
// User Attributes
// =============================================================================

@doc("User attributes")
model UserAttributes {
  @doc("User ID (unique identifier)")
  @encodedName("application/json", "user.id")
  id?: UserId;

  @doc("User name (may be pseudonymized)")
  @encodedName("application/json", "user.name")
  name?: string;

  @doc("User email (may be hashed)")
  @encodedName("application/json", "user.email")
  email?: string;

  @doc("User full name")
  @encodedName("application/json", "user.full_name")
  fullName?: string;

  @doc("User hash (for privacy)")
  @encodedName("application/json", "user.hash")
  hash?: string;

  @doc("User roles")
  @encodedName("application/json", "user.roles")
  roles?: string[];
}

// =============================================================================
// Client Attributes
// =============================================================================

@doc("Client attributes")
model ClientAttributes {
  @doc("Client IP address")
  @encodedName("application/json", "client.address")
  address?: IpAddress;

  @doc("Client port")
  @encodedName("application/json", "client.port")
  port?: Port;
}

// =============================================================================
// Service Attributes
// =============================================================================

@doc("Service resource attributes")
model ServiceAttributes {
  @doc("Service name (required)")
  @encodedName("application/json", "service.name")
  name: string;

  @doc("Service namespace")
  @encodedName("application/json", "service.namespace")
  `namespace`?: string;

  @doc("Service instance ID (unique per instance)")
  @encodedName("application/json", "service.instance.id")
  instanceId?: string;

  @doc("Service version")
  @encodedName("application/json", "service.version")
  version?: SemVer;
}

// =============================================================================
// App Attributes
// =============================================================================

@doc("Application attributes")
model AppAttributes {
  @doc("Application name")
  @encodedName("application/json", "app.name")
  name?: string;

  @doc("Application version")
  @encodedName("application/json", "app.version")
  version?: SemVer;

  @doc("Application build ID")
  @encodedName("application/json", "app.build.id")
  buildId?: string;
}

// =============================================================================
// Server Attributes
// =============================================================================

@doc("Server attributes")
model ServerAttributes {
  @doc("Server address (hostname or IP)")
  @encodedName("application/json", "server.address")
  address?: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  port?: Port;
}

// =============================================================================
// Destination Attributes
// =============================================================================

@doc("Destination attributes (for client spans)")
model DestinationAttributes {
  @doc("Destination address")
  @encodedName("application/json", "destination.address")
  address?: string;

  @doc("Destination port")
  @encodedName("application/json", "destination.port")
  port?: Port;
}

// =============================================================================
// Source Attributes
// =============================================================================

@doc("Source attributes")
model SourceAttributes {
  @doc("Source address")
  @encodedName("application/json", "source.address")
  address?: IpAddress;

  @doc("Source port")
  @encodedName("application/json", "source.port")
  port?: Port;
}

// =============================================================================
// Peer Attributes (Deprecated, for compatibility)
// =============================================================================

@doc("Peer attributes (deprecated)")
model PeerAttributes {
  @doc("Peer service name")
  @encodedName("application/json", "peer.service")
  service?: string;
}

// =============================================================================
// Identity Statistics
// =============================================================================

@doc("User activity statistics")
model UserActivityStats {
  @doc("Total unique users")
  @encodedName("application/json", "total_users")
  totalUsers: Count;

  @doc("Active users in time range")
  @encodedName("application/json", "active_users")
  activeUsers: Count;

  @doc("New users in time range")
  @encodedName("application/json", "new_users")
  newUsers: Count;

  @doc("Users by role")
  @encodedName("application/json", "by_role")
  byRole?: UserRoleStats[];
}

@doc("User stats by role")
model UserRoleStats {
  @doc("Role name")
  role: string;

  @doc("User count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Service dependency map")
model ServiceDependency {
  @doc("Source service")
  @encodedName("application/json", "source_service")
  sourceService: string;

  @doc("Target service")
  @encodedName("application/json", "target_service")
  targetService: string;

  @doc("Request count")
  @encodedName("application/json", "request_count")
  requestCount: Count;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average latency in milliseconds")
  @encodedName("application/json", "avg_latency_ms")
  avgLatencyMs: float64;
}
