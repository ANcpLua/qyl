
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.OTel.Enums;

namespace Qyl.Api
{
    public partial class MetricMetadata
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal MetricMetadata(string name, MetricType @type, IEnumerable<string> labelKeys, IEnumerable<string> services)
        {
            Name = name;
            Type = @type;
            LabelKeys = labelKeys.ToList();
            Services = services.ToList();
        }

        internal MetricMetadata(string name, string description, string unit, MetricType @type, IList<string> labelKeys, IList<string> services, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            Description = description;
            Unit = unit;
            Type = @type;
            LabelKeys = labelKeys;
            Services = services;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public string Description { get; }

        public string Unit { get; }

        public MetricType Type { get; }

        public IList<string> LabelKeys { get; }

        public IList<string> Services { get; }
    }
}
