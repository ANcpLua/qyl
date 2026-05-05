
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionGeoInfo
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionGeoInfo()
        {
        }

        internal SessionGeoInfo(string countryCode, string countryName, string region, string city, string postalCode, string timezone, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            CountryCode = countryCode;
            CountryName = countryName;
            Region = region;
            City = city;
            PostalCode = postalCode;
            Timezone = timezone;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string CountryCode { get; }

        public string CountryName { get; }

        public string Region { get; }

        public string City { get; }

        public string PostalCode { get; }

        public string Timezone { get; }
    }
}
