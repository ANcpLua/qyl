// =============================================================================
// Code Types (Stack Traces, Source Locations)
// =============================================================================
// Extracted from domains/ai/code.tsp for use by exceptions.tsp

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.OTel.Code;

// =============================================================================
// Source Location
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
}

// =============================================================================
// Stack Frame
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

// =============================================================================
// Stack Trace
// =============================================================================

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
