// =============================================================================
// QYL v2.0 - Geo Semantic Conventions (OTel v1.39)
// =============================================================================
// Geographic location attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Identity.Geo;

// =============================================================================
// Geo Attributes
// =============================================================================

@doc("Geographic location attributes")
model GeoAttributes {
  @doc("City name")
  @encodedName("application/json", "geo.city")
  city?: string;

  @doc("Country ISO code (ISO 3166-1 alpha-2)")
  @encodedName("application/json", "geo.country.iso_code")
  countryIsoCode?: CountryCode;

  @doc("Country name")
  @encodedName("application/json", "geo.country.name")
  countryName?: string;

  @doc("Continent code")
  @encodedName("application/json", "geo.continent.code")
  continentCode?: ContinentCode;

  @doc("Continent name")
  @encodedName("application/json", "geo.continent.name")
  continentName?: string;

  @doc("Region ISO code")
  @encodedName("application/json", "geo.region.iso_code")
  regionIsoCode?: string;

  @doc("Region name")
  @encodedName("application/json", "geo.region.name")
  regionName?: string;

  @doc("Postal/ZIP code")
  @encodedName("application/json", "geo.postal_code")
  postalCode?: string;

  @doc("Latitude")
  @encodedName("application/json", "geo.latitude")
  @minValue(-90)
  @maxValue(90)
  latitude?: float64;

  @doc("Longitude")
  @encodedName("application/json", "geo.longitude")
  @minValue(-180)
  @maxValue(180)
  longitude?: float64;

  @doc("Timezone (IANA)")
  @encodedName("application/json", "geo.timezone")
  timezone?: string;
}

// =============================================================================
// Continent Codes
// =============================================================================

@doc("Continent codes")
enum ContinentCode {
  @doc("Africa")
  af: "AF",

  @doc("Antarctica")
  an: "AN",

  @doc("Asia")
  as: "AS",

  @doc("Europe")
  eu: "EU",

  @doc("North America")
  na: "NA",

  @doc("Oceania")
  oc: "OC",

  @doc("South America")
  sa: "SA",
}

// =============================================================================
// Country Codes (ISO 3166-1 alpha-2) - Common subset
// =============================================================================

@doc("Country codes (ISO 3166-1 alpha-2)")
@minLength(2)
@maxLength(2)
scalar CountryCode extends string;

// =============================================================================
// Geo Location Model
// =============================================================================

@doc("Full geographic location")
model GeoLocation {
  @doc("Latitude")
  @minValue(-90)
  @maxValue(90)
  latitude: float64;

  @doc("Longitude")
  @minValue(-180)
  @maxValue(180)
  longitude: float64;

  @doc("Altitude in meters")
  altitude?: float64;

  @doc("Accuracy in meters")
  accuracy?: float64;

  @doc("Timestamp of location fix")
  timestamp?: utcDateTime;
}

// =============================================================================
// IP Geolocation
// =============================================================================

@doc("IP-based geolocation result")
model IpGeolocation {
  @doc("IP address")
  @encodedName("application/json", "ip_address")
  ipAddress: IpAddress;

  @doc("Resolved location")
  location: GeoAttributes;

  @doc("ASN number")
  asn?: int32;

  @doc("ASN organization")
  @encodedName("application/json", "asn_org")
  asnOrg?: string;

  @doc("ISP name")
  isp?: string;

  @doc("Connection type")
  @encodedName("application/json", "connection_type")
  connectionType?: ConnectionType;

  @doc("Is proxy/VPN")
  @encodedName("application/json", "is_proxy")
  isProxy?: boolean;

  @doc("Is hosting provider")
  @encodedName("application/json", "is_hosting")
  isHosting?: boolean;
}

@doc("Connection types")
enum ConnectionType {
  @doc("Cable/DSL")
  cableDsl: "cable_dsl",

  @doc("Corporate")
  corporate: "corporate",

  @doc("Cellular")
  cellular: "cellular",

  @doc("Dialup")
  dialup: "dialup",

  @doc("Satellite")
  satellite: "satellite",
}
