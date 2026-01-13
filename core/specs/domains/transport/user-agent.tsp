
// =============================================================================
// QYL v2.0 - User Agent Semantic Conventions (OTel v1.38)
// =============================================================================
// User agent parsing and browser detection attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Transport.UserAgent;

// =============================================================================
// User Agent Attributes
// =============================================================================

@doc("User agent attributes")
model UserAgentAttributes {
  @doc("Original user agent string")
  @encodedName("application/json", "user_agent.original")
  original: string;

  @doc("User agent name (browser/client name)")
  @encodedName("application/json", "user_agent.name")
  name?: string;

  @doc("User agent version")
  @encodedName("application/json", "user_agent.version")
  version?: string;

  @doc("Synthetic source (if bot/crawler)")
  @encodedName("application/json", "user_agent.synthetic.type")
  syntheticType?: SyntheticType;
}

// =============================================================================
// Synthetic Type Enumeration
// =============================================================================

@doc("Synthetic user agent types")
enum SyntheticType {
  @doc("Search engine bot")
  bot: "bot",

  @doc("Monitoring/synthetic testing")
  monitoring: "monitoring",

  @doc("Web crawler")
  crawler: "crawler",

  @doc("API client")
  apiClient: "api_client",

  @doc("Browser automation")
  automation: "automation",
}

// =============================================================================
// Parsed User Agent
// =============================================================================

@doc("Fully parsed user agent")
model ParsedUserAgent {
  @doc("Original user agent string")
  original: string;

  @doc("Browser information")
  browser?: BrowserInfo;

  @doc("Operating system information")
  os?: OsInfo;

  @doc("Device information")
  device?: DeviceInfo;

  @doc("Bot information")
  bot?: BotInfo;
}

@doc("Browser information")
model BrowserInfo {
  @doc("Browser name")
  name: string;

  @doc("Browser version")
  version?: string;

  @doc("Major version")
  @encodedName("application/json", "major_version")
  majorVersion?: int32;

  @doc("Browser engine")
  engine?: BrowserEngine;

  @doc("Is mobile browser")
  @encodedName("application/json", "is_mobile")
  isMobile?: boolean;
}

@doc("Browser engines")
enum BrowserEngine {
  @doc("Blink (Chrome, Edge, Opera)")
  blink: "blink",

  @doc("Gecko (Firefox)")
  gecko: "gecko",

  @doc("WebKit (Safari)")
  webkit: "webkit",

  @doc("Trident (IE)")
  trident: "trident",

  @doc("EdgeHTML (Legacy Edge)")
  edgehtml: "edgehtml",

  @doc("Presto (Old Opera)")
  presto: "presto",
}

@doc("Operating system information from user agent")
model OsInfo {
  @doc("OS name")
  name: string;

  @doc("OS version")
  version?: string;

  @doc("OS platform")
  platform?: OsPlatform;
}

@doc("OS platforms")
enum OsPlatform {
  @doc("Windows")
  windows: "windows",

  @doc("macOS")
  macos: "macos",

  @doc("Linux")
  linux: "linux",

  @doc("iOS")
  ios: "ios",

  @doc("Android")
  android: "android",

  @doc("Chrome OS")
  chromeos: "chromeos",

  @doc("Unknown")
  `unknown`: "unknown",
}

@doc("Device information")
model DeviceInfo {
  @doc("Device type")
  type: DeviceType;

  @doc("Device brand")
  brand?: string;

  @doc("Device model")
  `model`?: string;

  @doc("Is touch capable")
  @encodedName("application/json", "is_touch")
  isTouch?: boolean;
}

@doc("Device types")
enum DeviceType {
  @doc("Desktop computer")
  desktop: "desktop",

  @doc("Mobile phone")
  mobile: "mobile",

  @doc("Tablet")
  tablet: "tablet",

  @doc("Smart TV")
  tv: "tv",

  @doc("Game console")
  console: "console",

  @doc("Wearable device")
  wearable: "wearable",

  @doc("IoT device")
  iot: "iot",

  @doc("Bot/Crawler")
  bot: "bot",

  @doc("Unknown")
  `unknown`: "unknown",
}

@doc("Bot information")
model BotInfo {
  @doc("Bot name")
  name: string;

  @doc("Bot category")
  category: BotCategory;

  @doc("Bot owner/operator")
  owner?: string;

  @doc("Bot URL")
  url?: UrlString;

  @doc("Is verified bot")
  @encodedName("application/json", "is_verified")
  isVerified?: boolean;
}

@doc("Bot categories")
enum BotCategory {
  @doc("Search engine crawler")
  searchEngine: "search_engine",

  @doc("SEO tool")
  seo: "seo",

  @doc("Social media bot")
  social: "social",

  @doc("Feed fetcher")
  feed: "feed",

  @doc("Monitoring/uptime checker")
  monitoring: "monitoring",

  @doc("Security scanner")
  security: "security",

  @doc("AI/ML crawler")
  ai: "ai",

  @doc("Screenshot/preview service")
  screenshot: "screenshot",

  @doc("Archive/wayback")
  archive: "archive",

  @doc("Other bot")
  other: "other",
}
