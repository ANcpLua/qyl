
#nullable disable

using System;

namespace Qyl.Domains.Observe.Session
{
    internal static partial class DeviceTypeExtensions
    {
        public static string ToSerialString(this DeviceType value) => value switch
        {
            DeviceType.Desktop => "desktop",
            DeviceType.Mobile => "mobile",
            DeviceType.Tablet => "tablet",
            DeviceType.Tv => "tv",
            DeviceType.Console => "console",
            DeviceType.Wearable => "wearable",
            DeviceType.Iot => "iot",
            DeviceType.Bot => "bot",
            DeviceType.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DeviceType value.")
        };

        public static DeviceType ToDeviceType(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "desktop"))
            {
                return DeviceType.Desktop;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "mobile"))
            {
                return DeviceType.Mobile;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "tablet"))
            {
                return DeviceType.Tablet;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "tv"))
            {
                return DeviceType.Tv;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "console"))
            {
                return DeviceType.Console;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "wearable"))
            {
                return DeviceType.Wearable;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "iot"))
            {
                return DeviceType.Iot;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "bot"))
            {
                return DeviceType.Bot;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "unknown"))
            {
                return DeviceType.Unknown;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DeviceType value.");
        }
    }
}
