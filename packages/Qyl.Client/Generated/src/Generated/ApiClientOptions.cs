
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Qyl.Api
{
    public partial class ApiClientOptions : ClientPipelineOptions
    {
        private const ServiceVersion LatestVersion = ServiceVersion.V3;

        public ApiClientOptions(ServiceVersion version = LatestVersion)
        {
            Version = version switch
            {
                ServiceVersion.V1 => "2025-12-01",
                ServiceVersion.V2 => "2026-01-15",
                ServiceVersion.V3 => "2026-01-26",
                _ => throw new NotSupportedException()
            };
        }

        internal ApiClientOptions(IConfigurationSection section) : base(section)
        {
            Version = "2026-01-26";
            if (section is null || !section.Exists())
            {
                return;
            }
            if (section["Version"] is string version)
            {
                Version = version;
            }
        }

        internal string Version { get; }

        public enum ServiceVersion
        {
            V1 = 1,
            V2 = 2,
            V3 = 3
        }
    }
}
