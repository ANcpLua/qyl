
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Qyl.Api
{
    public partial class ApiClientSettings : ClientSettings
    {
        public Uri Endpoint { get; set; }

        public ApiClientOptions Options { get; set; }

        protected override void BindCore(IConfigurationSection section)
        {
            if (Uri.TryCreate(section["Endpoint"], UriKind.Absolute, out Uri endpoint))
            {
                Endpoint = endpoint;
            }
            IConfigurationSection optionsSection = section.GetSection("Options");
            if (optionsSection.Exists())
            {
                Options = new ApiClientOptions(optionsSection);
            }
        }
    }
}
