

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Geo;

public static class GeoAttributes
{
    public const string ContinentCode = "geo.continent.code";

    public static class ContinentCodeValues
    {
        public const string Af = "AF";

        public const string An = "AN";

        public const string As = "AS";

        public const string Eu = "EU";

        public const string Na = "NA";

        public const string Oc = "OC";

        public const string Sa = "SA";
    }

    public const string CountryIsoCode = "geo.country.iso_code";

    public const string LocalityName = "geo.locality.name";

    public const string LocationLat = "geo.location.lat";

    public const string LocationLon = "geo.location.lon";

    public const string PostalCode = "geo.postal_code";

    public const string RegionIsoCode = "geo.region.iso_code";
}
