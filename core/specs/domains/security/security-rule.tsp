// =============================================================================
// QYL v2.0 - Security Rule Semantic Conventions (OTel v1.39)
// =============================================================================
// Security event and rule telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Security.SecurityRule;

// =============================================================================
// Security Rule Attributes
// =============================================================================

@doc("Security rule span attributes")
model SecurityRuleAttributes {
  @doc("Rule category/type")
  @encodedName("application/json", "security.rule.category")
  ruleCategory?: SecurityRuleCategory;

  @doc("Rule description")
  @encodedName("application/json", "security.rule.description")
  ruleDescription?: string;

  @doc("Rule unique identifier")
  @encodedName("application/json", "security.rule.id")
  ruleId?: string;

  @doc("Rule name")
  @encodedName("application/json", "security.rule.name")
  ruleName?: string;

  @doc("Rule reference URL")
  @encodedName("application/json", "security.rule.reference")
  ruleReference?: UrlString;

  @doc("Ruleset/category the rule belongs to")
  @encodedName("application/json", "security.rule.ruleset")
  ruleRuleset?: string;

  @doc("Threat attack technique (MITRE ATT&CK)")
  @encodedName("application/json", "threat.technique.id")
  threatTechniqueId?: string;

  @doc("Threat attack technique name")
  @encodedName("application/json", "threat.technique.name")
  threatTechniqueName?: string;

  @doc("Threat tactic (MITRE ATT&CK)")
  @encodedName("application/json", "threat.tactic.id")
  threatTacticId?: string;

  @doc("Threat tactic name")
  @encodedName("application/json", "threat.tactic.name")
  threatTacticName?: string;
}

// =============================================================================
// Security Event Model
// =============================================================================

@doc("Security event/alert")
model SecurityEvent {
  @doc("Event unique identifier")
  @encodedName("application/json", "event_id")
  eventId: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: SecurityEventType;

  @doc("Event category")
  @encodedName("application/json", "event_category")
  eventCategory: SecurityEventCategory;

  @doc("Event action taken")
  @encodedName("application/json", "event_action")
  eventAction?: SecurityEventAction;

  @doc("Event outcome")
  @encodedName("application/json", "event_outcome")
  eventOutcome: SecurityEventOutcome;

  @doc("Event severity")
  severity: SecuritySeverity;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Rule that triggered this event")
  rule?: SecurityRuleAttributes;

  @doc("Source of the event")
  source?: SecuritySource;

  @doc("Target of the event")
  target?: SecurityTarget;

  @doc("Additional event data")
  data?: Attribute[];
}

// =============================================================================
// Security Enumerations
// =============================================================================

@doc("Security rule categories")
enum SecurityRuleCategory {
  @doc("Authentication rule")
  authentication: "authentication",

  @doc("Authorization rule")
  authorization: "authorization",

  @doc("Input validation rule")
  inputValidation: "input_validation",

  @doc("Rate limiting rule")
  rateLimit: "rate_limit",

  @doc("Firewall rule")
  firewall: "firewall",

  @doc("Intrusion detection rule")
  ids: "ids",

  @doc("Intrusion prevention rule")
  ips: "ips",

  @doc("Web application firewall rule")
  waf: "waf",

  @doc("Data loss prevention rule")
  dlp: "dlp",

  @doc("Anti-malware rule")
  antimalware: "antimalware",

  @doc("Encryption policy rule")
  encryption: "encryption",

  @doc("Access control rule")
  accessControl: "access_control",

  @doc("Compliance rule")
  compliance: "compliance",

  @doc("Custom rule")
  custom: "custom",
}

@doc("Security event types")
enum SecurityEventType {
  @doc("Alert generated")
  alert: "alert",

  @doc("Rule matched")
  ruleMatch: "rule_match",

  @doc("Policy violation")
  policyViolation: "policy_violation",

  @doc("Anomaly detected")
  anomaly: "anomaly",

  @doc("Threat detected")
  threat: "threat",

  @doc("Audit event")
  audit: "audit",

  @doc("Configuration change")
  configChange: "config_change",

  @doc("Access event")
  access: "access",
}

@doc("Security event categories")
enum SecurityEventCategory {
  @doc("Authentication event")
  authentication: "authentication",

  @doc("Authorization event")
  authorization: "authorization",

  @doc("Configuration event")
  configuration: "configuration",

  @doc("Database event")
  database: "database",

  @doc("File event")
  file: "file",

  @doc("IAM event")
  iam: "iam",

  @doc("Intrusion detection")
  intrusionDetection: "intrusion_detection",

  @doc("Malware event")
  malware: "malware",

  @doc("Network event")
  network: "network",

  @doc("Process event")
  process: "process",

  @doc("Registry event")
  registry: "registry",

  @doc("Session event")
  session: "session",

  @doc("Threat intel event")
  threatIntel: "threat_intel",

  @doc("Vulnerability event")
  vulnerability: "vulnerability",

  @doc("Web event")
  web: "web",
}

@doc("Security event actions")
enum SecurityEventAction {
  @doc("Access granted")
  accessGranted: "access_granted",

  @doc("Access denied")
  accessDenied: "access_denied",

  @doc("Login success")
  loginSuccess: "login_success",

  @doc("Login failure")
  loginFailure: "login_failure",

  @doc("Logout")
  logout: "logout",

  @doc("Password change")
  passwordChange: "password_change",

  @doc("Connection blocked")
  blocked: "blocked",

  @doc("Connection allowed")
  allowed: "allowed",

  @doc("Quarantine")
  quarantine: "quarantine",

  @doc("Alert generated")
  alertGenerated: "alert_generated",

  @doc("Rule triggered")
  ruleTriggered: "rule_triggered",
}

@doc("Security event outcomes")
enum SecurityEventOutcome {
  @doc("Success")
  success: "success",

  @doc("Failure")
  failure: "failure",

  @doc("Unknown")
  `unknown`: "unknown",
}

@doc("Security severity levels")
enum SecuritySeverity {
  @doc("Informational")
  informational: "informational",

  @doc("Low severity")
  low: "low",

  @doc("Medium severity")
  medium: "medium",

  @doc("High severity")
  high: "high",

  @doc("Critical severity")
  critical: "critical",
}

@doc("Security source information")
model SecuritySource {
  @doc("Source IP address")
  ip?: IpAddress;

  @doc("Source port")
  port?: Port;

  @doc("Source user")
  user?: string;

  @doc("Source domain")
  domain?: string;

  @doc("Source MAC address")
  mac?: MacAddress;

  @doc("Source geo location")
  geo?: GeoLocation;
}

@doc("Security target information")
model SecurityTarget {
  @doc("Target IP address")
  ip?: IpAddress;

  @doc("Target port")
  port?: Port;

  @doc("Target resource")
  resource?: string;

  @doc("Target user")
  user?: string;

  @doc("Target domain")
  domain?: string;
}

@doc("Geographic location")
model GeoLocation {
  @doc("Country code (ISO 3166-1 alpha-2)")
  @encodedName("application/json", "country_code")
  @pattern("^[A-Z]{2}$")
  countryCode?: string;

  @doc("Country name")
  @encodedName("application/json", "country_name")
  countryName?: string;

  @doc("Region/state code")
  @encodedName("application/json", "region_code")
  regionCode?: string;

  @doc("Region/state name")
  @encodedName("application/json", "region_name")
  regionName?: string;

  @doc("City name")
  city?: string;

  @doc("Postal code")
  @encodedName("application/json", "postal_code")
  postalCode?: string;

  @doc("Latitude")
  @minValue(-90.0)
  @maxValue(90.0)
  latitude?: float64;

  @doc("Longitude")
  @minValue(-180.0)
  @maxValue(180.0)
  longitude?: float64;

  @doc("Timezone")
  timezone?: string;
}
