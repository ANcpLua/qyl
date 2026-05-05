
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using System.Text.Json.Nodes;

namespace Qyl.Api
{

    public partial class MetricTimeSeries
    {
        public JsonObject Labels { get; set; }

        public MetricDataPoint[] Points { get; set; }


    }
}
