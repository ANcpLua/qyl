// =============================================================================
// QYL v2.0 - URL Semantic Conventions (OTel v1.39)
// =============================================================================
// URL component telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Transport.Url;

// =============================================================================
// URL Attributes
// =============================================================================

@doc("URL component attributes")
model UrlAttributes {
  @doc("Full URL (may be redacted)")
  @encodedName("application/json", "url.full")
  full?: UrlString;

  @doc("URL scheme (http, https, ftp, etc.)")
  @encodedName("application/json", "url.scheme")
  scheme?: string;

  @doc("URL path component")
  @encodedName("application/json", "url.path")
  path?: UrlPath;

  @doc("URL query string (without leading ?)")
  @encodedName("application/json", "url.query")
  query?: UrlQuery;

  @doc("URL fragment (without leading #)")
  @encodedName("application/json", "url.fragment")
  fragment?: string;

  @doc("Domain/host of the URL")
  @encodedName("application/json", "url.domain")
  domain?: string;

  @doc("Port number from URL")
  @encodedName("application/json", "url.port")
  port?: Port;

  @doc("Username from URL (if present)")
  @encodedName("application/json", "url.username")
  username?: string;

  @doc("Registered domain (eTLD+1)")
  @encodedName("application/json", "url.registered_domain")
  registeredDomain?: string;

  @doc("Top-level domain")
  @encodedName("application/json", "url.top_level_domain")
  topLevelDomain?: string;

  @doc("Subdomain (excluding registered domain)")
  @encodedName("application/json", "url.subdomain")
  subdomain?: string;

  @doc("Original URL before any modification")
  @encodedName("application/json", "url.original")
  original?: UrlString;

  @doc("URL template with placeholders")
  @encodedName("application/json", "url.template")
  template?: string;
}

// =============================================================================
// URL Parsing Model
// =============================================================================

@doc("Parsed URL components")
model ParsedUrl {
  @doc("Original URL string")
  original: UrlString;

  @doc("URL scheme")
  scheme: string;

  @doc("URL host (domain or IP)")
  host: string;

  @doc("URL port (explicit or default)")
  port: Port;

  @doc("URL path")
  path: UrlPath;

  @doc("Query parameters as key-value pairs")
  @encodedName("application/json", "query_params")
  queryParams?: UrlQueryParam[];

  @doc("URL fragment")
  fragment?: string;

  @doc("Userinfo (username:password)")
  userinfo?: string;

  @doc("Whether URL is absolute")
  @encodedName("application/json", "is_absolute")
  isAbsolute: boolean;

  @doc("Whether URL is secure (https, wss, etc.)")
  @encodedName("application/json", "is_secure")
  isSecure: boolean;
}

@doc("URL query parameter")
model UrlQueryParam {
  @doc("Parameter name")
  name: string;

  @doc("Parameter value (may be empty string)")
  value: string;
}

// =============================================================================
// Common URL Patterns
// =============================================================================

@doc("URL category for classification")
enum UrlCategory {
  @doc("API endpoint")
  api: "api",

  @doc("Static asset (JS, CSS, images)")
  static: "static",

  @doc("Web page")
  page: "page",

  @doc("WebSocket endpoint")
  websocket: "websocket",

  @doc("Health check endpoint")
  health: "health",

  @doc("Metrics endpoint")
  metrics: "metrics",

  @doc("Authentication endpoint")
  auth: "auth",

  @doc("Webhook endpoint")
  webhook: "webhook",

  @doc("GraphQL endpoint")
  graphql: "graphql",

  @doc("RSS/Atom feed")
  feed: "feed",

  @doc("File download")
  download: "download",

  @doc("Upload endpoint")
  upload: "upload",

  @doc("Other/unknown")
  other: "other",
}

@doc("Sensitive URL parameter types")
enum SensitiveParamType {
  @doc("API key")
  apiKey: "api_key",

  @doc("Access token")
  accessToken: "access_token",

  @doc("Password")
  password: "password",

  @doc("Secret")
  secret: "secret",

  @doc("Session ID")
  sessionId: "session_id",

  @doc("Credit card")
  creditCard: "credit_card",

  @doc("Social security number")
  ssn: "ssn",

  @doc("Personally identifiable information")
  pii: "pii",
}
