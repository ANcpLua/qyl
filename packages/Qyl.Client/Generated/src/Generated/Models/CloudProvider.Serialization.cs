
#nullable disable

using System;

namespace Qyl.OTel.Resource
{
    internal static partial class CloudProviderExtensions
    {
        public static string ToSerialString(this CloudProvider value) => value switch
        {
            CloudProvider.AlibabaCloud => "alibaba_cloud",
            CloudProvider.Aws => "aws",
            CloudProvider.Azure => "azure",
            CloudProvider.Gcp => "gcp",
            CloudProvider.Heroku => "heroku",
            CloudProvider.IbmCloud => "ibm_cloud",
            CloudProvider.TencentCloud => "tencent_cloud",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CloudProvider value.")
        };

        public static CloudProvider ToCloudProvider(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "alibaba_cloud"))
            {
                return CloudProvider.AlibabaCloud;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "aws"))
            {
                return CloudProvider.Aws;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "azure"))
            {
                return CloudProvider.Azure;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "gcp"))
            {
                return CloudProvider.Gcp;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "heroku"))
            {
                return CloudProvider.Heroku;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "ibm_cloud"))
            {
                return CloudProvider.IbmCloud;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "tencent_cloud"))
            {
                return CloudProvider.TencentCloud;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CloudProvider value.");
        }
    }
}
