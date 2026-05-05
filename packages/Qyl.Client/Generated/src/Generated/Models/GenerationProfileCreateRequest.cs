
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class GenerationProfileCreateRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public GenerationProfileCreateRequest(string name, string targetFramework)
        {
            Argument.AssertNotNull(name, nameof(name));
            Argument.AssertNotNull(targetFramework, nameof(targetFramework));

            Name = name;
            TargetFramework = targetFramework;
        }

        internal GenerationProfileCreateRequest(string name, string targetFramework, string description, string featuresJson, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            TargetFramework = targetFramework;
            Description = description;
            FeaturesJson = featuresJson;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public string TargetFramework { get; }

        public string Description { get; set; }

        public string FeaturesJson { get; set; }
    }
}
