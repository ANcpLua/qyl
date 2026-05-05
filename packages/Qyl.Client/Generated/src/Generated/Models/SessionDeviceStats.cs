
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionDeviceStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionDeviceStats(DeviceType deviceType, long count, double percentage)
        {
            DeviceType = deviceType;
            Count = count;
            Percentage = percentage;
        }

        internal SessionDeviceStats(DeviceType deviceType, long count, double percentage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            DeviceType = deviceType;
            Count = count;
            Percentage = percentage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public DeviceType DeviceType { get; }

        public long Count { get; }

        public double Percentage { get; }
    }
}
