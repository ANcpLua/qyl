
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionClientInfo
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionClientInfo()
        {
        }

        internal SessionClientInfo(string ip, string userAgent, DeviceType? deviceType, string os, string browser, string browserVersion, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Ip = ip;
            UserAgent = userAgent;
            DeviceType = deviceType;
            Os = os;
            Browser = browser;
            BrowserVersion = browserVersion;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Ip { get; }

        public string UserAgent { get; }

        public DeviceType? DeviceType { get; }

        public string Os { get; }

        public string Browser { get; }

        public string BrowserVersion { get; }
    }
}
