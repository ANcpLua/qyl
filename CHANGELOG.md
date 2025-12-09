# Changelog

Please update changelog as part of any significant pull request. Place short
description of your change into "Unreleased" section. As part of release process
content of "Unreleased" section content will generate release notes for the
release.

## Unreleased

### Context

### Traces

- Deprecate Zipkin exporter document and make exporter implementation optional.
  ([#4715](https://github.com/open-telemetry/opentelemetry-specification/pull/4715/))
- Add spec for `AlwaysRecord` sampler
  ([#4699](https://github.com/open-telemetry/opentelemetry-specification/pull/4699))

### Metrics

- Stabilize `Enabled` API for synchronous instruments.
  ([#4746](https://github.com/open-telemetry/opentelemetry-specification/pull/4746))
- Allow instrument `Enabled` implementation to have additional optimizations and features.
  ([#4747](https://github.com/open-telemetry/opentelemetry-specification/pull/4747))

### Logs

- Stabilize `LogRecordProcessor.Enabled`.
  ([#4717](https://github.com/open-telemetry/opentelemetry-specification/pull/4717))

### Baggage

### Profiles

### Resource

### Entities

### OpenTelemetry Protocol

### Compatibility

### SDK Configuration

### Common

### Supplementary Guidelines

### OTEPs

## v1.51.0 (2025-11-17)

### Metrics

- `AlignedHistogramBucketExemplarReservoir` SHOULD use a time-weighted algorithm.
  ([#4678](https://github.com/open-telemetry/opentelemetry-specification/pull/4678))

### Profiles

- Document the profiles signal.
  ([#4685](https://github.com/open-telemetry/opentelemetry-specification/pull/4685))

### Common

- Extend the set of attribute value types to support more complex data structures.
  ([#4651](https://github.com/open-telemetry/opentelemetry-specification/pull/4651))

## v1.50.0 (2025-10-17)

### Traces

- Restore `TraceIdRatioBased` and give it a deprecation timeline. Update recommended
  warnings based on feedback in issue [#4601](https://github.com/open-telemetry/opentelemetry-specification/issues/4601).
  ([#4627](https://github.com/open-telemetry/opentelemetry-specification/pull/4627))
- Changes of `TracerConfig.disabled` MUST be eventually visible.
  ([#4645](https://github.com/open-telemetry/opentelemetry-specification/pull/4645))
- Remove text related to the former expermental probability sampling specification.
  ([#4673](https://github.com/open-telemetry/opentelemetry-specification/pull/4673))

### Metrics

- Changes of `MeterConfig.disabled` MUST be eventually visible.
  ([#4645](https://github.com/open-telemetry/opentelemetry-specification/pull/4645))

### Logs

- Add minimum_severity and trace_based logger configuration parameters.
  ([#4612](https://github.com/open-telemetry/opentelemetry-specification/pull/4612))
- Changes of `LoggerConfig.disabled` MUST be eventually visible.
  ([#4645](https://github.com/open-telemetry/opentelemetry-specification/pull/4645))

## v1.49.0 (2025-09-16)

### Entities

- Specify entity information via an environment variable.
  ([#4594](https://github.com/open-telemetry/opentelemetry-specification/pull/4594))

### Common

- OTLP Exporters may allow devs to prepend a product identifier in `User-Agent` header.
  ([#4560](https://github.com/open-telemetry/opentelemetry-specification/pull/4560))
- ⚠️ **IMPORTANT**: Extending the set of standard attribute value types is no longer a breaking change.
  ([#4614](https://github.com/open-telemetry/opentelemetry-specification/pull/4614))

### OTEPs

- Clarify in Composite Samplers OTEP the unreliable threshold case.
  ([#4569](https://github.com/open-telemetry/opentelemetry-specification/pull/4569))

## v1.48.0 (2025-08-13)

### Logs

- Improve concurrency safety description of `LogRecordProcessor.OnEmit`.
  ([#4578](https://github.com/open-telemetry/opentelemetry-specification/pull/4578))
- Clarify that `SeverityNumber` values are used when comparing severities.
  ([#4552](https://github.com/open-telemetry/opentelemetry-specification/pull/4552))

### Entities

- Mention entity references in the stability guarantees.
  ([#4593](https://github.com/open-telemetry/opentelemetry-specification/pull/4593))

### OpenTelemetry Protocol

- Clarify protocol defaults on specification.
  ([#4585](https://github.com/open-telemetry/opentelemetry-specification/pull/4585))

### Compatibility

- Flexibilie escaping of characters that are discouraged by Prometheus Conventions
  in Prometheus exporters.
  ([#4533](https://github.com/open-telemetry/opentelemetry-specification/pull/4533))
- Flexibilize addition of unit/type related suffixes in Prometheus exporters.
  ([#4533](https://github.com/open-telemetry/opentelemetry-specification/pull/4533))
- Define the configuration option "Translation Strategies" for Prometheus exporters.
  ([#4533](https://github.com/open-telemetry/opentelemetry-specification/pull/4533))
- Define conversion of Prometheus native histograms to OpenTelemetry exponential histograms.
  ([#4561](https://github.com/open-telemetry/opentelemetry-specification/pull/4561))
- Clarify what to do when scope attribute conflicts with name, version and schema URL.
  ([#4599](https://github.com/open-telemetry/opentelemetry-specification/pull/4599))

### SDK Configuration

- Enum values provided via environment variables SHOULD be interpreted in a case-insensitive manner.
  ([#4576](https://github.com/open-telemetry/opentelemetry-specification/pull/4576))

## v1.47.0 (2025-07-18)

### Traces

- Define sampling threshold field in OpenTelemetry TraceState; define the behavior
  of TraceIdRatioBased sampler in terms of W3C Trace Context Level 2 randomness.
  ([#4166](https://github.com/open-telemetry/opentelemetry-specification/pull/4166))
- Define CompositeSampler implementation and built-in ComposableSampler interfaces.
  ([#4466](https://github.com/open-telemetry/opentelemetry-specification/pull/4466))
- Define how SDK implements `Tracer.Enabled`.
  ([#4537](https://github.com/open-telemetry/opentelemetry-specification/pull/4537))

### Logs

- Stabilize `Event Name` parameter of `Logger.Enabled`.
  ([#4534](https://github.com/open-telemetry/opentelemetry-specification/pull/4534))
- Stabilize SDK and No-Op `Logger.Enabled`.
  ([#4536](https://github.com/open-telemetry/opentelemetry-specification/pull/4536))
- `SeverityNumber=0` MAY be used to represent an unspecified value.
  ([#4535](https://github.com/open-telemetry/opentelemetry-specification/pull/4535))

### Compatibility

- Clarify expectations about Prometheus content negotiation for metric names.
  ([#4543](https://github.com/open-telemetry/opentelemetry-specification/pull/4543))

### Supplementary Guidelines

- Add Supplementary Guidelines for environment variables as context carrier
  specification.
  ([#4548](https://github.com/open-telemetry/opentelemetry-specification/pull/4548))

### OTEPs

- Extend attributes to support complex values.
  ([#4485](https://github.com/open-telemetry/opentelemetry-specification/pull/4485))

### Common

- Update spec to comply with OTEP-232.
  ([#4529](https://github.com/open-telemetry/opentelemetry-specification/pull/4529))

## v1.46.0 (2025-06-12)

### Metrics

- Prometheus receiver can expect `otel_scope_schema_url` and `otel_scope_[attribute]` labels on all metrics.
  ([#4505](https://github.com/open-telemetry/opentelemetry-specification/pull/4505))
- Prometheus receiver no longer expects `otel_scope_info` metric.
  ([#4505](https://github.com/open-telemetry/opentelemetry-specification/pull/4505))
- Prometheus exporter adds `otel_scope_schema_url` and `otel_scope_[attribute]` labels on all metrics.
  ([#4505](https://github.com/open-telemetry/opentelemetry-specification/pull/4505))
- Prometheus exporter no longer exports `otel_scope_info` metric.
  ([#4505](https://github.com/open-telemetry/opentelemetry-specification/pull/4505))

### Entities

- Define rules for setting identifying attributes.
  ([#4498](https://github.com/open-telemetry/opentelemetry-specification/pull/4498))
- Define rules for entity-resource referencing model.
  ([#4499](https://github.com/open-telemetry/opentelemetry-specification/pull/4499))

### Common

- Move Instrumentation Scope definition from glossary to a dedicated document and use normative language.
  ([#4488](https://github.com/open-telemetry/opentelemetry-specification/pull/4488))

## v1.45.0 (2025-05-14)

### Context

- Drop reference to binary `Propagator`.
  ([#4490](https://github.com/open-telemetry/opentelemetry-specification/pull/4490))

### Logs

- Add optional `Event Name` parameter to `Logger.Enabled` and `LogRecordProcessor.Enabled`.
  ([#4489](https://github.com/open-telemetry/opentelemetry-specification/pull/4489))

### Resource

- Add experimental resource detector name.
  ([#4461](https://github.com/open-telemetry/opentelemetry-specification/pull/4461))

### OTEPs

- OTEP: Span Event API deprecation plan.
  ([#4430](https://github.com/open-telemetry/opentelemetry-specification/pull/4430))

## v1.44.0 (2025-04-15)

### Context

- Add context propagation through Environment Variables specification.
    ([#4454](https://github.com/open-telemetry/opentelemetry-specification/pull/4454))
- On Propagators API, stabilize `GetAll` on the `TextMap` Extract.
    ([#4472](https://github.com/open-telemetry/opentelemetry-specification/pull/4472))

### Traces

- Define sampling threshold field in OpenTelemetry TraceState; define the behavior
  of TraceIdRatioBased sampler in terms of W3C Trace Context Level 2 randomness.
  ([#4166](https://github.com/open-telemetry/opentelemetry-specification/pull/4166))

### Metrics

- Clarify SDK behavior for Instrument Advisory Parameter.
  ([#4389](https://github.com/open-telemetry/opentelemetry-specification/pull/4389))

### Logs

- Add `Enabled` opt-in operation to the `LogRecordProcessor`.
  ([#4439](https://github.com/open-telemetry/opentelemetry-specification/pull/4439))
- Stabilize `Logger.Enabled`.
  ([#4463](https://github.com/open-telemetry/opentelemetry-specification/pull/4463))
- Stabilize `EventName`.
  ([#4475](https://github.com/open-telemetry/opentelemetry-specification/pull/4475))
- Move implementation details of the `Observed Timestamp` to the Log SDK.
  ([#4482](https://github.com/open-telemetry/opentelemetry-specification/pull/4482))

### Baggage

- Add context (baggage) propagation through Environment Variables specification.
    ([#4454](https://github.com/open-telemetry/opentelemetry-specification/pull/4454))

### Resource

- Add Datamodel for Entities.
   ([#4442](https://github.com/open-telemetry/opentelemetry-specification/pull/4442))

### SDK Configuration

- Convert declarative config env var substitution syntax to ABNF.
  ([#4448](https://github.com/open-telemetry/opentelemetry-specification/pull/4448))
- List declarative config supported SDK extension plugin interfaces.
  ([#4452](https://github.com/open-telemetry/opentelemetry-specification/pull/4452))

## v1.43.0 (2025-03-18)

### Traces

- Clarify STDOUT exporter format is unspecified.
   ([#4418](https://github.com/open-telemetry/opentelemetry-specification/pull/4418))

### Metrics

- Clarify the metrics design goal, scope out StatsD client support.
   ([#4445](https://github.com/open-telemetry/opentelemetry-specification/pull/4445))
- Clarify STDOUT exporter format is unspecified.
   ([#4418](https://github.com/open-telemetry/opentelemetry-specification/pull/4418))

### Logs

- Clarify that it is allowed to directly use Logs API.
   ([#4438](https://github.com/open-telemetry/opentelemetry-specification/pull/4438))
- Clarify STDOUT exporter format is unspecified.
   ([#4418](https://github.com/open-telemetry/opentelemetry-specification/pull/4418))

### Supplementary Guidelines

- Add Advanced Processing to Logs Supplementary Guidelines.
  ([#4407](https://github.com/open-telemetry/opentelemetry-specification/pull/4407))

### OTEPs

- Composite Head Samplers.
  ([#4321](https://github.com/open-telemetry/opentelemetry-specification/pull/4321))

## v1.42.0 (2025-02-18)

### Traces

- Deprecate `exception.escaped` attribute, add link to in-development semantic-conventions
  on how to record errors across signals.
  ([#4368](https://github.com/open-telemetry/opentelemetry-specification/pull/4368))
- Define randomness value requirements for W3C Trace Context Level 2.
  ([#4162](https://github.com/open-telemetry/opentelemetry-specification/pull/4162))

### Logs

- Define how SDK implements `Logger.Enabled`.
  ([#4381](https://github.com/open-telemetry/opentelemetry-specification/pull/4381))
- Logs API should have functionality for reusing Standard Attributes.
  ([#4373](https://github.com/open-telemetry/opentelemetry-specification/pull/4373))

### SDK Configuration

- Define syntax for escaping declarative configuration environment variable
  references.
  ([#4375](https://github.com/open-telemetry/opentelemetry-specification/pull/4375))
- Resolve various declarative config TODOs.
  ([#4394](https://github.com/open-telemetry/opentelemetry-specification/pull/4394))

## v1.41.0 (2025-01-21)

### Logs

- Remove the deprecated Events API and SDK in favor of having Events support in the Logs API and SDK.
  ([#4353](https://github.com/open-telemetry/opentelemetry-specification/pull/4353))
- Remove `Logger`'s Log Instrumentation operations.
  ([#4352](https://github.com/open-telemetry/opentelemetry-specification/pull/4352))
- Make all `Logger` operations user-facing.
  ([#4352](https://github.com/open-telemetry/opentelemetry-specification/pull/4352))

### SDK Configuration

- Clarify that implementations should interpret timeout environment variable
  values of zero as no limit (infinity).
  ([#4331](https://github.com/open-telemetry/opentelemetry-specification/pull/4331))

## v1.40.0 (2024-12-12)

### Context

- Adds optional `GetAll` method to `Getter` in Propagation API, allowing for the retrieval of multiple values for the same key.
  [#4295](https://github.com/open-telemetry/opentelemetry-specification/pull/4295)

### Traces

- Add in-development support for `otlp/stdout` exporter via `OTEL_TRACES_EXPORTER`.
  ([#4183](https://github.com/open-telemetry/opentelemetry-specification/pull/4183))
- Remove the recommendation to not synchronize access to `TracerConfig.disabled`.
  ([#4310](https://github.com/open-telemetry/opentelemetry-specification/pull/4310))

### Metrics

- Add in-development support for `otlp/stdout` exporter via `OTEL_METRICS_EXPORTER`.
  ([#4183](https://github.com/open-telemetry/opentelemetry-specification/pull/4183))
- Remove the recommendation to not synchronize access to `MeterConfig.disabled`.
  ([#4310](https://github.com/open-telemetry/opentelemetry-specification/pull/4310))

### Logs

- Add in-development support for `otlp/stdout` exporter via `OTEL_LOGS_EXPORTER`.
 ([#4183](https://github.com/open-telemetry/opentelemetry-specification/pull/4183))
- Remove the recommendation to not synchronize access to `LoggerConfig.disabled`.
  ([#4310](https://github.com/open-telemetry/opentelemetry-specification/pull/4310))
- Remove the in-development isolating log record processor.
  ([#4301](https://github.com/open-telemetry/opentelemetry-specification/pull/4301))

### Events

- Deprecate Events API and SDK in favor of having Events support in the Logs API and SDK.
  ([#4319](https://github.com/open-telemetry/opentelemetry-specification/pull/4319))
- Change `event.name` attribute into top-level event name field.
  ([#4320](https://github.com/open-telemetry/opentelemetry-specification/pull/4320))

### Common

- Lay out core principles for Specification changes.
  ([#4286](https://github.com/open-telemetry/opentelemetry-specification/pull/4286))

### Supplementary Guidelines

- Add core principles for evaluating specification changes.
  ([#4286](https://github.com/open-telemetry/opentelemetry-specification/pull/4286))

## OTEPs

- The [open-telemetry/oteps](https://github.com/open-telemetry/oteps) repository was
  merged into the specification repository.
 ([#4288](https://github.com/open-telemetry/opentelemetry-specification/pull/4288))

## v1.39.0 (2024-11-06)

### Logs

- Simplify the name "Logs Instrumentation API" to just "Logs API".
  ([#4258](https://github.com/open-telemetry/opentelemetry-specification/pull/4258))
- Rename Log Bridge API to Logs API. Define the existing Logger methods to be
  Log Bridge Operations. Add EmitEvent to the Logger as an Instrumentation Operation.
  ([#4259](https://github.com/open-telemetry/opentelemetry-specification/pull/4259))

### Profiles

- Define required attributes for Mappings.
  ([#4197](https://github.com/open-telemetry/opentelemetry-specification/pull/4197))

### Compatibility

- Add requirement to allow extending Stable APIs.
  ([#4270](https://github.com/open-telemetry/opentelemetry-specification/pull/4270))

### SDK Configuration

- Clarify declarative configuration parse requirements for null vs empty.
  ([#4269](https://github.com/open-telemetry/opentelemetry-specification/pull/4269))

### Common

- Define prototype for proposed features in development.
  ([#4273](https://github.com/open-telemetry/opentelemetry-specification/pull/4273))

## v1.38.0 (2024-10-10)

### Traces

- Make all fields as identifying for Tracer. Previously attributes were omitted from being identifying.
  ([#4161](https://github.com/open-telemetry/opentelemetry-specification/pull/4161))
- Clarify that `Export` MUST NOT be called by simple and batching processors concurrently.
  ([#4205](https://github.com/open-telemetry/opentelemetry-specification/pull/4205))

### Metrics

- Make all fields as identifying for Meter. Previously attributes were omitted from being identifying.
  ([#4161](https://github.com/open-telemetry/opentelemetry-specification/pull/4161))
- Add support for filtering attribute keys for streams via an exclude list.
  ([#4188](https://github.com/open-telemetry/opentelemetry-specification/pull/4188))
- Clarify that `Enabled` only applies to synchronous instruments.
  ([#4211](https://github.com/open-telemetry/opentelemetry-specification/pull/4211))
- Clarify that applying cardinality limits should be done after attribute filtering.
  ([#4228](https://github.com/open-telemetry/opentelemetry-specification/pull/4228))
- Mark cardinality limits as stable.
  ([#4222](https://github.com/open-telemetry/opentelemetry-specification/pull/4222))

### Logs

- Make all fields as identifying for Logger. Previously attributes were omitted from being identifying.
  ([#4161](https://github.com/open-telemetry/opentelemetry-specification/pull/4161))
- Define `Enabled` parameters for `Logger`.
  ([#4203](https://github.com/open-telemetry/opentelemetry-specification/pull/4203))
  ([#4221](https://github.com/open-telemetry/opentelemetry-specification/pull/4221))
- Introduce initial placeholder for the new user-facing Logs API, adding references
  to existing API's informing of the coming changes while the definition is defined.
  ([#4236](https://github.com/open-telemetry/opentelemetry-specification/pull/4236))

### Common

- Define equality for attributes and collection of attributes.
  ([#4161](https://github.com/open-telemetry/opentelemetry-specification/pull/4161))
- Update Instrumentation Scope glossary entry with correct identifying fields
  ([#4244](https://github.com/open-telemetry/opentelemetry-specification/pull/4244))
