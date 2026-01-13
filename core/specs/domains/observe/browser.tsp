// =============================================================================
// QYL v2.0 - Browser Semantic Conventions (OTel v1.38)
// =============================================================================
// Browser/RUM telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Observe.Browser;

// =============================================================================
// Browser Attributes
// =============================================================================

@doc("Browser span/event attributes")
model BrowserAttributes {
  @doc("Browser brand names and versions")
  @encodedName("application/json", "browser.brands")
  brands?: string[];

  @doc("Browser platform")
  @encodedName("application/json", "browser.platform")
  platform?: string;

  @doc("Browser mobile flag")
  @encodedName("application/json", "browser.mobile")
  mobile?: boolean;

  @doc("Browser language")
  @encodedName("application/json", "browser.language")
  language?: LanguageCode;

  @doc("User agent string")
  @encodedName("application/json", "user_agent.original")
  userAgentOriginal?: UserAgent;
}

// =============================================================================
// Browser Event Models
// =============================================================================

@doc("Page view event")
model PageViewEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "browser.page_view";

  @doc("Page URL")
  @encodedName("application/json", "page.url")
  pageUrl: UrlString;

  @doc("Page title")
  @encodedName("application/json", "page.title")
  pageTitle?: string;

  @doc("Referrer URL")
  @encodedName("application/json", "page.referrer")
  pageReferrer?: UrlString;

  @doc("Page load time in milliseconds")
  @encodedName("application/json", "page.load_time_ms")
  pageLoadTimeMs?: DurationMs;

  @doc("DOM content loaded time")
  @encodedName("application/json", "page.dom_content_loaded_ms")
  domContentLoadedMs?: DurationMs;

  @doc("First contentful paint time")
  @encodedName("application/json", "page.fcp_ms")
  fcpMs?: DurationMs;

  @doc("Largest contentful paint time")
  @encodedName("application/json", "page.lcp_ms")
  lcpMs?: DurationMs;

  @doc("Cumulative layout shift score")
  @encodedName("application/json", "page.cls")
  cls?: float64;

  @doc("First input delay in milliseconds")
  @encodedName("application/json", "page.fid_ms")
  fidMs?: DurationMs;

  @doc("Time to first byte")
  @encodedName("application/json", "page.ttfb_ms")
  ttfbMs?: DurationMs;

  @doc("Session ID")
  @encodedName("application/json", "session.id")
  sessionId?: SessionId;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("User interaction event")
model UserInteractionEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "browser.user_interaction";

  @doc("Interaction type")
  @encodedName("application/json", "interaction.type")
  interactionType: BrowserInteractionType;

  @doc("Element selector")
  @encodedName("application/json", "element.selector")
  elementSelector?: string;

  @doc("Element tag name")
  @encodedName("application/json", "element.tag")
  elementTag?: string;

  @doc("Element ID")
  @encodedName("application/json", "element.id")
  elementId?: string;

  @doc("Element text content (truncated)")
  @encodedName("application/json", "element.text")
  elementText?: string;

  @doc("X coordinate")
  x?: int32;

  @doc("Y coordinate")
  y?: int32;

  @doc("Page URL")
  @encodedName("application/json", "page.url")
  pageUrl: UrlString;

  @doc("Session ID")
  @encodedName("application/json", "session.id")
  sessionId?: SessionId;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Browser error event")
model BrowserErrorEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "browser.error";

  @doc("Error type")
  @encodedName("application/json", "error.type")
  errorType: BrowserErrorType;

  @doc("Error message")
  @encodedName("application/json", "error.message")
  errorMessage: string;

  @doc("Error stack trace")
  @encodedName("application/json", "error.stacktrace")
  errorStacktrace?: string;

  @doc("Source file")
  @encodedName("application/json", "error.source_file")
  sourceFile?: string;

  @doc("Line number")
  @encodedName("application/json", "error.line_number")
  lineNumber?: int32;

  @doc("Column number")
  @encodedName("application/json", "error.column_number")
  columnNumber?: int32;

  @doc("Page URL")
  @encodedName("application/json", "page.url")
  pageUrl: UrlString;

  @doc("Session ID")
  @encodedName("application/json", "session.id")
  sessionId?: SessionId;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Browser Enumerations
// =============================================================================

@doc("Browser interaction types")
enum BrowserInteractionType {
  @doc("Click event")
  click: "click",

  @doc("Double click event")
  dblclick: "dblclick",

  @doc("Form submission")
  submit: "submit",

  @doc("Input change")
  change: "change",

  @doc("Focus event")
  focus: "focus",

  @doc("Blur event")
  blur: "blur",

  @doc("Scroll event")
  scroll: "scroll",

  @doc("Key press")
  keypress: "keypress",

  @doc("Touch start")
  touchstart: "touchstart",

  @doc("Touch end")
  touchend: "touchend",

  @doc("Mouse enter")
  mouseenter: "mouseenter",

  @doc("Mouse leave")
  mouseleave: "mouseleave",

  @doc("Copy")
  copy: "copy",

  @doc("Paste")
  paste: "paste",
}

@doc("Browser error types")
enum BrowserErrorType {
  @doc("JavaScript error")
  javascript: "javascript",

  @doc("Unhandled promise rejection")
  unhandledRejection: "unhandled_rejection",

  @doc("Resource loading error")
  resource: "resource",

  @doc("CORS error")
  cors: "cors",

  @doc("CSP violation")
  csp: "csp",

  @doc("Network error")
  network: "network",

  @doc("Syntax error")
  syntax: "syntax",

  @doc("Type error")
  type: "type",

  @doc("Reference error")
  reference: "reference",

  @doc("Range error")
  range: "range",

  @doc("Custom error")
  custom: "custom",
}

// =============================================================================
// Web Vitals Metrics
// =============================================================================

@doc("Core Web Vitals LCP metric")
model WebVitalsLcpMetric {
  @doc("Metric name")
  name: "browser.lcp";

  @doc("Metric unit (milliseconds)")
  unit: "ms";

  @doc("Page URL pattern")
  @encodedName("application/json", "page.url.pattern")
  pageUrlPattern?: string;
}

@doc("Core Web Vitals CLS metric")
model WebVitalsClsMetric {
  @doc("Metric name")
  name: "browser.cls";

  @doc("Metric unit (unitless score)")
  unit: "1";

  @doc("Page URL pattern")
  @encodedName("application/json", "page.url.pattern")
  pageUrlPattern?: string;
}

@doc("Core Web Vitals FID metric")
model WebVitalsFidMetric {
  @doc("Metric name")
  name: "browser.fid";

  @doc("Metric unit (milliseconds)")
  unit: "ms";

  @doc("Page URL pattern")
  @encodedName("application/json", "page.url.pattern")
  pageUrlPattern?: string;
}

@doc("Interaction to Next Paint metric")
model WebVitalsInpMetric {
  @doc("Metric name")
  name: "browser.inp";

  @doc("Metric unit (milliseconds)")
  unit: "ms";

  @doc("Page URL pattern")
  @encodedName("application/json", "page.url.pattern")
  pageUrlPattern?: string;
}

@doc("Time to First Byte metric")
model WebVitalsTtfbMetric {
  @doc("Metric name")
  name: "browser.ttfb";

  @doc("Metric unit (milliseconds)")
  unit: "ms";

  @doc("Page URL pattern")
  @encodedName("application/json", "page.url.pattern")
  pageUrlPattern?: string;
}
