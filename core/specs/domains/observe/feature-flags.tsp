// =============================================================================
// QYL v2.0 - Feature Flags Semantic Conventions (OTel v1.38)
// =============================================================================
// Feature flag evaluation telemetry attributes.
// Aligned with official OTel registry.yaml
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Observe.FeatureFlags;

// =============================================================================
// Feature Flag Value Union (for type: any)
// =============================================================================

@doc("Feature flag evaluated value - discriminated union for polymorphic values")
@discriminated(#{ envelope: "none", discriminatorPropertyName: "type" })
union FeatureFlagValue {
  @doc("String value")
  string: FeatureFlagStringValue,

  @doc("Boolean value")
  boolean: FeatureFlagBooleanValue,

  @doc("Number value")
  number: FeatureFlagNumberValue,

  @doc("JSON object value")
  object: FeatureFlagObjectValue,
}

@doc("String flag value")
model FeatureFlagStringValue {
  @doc("Value type discriminator")
  type: "string";

  @doc("The string value")
  value: string;
}

@doc("Boolean flag value")
model FeatureFlagBooleanValue {
  @doc("Value type discriminator")
  type: "boolean";

  @doc("The boolean value")
  value: boolean;
}

@doc("Number flag value")
model FeatureFlagNumberValue {
  @doc("Value type discriminator")
  type: "number";

  @doc("The numeric value")
  value: float64;
}

@doc("Object/JSON flag value")
model FeatureFlagObjectValue {
  @doc("Value type discriminator")
  type: "object";

  @doc("The object value as key-value pairs")
  value: Record<string>;
}

// =============================================================================
// Feature Flag Attributes (OTel v1.38 registry.yaml)
// =============================================================================

@doc("Feature flag span/event attributes per OTel v1.38")
model FeatureFlagAttributes {
  @doc("The lookup key of the feature flag")
  @encodedName("application/json", "feature_flag.key")
  key: string;

  @doc("Identifies the feature flag provider")
  @encodedName("application/json", "feature_flag.provider.name")
  providerName?: string;

  @doc("A semantic identifier for an evaluated flag value (e.g., 'red' for '#c05543')")
  @encodedName("application/json", "feature_flag.result.variant")
  resultVariant?: string;

  @doc("The unique identifier for the flag evaluation context (e.g., targeting key)")
  @encodedName("application/json", "feature_flag.context.id")
  contextId?: string;

  @doc("The version of the ruleset used during evaluation")
  @encodedName("application/json", "feature_flag.version")
  version?: string;

  @doc("The identifier of the flag set to which the feature flag belongs")
  @encodedName("application/json", "feature_flag.set.id")
  setId?: string;

  @doc("The reason code which shows how a feature flag value was determined")
  @encodedName("application/json", "feature_flag.result.reason")
  resultReason?: FeatureFlagResultReason;

  @doc("The evaluated value of the feature flag (may be string, boolean, or number)")
  @encodedName("application/json", "feature_flag.result.value")
  resultValue?: FeatureFlagValue;
}

// =============================================================================
// Feature Flag Result Reason (OTel v1.38 exact values)
// =============================================================================

@doc("Feature flag result reason codes per OTel v1.38")
enum FeatureFlagResultReason {
  @doc("The resolved value is static (no dynamic evaluation)")
  static: "static",

  @doc("Fell back to a pre-configured value (no dynamic evaluation occurred)")
  default: "default",

  @doc("The resolved value was the result of dynamic evaluation/targeting")
  targetingMatch: "targeting_match",

  @doc("The resolved value was the result of pseudorandom assignment")
  split: "split",

  @doc("The resolved value was retrieved from cache")
  cached: "cached",

  @doc("The resolved value was the result of the flag being disabled")
  disabled: "disabled",

  @doc("The reason for the resolved value could not be determined")
  `unknown`: "unknown",

  @doc("The resolved value is non-authoritative or possibly out of date")
  stale: "stale",

  @doc("The resolved value was the result of an error")
  error: "error",
}

// =============================================================================
// Feature Flag Error Codes (OpenFeature spec)
// =============================================================================

@doc("Feature flag error codes")
enum FeatureFlagErrorCode {
  @doc("Provider not ready")
  providerNotReady: "PROVIDER_NOT_READY",

  @doc("Flag not found")
  flagNotFound: "FLAG_NOT_FOUND",

  @doc("Parse error")
  parseError: "PARSE_ERROR",

  @doc("Type mismatch")
  typeMismatch: "TYPE_MISMATCH",

  @doc("Targeting key missing")
  targetingKeyMissing: "TARGETING_KEY_MISSING",

  @doc("Invalid context")
  invalidContext: "INVALID_CONTEXT",

  @doc("Provider fatal error")
  providerFatal: "PROVIDER_FATAL",

  @doc("General error")
  general: "GENERAL",
}

// =============================================================================
// Feature Flag Providers
// =============================================================================

@doc("Feature flag providers")
enum FeatureFlagProvider {
  @doc("LaunchDarkly")
  launchdarkly: "launchdarkly",

  @doc("Split.io")
  split: "split",

  @doc("Flagsmith")
  flagsmith: "flagsmith",

  @doc("Unleash")
  unleash: "unleash",

  @doc("ConfigCat")
  configcat: "configcat",

  @doc("OpenFeature")
  openfeature: "openfeature",

  @doc("AWS AppConfig")
  awsAppconfig: "aws_appconfig",

  @doc("Azure App Configuration")
  azureAppconfig: "azure_appconfig",

  @doc("GCP Feature Flags (Firebase)")
  gcpFirebase: "gcp_firebase",

  @doc("Flipt")
  flipt: "flipt",

  @doc("PostHog")
  posthog: "posthog",

  @doc("Statsig")
  statsig: "statsig",

  @doc("Harness")
  harness: "harness",

  @doc("DevCycle")
  devcycle: "devcycle",

  @doc("Custom/internal")
  custom: "custom",
}

// =============================================================================
// Feature Flag Events
// =============================================================================

@doc("Feature flag evaluation event")
model FeatureFlagEvaluationEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "feature_flag.evaluation";

  @doc("Flag key")
  @encodedName("application/json", "feature_flag.key")
  flagKey: string;

  @doc("Variant returned")
  @encodedName("application/json", "feature_flag.result.variant")
  resultVariant: string;

  @doc("Provider name")
  @encodedName("application/json", "feature_flag.provider.name")
  providerName: FeatureFlagProvider;

  @doc("Result reason")
  @encodedName("application/json", "feature_flag.result.reason")
  resultReason: FeatureFlagResultReason;

  @doc("Context ID")
  @encodedName("application/json", "feature_flag.context.id")
  contextId?: string;

  @doc("Result value")
  @encodedName("application/json", "feature_flag.result.value")
  resultValue?: FeatureFlagValue;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Evaluation duration in nanoseconds")
  @encodedName("application/json", "duration_ns")
  durationNs?: DurationNs;
}

@doc("Feature flag change event")
model FeatureFlagChangeEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "feature_flag.configuration_changed";

  @doc("Flag key")
  @encodedName("application/json", "feature_flag.key")
  flagKey: string;

  @doc("Previous variant")
  @encodedName("application/json", "feature_flag.previous_variant")
  previousVariant?: string;

  @doc("New variant")
  @encodedName("application/json", "feature_flag.new_variant")
  newVariant: string;

  @doc("Change source")
  @encodedName("application/json", "feature_flag.change_source")
  changeSource?: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Feature Flag Metrics
// =============================================================================

@doc("Feature flag evaluation count metric")
model FeatureFlagEvaluationsMetric {
  @doc("Metric name")
  name: "feature_flag.evaluations";

  @doc("Metric unit")
  unit: "{evaluation}";

  @doc("Flag key")
  @encodedName("application/json", "feature_flag.key")
  flagKey: string;

  @doc("Provider name")
  @encodedName("application/json", "feature_flag.provider.name")
  providerName: FeatureFlagProvider;

  @doc("Variant returned")
  @encodedName("application/json", "feature_flag.result.variant")
  resultVariant: string;

  @doc("Result reason")
  @encodedName("application/json", "feature_flag.result.reason")
  resultReason: FeatureFlagResultReason;
}

@doc("Feature flag evaluation duration metric")
model FeatureFlagEvaluationDurationMetric {
  @doc("Metric name")
  name: "feature_flag.evaluation.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Flag key")
  @encodedName("application/json", "feature_flag.key")
  flagKey: string;

  @doc("Provider name")
  @encodedName("application/json", "feature_flag.provider.name")
  providerName: FeatureFlagProvider;
}
