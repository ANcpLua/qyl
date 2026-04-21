import "../dist/index.js";

namespace Qyl.Semconv;

/**
 * Options for `@qylAttr`. All optional; the rule set only fires on what's present.
 */
model QylAttrOptions {
  cardinality?: "low" | "medium" | "high";
  stability?: "experimental" | "stable" | "deprecated";
  required?: boolean;
}

/**
 * Declares that a model property (or operation parameter) emits a qyl-owned
 * telemetry attribute under the given key and primitive type. The library's
 * `$onValidate` hook cross-checks every annotation against OTel semconv and
 * fires the QYL-LINT-00x diagnostics when rules are violated.
 */
extern dec qylAttr(
  target: ModelProperty | Operation,
  key: valueof string,
  type: valueof "string" | "int" | "long" | "double" | "boolean" | "string[]",
  options?: valueof QylAttrOptions
);
