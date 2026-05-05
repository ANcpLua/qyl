
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Common;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Metrics
{

    public partial class Metric
    {
        [StringConstraint(MinLength = 1)]
        public string Name { get; set; }

        public string Description { get; set; }

        public string Unit { get; set; }

        public MetricData Data { get; set; }

        public Attribute[] Metadata { get; set; }

        public Qyl.OTel.Resource.Resource Resource { get; set; }

        [JsonPropertyName("instrumentation_scope")]
        public InstrumentationScope InstrumentationScope { get; set; }


    }
}
