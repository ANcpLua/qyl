
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionCountryStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionCountryStats(string countryCode, string countryName, long count, double percentage)
        {
            CountryCode = countryCode;
            CountryName = countryName;
            Count = count;
            Percentage = percentage;
        }

        internal SessionCountryStats(string countryCode, string countryName, long count, double percentage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            CountryCode = countryCode;
            CountryName = countryName;
            Count = count;
            Percentage = percentage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string CountryCode { get; }

        public string CountryName { get; }

        public long Count { get; }

        public double Percentage { get; }
    }
}
