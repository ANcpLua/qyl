// =============================================================================
// QYL v2.0 - Code Semantic Conventions (OTel v1.39)
// =============================================================================
// Source code and code-related telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.AI.Code;

// =============================================================================
// Code Span Attributes
// =============================================================================

@doc("Code-related span attributes for tracking source code context")
model CodeAttributes {
  @doc("The column number in the source file (1-indexed)")
  @encodedName("application/json", "code.column")
  @minValue(1)
  column?: int32;

  @doc("The source file path (relative or absolute)")
  @encodedName("application/json", "code.filepath")
  filepath?: string;

  @doc("The function/method name")
  @encodedName("application/json", "code.function")
  function?: string;

  @doc("The line number in the source file (1-indexed)")
  @encodedName("application/json", "code.lineno")
  @minValue(1)
  lineno?: int32;

  @doc("The namespace/module containing the code")
  @encodedName("application/json", "code.namespace")
  `namespace`?: string;

  @doc("The stacktrace as a string")
  @encodedName("application/json", "code.stacktrace")
  stacktrace?: string;
}

// =============================================================================
// Code Location Model
// =============================================================================

@doc("Precise source code location for debugging and tracing")
model CodeLocation {
  @doc("Source file path")
  filepath: string;

  @doc("Line number (1-indexed)")
  @encodedName("application/json", "line_number")
  @minValue(1)
  lineNumber: int32;

  @doc("Column number (1-indexed)")
  @encodedName("application/json", "column_number")
  @minValue(1)
  columnNumber?: int32;

  @doc("Function/method name")
  @encodedName("application/json", "function_name")
  functionName?: string;

  @doc("Class/type name")
  @encodedName("application/json", "class_name")
  className?: string;

  @doc("Namespace/module")
  `namespace`?: string;
}

// =============================================================================
// Stack Frame Model
// =============================================================================

@doc("Single frame in a call stack")
model StackFrame {
  @doc("Frame index (0 = top of stack)")
  index: int32;

  @doc("Source location")
  location: CodeLocation;

  @doc("Whether this is user code (not library/framework)")
  @encodedName("application/json", "is_user_code")
  isUserCode?: boolean;

  @doc("Assembly/module name")
  @encodedName("application/json", "module_name")
  moduleName?: string;

  @doc("Assembly/module version")
  @encodedName("application/json", "module_version")
  moduleVersion?: SemVer;

  @doc("Native/managed indicator")
  @encodedName("application/json", "is_native")
  isNative?: boolean;
}

@doc("Full stack trace")
model StackTrace {
  @doc("Stack frames (top to bottom)")
  frames: StackFrame[];

  @doc("Whether the stack was truncated")
  truncated?: boolean;

  @doc("Total frame count before truncation")
  @encodedName("application/json", "total_frames")
  totalFrames?: int32;
}

// =============================================================================
// Code Coverage (for testing spans)
// =============================================================================

@doc("Code coverage information")
model CodeCoverage {
  @doc("Line coverage percentage")
  @encodedName("application/json", "line_coverage")
  lineCoverage?: Percentage;

  @doc("Branch coverage percentage")
  @encodedName("application/json", "branch_coverage")
  branchCoverage?: Percentage;

  @doc("Function coverage percentage")
  @encodedName("application/json", "function_coverage")
  functionCoverage?: Percentage;

  @doc("Statement coverage percentage")
  @encodedName("application/json", "statement_coverage")
  statementCoverage?: Percentage;

  @doc("Lines covered")
  @encodedName("application/json", "lines_covered")
  linesCovered?: Count;

  @doc("Total lines")
  @encodedName("application/json", "lines_total")
  linesTotal?: Count;
}
