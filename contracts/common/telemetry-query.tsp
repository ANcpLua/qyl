import "./types.tsp";
using TypeSpec.OpenAPI;
namespace Qyl.Api.Contracts.Common;

@doc("Opaque project-scoped failure fingerprint issued and verified by the collector.")
@minLength(1)
@maxLength(512)
scalar FailureId extends string;

@doc("Signed self-contained project-scoped verification token issued by the collector.")
@minLength(1)
@maxLength(8192)
scalar VerifyId extends string;

@doc("A comparison operand keeps its JSON type: a number or boolean is never coerced to a string.")
union MatcherValue { text: string, number: float64, boolean: boolean }

@doc("Supported predicate operators. eq, neq and in compare typed values: a number equals a number of the same value, a string the same string and a boolean the same boolean, so 1 does not equal \"1\". prefix and contains match the value's text; gt, gte, lt and lte compare numbers.")
enum MatcherOp { eq, neq, `in`, prefix, contains, exists, gt, gte, lt, lte }

@doc("Attribute namespace in which the predicate is evaluated.")
enum MatcherScope { span, resource, log }

@doc("TelemetryMatcher contract.")
model TelemetryMatcher {
  @minLength(1)
  @maxLength(256)
  key: string;
  `op`: MatcherOp;
  value?: MatcherValue;
  @minItems(1)
  @maxItems(256)
  values?: MatcherValue[];
  scope?: MatcherScope;
}

@doc("Absolute half-open window applied by the collector.")
model ToolWindow {
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;
}

@doc("Scan coverage; unknown counts are null, never an invented zero.")
model ToolCoverage {
  examined: Count | null;
  matched: Count | null;
  returned: Count | null;
  truncated: boolean;
  @encodedName("application/json", "scan_complete")
  scanComplete: boolean;
  @encodedName("application/json", "as_of")
  asOf: utcDateTime;
}

@doc("ToolReadContext contract.")
model ToolReadContext {
  window: ToolWindow;
  coverage: ToolCoverage;
  @doc("Item totals by paged collection path. Omitted totals are unknown, not zero.")
  @encodedName("application/json", "page_totals")
  pageTotals?: Record<int64>;
}

